---
name: rtub-push
description: RTUB-specific Web Push facts — the WebPushOptions/VAPID contract and its Owner-vs-Enabled gate, the server-side opt-out flag, push-only delivery (no inbox copy since 027) and 404/410 subscription cleanup, base64url key normalization, the single service-worker registration, and the React /profile push client (task 038). Load this whenever the task touches Web Push, VAPID keys or WebPush config, push subscription storage or the /api/push endpoints (PushEndpoints), the notification opt-in/permission UI, push delivery or fan-out from a service or background job, the service worker's push/notificationclick/pushsubscriptionchange handlers, or iOS/Android push behavior. Do not load it for service-worker caching, offline fallback, manifest/install-prompt work, or ordinary frontend changes.
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
| HTTP endpoints | `src/RTUB.Web/Endpoints/PushEndpoints.cs` (`/api/push/{status,check,subscribe,unsubscribe}`, task 038; own subscriptions only, X-CSRF-TOKEN, no-store) + `Helpers/PushSubscriptionRules.cs` (endpoint host allowlist, key lengths) |
| Browser client | `src/RTUB.Web/portal/src/push.ts` + `Profile.tsx` `PushSection` (React `/profile`, task 038) |
| SW push handlers | `src/RTUB.Web/wwwroot/service-worker.js` — the `push`, `notificationclick` and `pushsubscriptionchange` listeners |
| Opt-in UI | `/profile` → "Notificações push" (no prompt on load). Detail: `docs/react-push-v2.md` |
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
- `WebPush:Enabled` — feature flag for **non-Owner** users:   `PushEndpoints.HasAccess` (`IsInRole("Owner") || options.Enabled`), so Owners bypass the flag.
- `WebPushOptions.IsConfigured()` — all three VAPID values non-blank. Sends are skipped when false.

`IsConfigured()` only checks non-blankness. `SetVapidDetails` is called in the constructor inside a
`try/catch (ArgumentException)` that logs a warning and continues, so a **malformed** key yields
`IsConfigured() == true` with a client that can never send. If pushes silently do nothing, check for the
startup warning "Invalid VAPID configuration" before suspecting subscriptions.

## Opt-out flag (server only since 038)

`ApplicationUser.PushNotificationsOptedOut`: cleared on subscribe, set when an unsubscribe leaves the member **no**
subscription. Only the service worker's `pushsubscriptionchange` handler reads it (it does not re-create a subscription
for a member who turned push off). There is no client-side flag any more: the React client never re-subscribes
without a click, so the old `rtub-push-*` localStorage keys and the 30-minute health check are gone.

## Delivery semantics

- **Push only, no in-app copy (since 027).** Messages / Conversas was removed (`docs/messages-removal.md`):
  `SendToUserAsync`, `BroadcastAsync` and `SendToSelectedUsersAsync` no longer write the old "Sistema RTUB"
  inbox message, and `SendPushOnlyAsync` is gone. With push unconfigured a send does nothing; a member without
  push sees the notification nowhere in the app. Do not reintroduce a write into the message tables: they are
  preserved (never dropped) for Messaging v2, which will audit and evolve them; until then nothing reads or writes
  them, and `MessagesRollbackSafetyTests` fails if anything touches them.
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

## Platform notes (038)

- **Unique tag per notification**: the SW appends `-<timestamp>`; `data.baseTag` keeps the original for click routing.
- **No user-agent checks anywhere** (038 removed the iOS sniff and the Android-only `vibrate`; platform defaults apply).
- **`showNotification` inside `waitUntil`**, nothing else in the push handler (iOS kills the SW quickly).
- **Permission first in the click** (`turnOn` in `push.ts`), before any network call; iOS/iPadOS offer push only in a
  Home Screen app (the UI says so when `PushManager` is missing outside standalone).
- **`pushsubscriptionchange`**: re-sends the new subscription to `/api/push/subscribe` with `replaces` = old endpoint and
  an antiforgery token fetched in the SW; never calls unsubscribe (that used to opt the member out).
- **Single SW registration**: `wwwroot/js/sw-register.js`; the React client only adopts it.
- Real-device status: Edge desktop verified end to end locally; Android PWA / iOS Home Screen / DEV still to check.

## Auth coupling

Every push endpoint answers 401 to visitors (`RequireMember` filter) and resolves the user from
`ClaimTypes.NameIdentifier`. All client fetches use `credentials: 'include'` — including the ones inside
the service worker. Push subscription cannot work for an anonymous visitor, and any change to auth
cookie scope or SameSite affects the SW's `pushsubscriptionchange` re-subscribe.

## Tests and what they don't cover

`tests/RTUB.Application.Tests/` — `Services/PushNotificationServiceTests.cs`, `Factories/PushNotificationFactoryTests.cs`;
`RTUB.Integration.Tests/Api/PushApiTests.cs`; `RTUB.Web.Tests/ReactPortal/PushV2SourceTests.cs`; `RTUB.Core.Tests/Entities/PushSubscriptionTests.cs`.

`PushNotificationService` constructs `new WebPushClient()` internally — it is not injected and cannot be
mocked. The tests use placeholder VAPID strings, so `SetVapidDetails` throws and is swallowed: **no test
exercises an actual send, a retry, or 404/410 cleanup.** They cover subscribe/unsubscribe, opt-out,
the push-only paths and filtering only. Treat green tests as no evidence that delivery works; verify
delivery changes on a real device.

## Known debt

- `WebPush-NetCore` is obsolete; `WebPushClient` is newed up inline, blocking injection and send tests.
- No `CancellationToken` anywhere in the push API; `BroadcastAsync` fan-out is unbounded.
- No delivery metrics beyond the `ILogger` counts from `SendToSelectedUsersAsync`.

Logging convention: push logs identify users by **username/nickname**, never by user ID
(`subscription.User?.Nickname ?? subscription.User?.FirstName`, falling back to `"unknown"`) — which is
why `GetByUserIdAsync`/`GetAllActiveAsync` `.Include(s => s.User)`. Keep that when adding log lines.
