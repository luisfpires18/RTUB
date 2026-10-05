---
name: rtub-push
description: RTUB-specific Web Push facts — the WebPushOptions/VAPID contract and its Owner-vs-Enabled gate, the dual server+client opt-out flags that must stay in sync, push-only delivery (no inbox copy since 027) and 404/410 subscription cleanup, base64url key normalization, the two separate service-worker registration paths, and the iOS/Android/TWA workarounds baked into the push handler. Load this whenever the task touches Web Push, VAPID keys or WebPush config, push subscription storage or the PushController endpoints, the notification opt-in/permission UI, push delivery or fan-out from a service or background job, the service worker's push/notificationclick/pushsubscriptionchange handlers, or iOS/Android push behavior. Do not load it for service-worker caching, offline fallback, manifest/install-prompt work, or ordinary frontend changes.
---

# RTUB Web Push

Verified against the current implementation. Non-obvious things that are easy to break.
Read the linked file when you need the detail.

## Shape of the subsystem

| Concern | File |
| --- | --- |
| Config/VAPID options | `src/RTUB.Application/Configuration/WebPushOptions.cs` |
| Send + subscribe logic | `src/RTUB.Application/Services/PushNotificationService.cs` |
| Notification text/URL/tag builders | `src/RTUB.Application/Factories/PushNotificationFactory.cs` (all pt-PT copy) |
| Persistence | `src/RTUB.Application/Repositories/PushSubscriptionRepository.cs`, `src/RTUB.Core/Entities/PushSubscription.cs` |
| HTTP endpoints | `src/RTUB.Web/Controllers/PushController.cs` (`/api/push/{status,subscribe,unsubscribe,broadcast,send-to-selected}`) |
| Browser manager | `src/RTUB.Web/wwwroot/js/push-notifications.js` |
| SW push handlers | `src/RTUB.Web/wwwroot/service-worker.js` — the `push`, `notificationclick` and `pushsubscriptionchange` listeners |
| Opt-in UI | `src/RTUB.Shared/Components/UI/PushNotificationPrompt.razor`, `PushNotificationToggle.razor` |
| DI | `ServiceCollectionExtensions.AddPushNotificationServices` (all three registrations are **scoped**) |

Library is `WebPush-NetCore`. `IPushNotificationService` is called from most domain services, several
routable pages, and the reminder/notification background services in `src/RTUB.Application/Services/`
(`ActivityReminder`, `CalotesNotification`, `PendingRequestReminder`, `QuestionNotification`,
`RehearsalApprovalReminder`, `WeeklyNotification`). Notification copy comes from `IPushNotificationFactory` —
add new notification types there rather than building `SendPushNotificationDto` inline.

## Configuration and secrets

`WebPushOptions` binds section `"WebPush"`. `appsettings.json` carries **only** `Enabled: true` —
`VapidSubject`, `VapidPublicKey`, `VapidPrivateKey` are supplied per-environment (env vars / user-secrets)
and are never committed. `appsettings.Production.json` has no `WebPush` section at all, so production
depends entirely on externally-supplied VAPID values. Never write a real key into the repo, and never
return `VapidPrivateKey` to a client — only `GetVapidPublicKey()` crosses that boundary.

Two independent gates, easy to confuse:
- `WebPush:Enabled` — feature flag for **non-Owner** users. `PushController.HasWebPushAccess()` is
  `User.IsInRole("Owner") || _webPushOptions.Enabled`, so Owners bypass the flag.
- `WebPushOptions.IsConfigured()` — all three VAPID values non-blank. Sends are skipped when false.

`IsConfigured()` only checks non-blankness. `SetVapidDetails` is called in the constructor inside a
`try/catch (ArgumentException)` that logs a warning and continues, so a **malformed** key yields
`IsConfigured() == true` with a client that can never send. If pushes silently do nothing, check for the
startup warning "Invalid VAPID configuration" before suspecting subscriptions.

## Opt-out is tracked in two places — keep them in sync

Server: `ApplicationUser.PushNotificationsOptedOut` (`src/RTUB.Core/Entities/ApplicationUser.cs`),
via `IsOptedOutAsync`/`SetOptedOutAsync`. `PushController` sets it `false` on subscribe and `true` on
unsubscribe. Client: `localStorage['rtub-push-opted-out']`, via `pwaHelper.markOptedOut/clearOptedOut/isOptedOut`.

Both exist because unsubscribing does **not** revoke `Notification.permission` — it stays `granted`,
especially on iOS. Without the flag, the 30-minute health check and `validateAndRefreshSubscription` in
`push-notifications.js` would see permission granted + no subscription and silently re-subscribe a user
who deliberately opted out. Any new recovery/self-healing path must check the opt-out flag first, and
any new opt-out path must set both sides (`/api/push/status` returns `isOptedOut` and `pwa-helper.js`
mirrors it into localStorage).

Related flags: `localStorage['rtub-push-subscription-lost']` marks a failed recovery so the prompt UI can
surface it; `rtub-push-prompted` suppresses repeat prompting.

## Delivery semantics

- **Push only, no in-app copy (since 027).** Messages / Conversas was removed (`docs/messages-removal.md`):
  `SendToUserAsync`, `BroadcastAsync` and `SendToSelectedUsersAsync` no longer write the old "Sistema RTUB"
  inbox message, and `SendPushOnlyAsync` is gone. With push unconfigured a send does nothing; a member without
  push sees the notification nowhere in the app. Do not reintroduce a write into the message tables: they are
  kept only until the contract task drops them, and `MessagesRollbackSafetyTests` fails if anything touches them.
- `404 NotFound` / `410 Gone` from the push service ⇒ subscription is deleted immediately
  (`SendNotificationWithResultAsync`). This is why the client re-syncs on every health check: the server
  may have dropped a record the browser still holds.
- Retries: 3 attempts over transient `HttpRequestException`/`IOException`/`SocketException` (checked
  through the whole inner-exception chain) with 2^n backoff; `429` backs off 3^n. Every other exception
  returns false without retry.
- Send options are fixed at `TTL 86400` and header `Urgency: high` — the high urgency is what makes iOS
  and Android wake the device instead of batching. Don't lower it casually.
- Fan-out differs per method: `BroadcastAsync` uses unbounded `Task.WhenAll`; `SendToSelectedUsersAsync`
  is deliberately **sequential** so failures are attributable and push services don't rate-limit.
- `SendToSelectedUsersAsync` loads **all** active subscriptions and filters in memory
  (`GetAllActiveAsync().Where(...)`) — fine at RTUB's size, but know it before adding callers in a loop.
- No method on `IPushNotificationService` takes a `CancellationToken`, so background services cannot
  cancel an in-flight send.

## Key encoding

`PushNotificationService.NormalizeBase64Url` converts `+`→`-`, `/`→`_`, strips `=` before storing.
Browsers disagree (some Android send standard base64), and inconsistent storage created duplicate
subscription rows. On the client, always send `subscription.toJSON()` — the manual
`arrayBufferToBase64` helper is marked deprecated precisely because its `+/=` output caused silent
delivery failures on some Android devices.

## Platform workarounds already in place (don't strip them)

- **Unique tag per notification**: the SW appends `-<timestamp>` to the tag because iOS and Android
  silently *replace* same-tag notifications. The original tag is kept in `data.baseTag` for click routing.
- **No `vibrate` on iOS**: a present `vibrate` makes iOS Safari 16.4–17.x fail silently; the SW feature-detects.
- **`showNotification` inside `waitUntil`**: iOS kills the SW quickly; the push handler does nothing else now
  (the open-client message and app badge went with Messages in 027). Keep any future extra step after it.
- **iOS**: re-awaits `navigator.serviceWorker.ready` before `pushManager.subscribe()`; push requires an
  installed (standalone) PWA.
- **TWA/Android**: detected via `android-app://` referrer or standalone+Android; subscribe is retried 3×
  (1× otherwise) because subscriptions go stale after Play Store app updates.
- **`pushsubscriptionchange`**: Chrome/Android rotates endpoints; the SW re-POSTs to `/api/push/subscribe`,
  refetching the VAPID key from `/api/push/status` when the browser gave no `newSubscription`.
- App badge: nothing sets it since 027 (it showed unread messages, and the payload has no `unreadCount`);
  `notificationclick` still clears a badge an older worker left.

## Auth coupling

Every push endpoint is `[Authorize]`, and subscribe/unsubscribe resolve the user from
`ClaimTypes.NameIdentifier`. All client fetches use `credentials: 'include'` — including the ones inside
the service worker. Push subscription cannot work for an anonymous visitor, and any change to auth
cookie scope or SameSite affects the SW's `pushsubscriptionchange` re-subscribe.

## Tests and what they don't cover

`tests/RTUB.Application.Tests/` — `Services/PushNotificationServiceTests.cs`, `Factories/PushNotificationFactoryTests.cs`;
`RTUB.Web.Tests/Controllers/PushControllerTests.cs`; `RTUB.Core.Tests/Entities/PushSubscriptionTests.cs`;
`RTUB.Shared.Tests/Components/UI/PushNotification{Prompt,Toggle}Tests.cs`.

`PushNotificationService` constructs `new WebPushClient()` internally — it is not injected and cannot be
mocked. The tests use placeholder VAPID strings, so `SetVapidDetails` throws and is swallowed: **no test
exercises an actual send, a retry, or 404/410 cleanup.** They cover subscribe/unsubscribe, opt-out,
the push-only paths and filtering only. Treat green tests as no evidence that delivery works; verify
delivery changes on a real device.

## Known debt — current state, not the target design

Do not treat these as intended architecture, and do not fix them opportunistically (they are Phase 6
modernization items):

- `WebPush-NetCore` is obsolete; `WebPushClient` is newed up inline, blocking injection and tests.
- The service worker is registered from **two** places with different options: `registerServiceWorker()` in
  `wwwroot/js/sw-register.js` (passes `scope:'/'`, `updateViaCache:'none'`) and
  `PushNotificationsManager.registerServiceWorker()` in `wwwroot/js/push-notifications.js` (passes none).
  Both scripts are loaded by `MainLayout.razor`. Reuse an existing registration rather than adding a third.
- Two divergent paths into the JS manager: `PushNotificationPrompt.razor` calls `pwaHelper.*` wrappers;
  `PushNotificationToggle.razor` calls `JSRuntime.InvokeAsync("eval", ...)` with inline script.
- `PushNotificationsManager` mixes support detection, permission, subscription and server sync in one class.
- No `CancellationToken` anywhere in the push API; `BroadcastAsync` fan-out is unbounded.
- No delivery metrics beyond the `ILogger` counts from `SendToSelectedUsersAsync`.

Logging convention: push logs identify users by **username/nickname**, never by user ID
(`subscription.User?.Nickname ?? subscription.User?.FirstName`, falling back to `"unknown"`) — which is
why `GetByUserIdAsync`/`GetAllActiveAsync` `.Include(s => s.User)`. Keep that when adding log lines.
