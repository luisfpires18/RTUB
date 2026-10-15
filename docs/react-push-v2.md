# Push v2: members' push notifications in React (task 038)

The member turns Web Push on and off in the React **`/profile`**, section **"Notificações push"**, separate from
"Notificações por email". The old client (Blazor-era `push-notifications.js` and `pwa-helper.js`, the Blazor
`PushNotificationPrompt` / `PushNotificationToggle`) is gone; `PushController` is replaced by minimal-API endpoints.
Sending is unchanged: the domain services and background jobs call `IPushNotificationService`, and the Owner's custom
push stays on `/notifications` (`/api/owner/notifications`, task 035). No schema change: `PushSubscriptions` and
`AspNetUsers.PushNotificationsOptedOut` are kept as they are.

## How it works

- **Capability detection only**: `'serviceWorker' in navigator`, `'PushManager' in window`, `'Notification' in window`,
  `Notification.permission`, `navigator.serviceWorker.ready`, `pushManager.getSubscription()`. No user-agent checks
  anywhere (client or worker).
- **Nothing is asked on load.** `readPush()` (portal `push.ts`) reads the server status and the browser's subscription
  and compares them; the permission prompt and `pushManager.subscribe()` happen only in `turnOn()`, from a button. The
  prompt comes first in the click, before any network request, so browsers that need the click still have it.
- **Nothing is stored in the browser.** The two sources are the server (`/api/push/status`, `/api/push/check`) and the
  browser's `PushSubscription`. The old `localStorage` flags (`rtub-push-opted-out`, `rtub-push-subscription-lost`,
  `rtub-push-prompted`) are no longer read or written.
- **One registration path**: `wwwroot/js/sw-register.js` registers the worker on every page; the React client only
  adopts it (`navigator.serviceWorker.ready`, `getRegistration()`). `ServiceWorkerReliabilityTests` counts the calls.
- Each device / browser is a separate subscription. The section says how many of the member's other devices are on,
  and offers to turn this one on or off, or all of them off.

### States shown

| State | When | Offered |
| --- | --- | --- |
| unsupported | no Service Worker / Push API / Notification | text; outside an installed app it also says that iPhone and iPad need the app added to the Home Screen |
| disabled | `WebPush:Enabled` false and not Owner | text |
| not-configured | no VAPID keys | text |
| no-worker | the worker never became ready (8 s) | "Tentar outra vez" |
| denied | notifications blocked for the site | how to allow them; "Verificar outra vez"; "Remover este dispositivo" if its old subscription is still stored |
| off | no subscription in this browser, or permission not granted (#230) | "Ativar neste dispositivo"; "Desativar em todos os dispositivos" when others are on |
| on | subscribed here, permission granted, and stored for the member | "Enviar notificação de teste"; "Desativar neste dispositivo" (+ all) |
| unregistered | subscribed here, unknown to the server | "Corrigir subscrição" (stores it) |
| stale | made with another VAPID key, or expired | "Ativar novamente" (new subscription, the old endpoint replaced) |
| error | the status could not be read | "Tentar outra vez" |

Nothing is ever repaired or re-subscribed without a click, except the browser's own endpoint rotation (below).

## API (`src/RTUB.Web/Endpoints/PushEndpoints.cs`)

Signed-in members only (401), every answer `no-store`, every POST needs `X-CSRF-TOKEN`. A member only reads, adds or
removes their **own** subscriptions.

| Route | Body | Answer |
| --- | --- | --- |
| `GET /api/push/status` | | `{enabled, configured, reason, vapidPublicKey, optedOut, subscriptions}`; `reason` is `disabled`, `not-configured` or null; only the public key, and only when available |
| `POST /api/push/check` | `{endpoint}` | `{registered}`: whether that endpoint is one of the caller's |
| `POST /api/push/subscribe` | `subscription.toJSON()` + `replaces` | the status; 403 when push is off for the caller, 409 without keys, 400 with field errors |
| `POST /api/push/unsubscribe` | `{endpoint}` or `{all: true}` | the status; works even when push is switched off |
| `POST /api/push/test` (#230) | `{endpoint}` | `{outcome, status}`: a fixed test push to the caller's own subscription with that endpoint; 403 / 409 as subscribe; 429 within 30 s of the member's last test |

- **Validation** (`PushSubscriptionRules`): the endpoint is an https URL on a browser push service (FCM
  `fcm.googleapis.com` / `android.googleapis.com`, Mozilla `push.services.mozilla.com`, Apple `push.apple.com`,
  Windows `notify.windows.com`, and their subdomains), no IP address, no user info, ≤ 2048 characters: the server
  POSTs every notification to it, so an arbitrary URL would let a member point server requests anywhere. `p256dh` is a
  65-byte uncompressed P-256 key, `auth` 16 bytes, base64url (standard base64 accepted). A browser on another push
  service is refused with a message; add its host here if one turns up.
- **`replaces`**: the same device's previous endpoint (browser rotation, or a repair) is removed in the same call, the
  caller's only.
- **Opt-out** (`PushNotificationsOptedOut`): cleared on subscribe, set when an unsubscribe leaves the member no
  subscription. Only the worker's rotation handler reads it (it does not bring a subscription back for a member who
  turned push off).
- An endpoint already stored for another account moves to the caller (the same browser, another member signing in), as
  before.

### What changed from `PushController`

- Unsubscribe deleted **any** subscription by endpoint, whoever it belonged to; now only the caller's.
- No antiforgery on the writes (only the JSON content-type gate); now `X-CSRF-TOKEN` as every React API.
- No validation of the endpoint or keys; now `PushSubscriptionRules`.
- `expirationTime` was bound as a date, so a browser's number broke binding; now Unix milliseconds.
- `POST /api/push/broadcast` (Owner) and `POST /api/push/send-to-selected` (Owner **and Admin**, although 035 made
  custom push Owner only) had no caller left: removed. Sending is `/api/owner/notifications`.
- The worker's endpoint-rotation handler called `/api/push/unsubscribe` for the old endpoint, which opted the member
  out; it now sends one `subscribe` with `replaces`.

## Service worker (`wwwroot/service-worker.js`)

- `push`: shows the notification inside `waitUntil` (nothing else); unique tag per notification (`baseTag` kept for
  click routing); Portuguese defaults. The iOS user-agent check and the Android-only `vibrate` pattern are gone: the
  platform's default notification behaviour applies (vibration follows the phone's settings).
- `notificationclick`: `notificationTarget()` opens the notification's own same-origin URL (anything else opens `/`);
  an open RTUB window is navigated (`client.navigate`, or the `rtub:navigate` message `sw-register.js` handles, also
  same-origin only) and focused, otherwise a window opens. The app-badge clearing left from Messages (027) is gone.
- `pushsubscriptionchange`: re-sends the browser's new subscription with `replaces` = the old endpoint and the
  antiforgery token; when the browser gives none, subscribes again with the server's key unless the member opted out.
  Failures are logged; `/profile` then shows the device as off. The `rtub:subscription-lost` message is gone.
- Offline, install and caching are unchanged (`CACHE_VERSION` stays `v2.8.0`).

## Test push and delivery outcomes (#230)

`/profile` → "Enviar notificação de teste" (state `on` only) posts this browser's endpoint to `/api/push/test`.
`SendTestAsync` sends only to the caller's own row with that endpoint (another member's endpoint is
`not-registered`, nothing sent), with fixed text (`CreatePushTestNotification`, opens `/profile#push`). One test per
member per 30 s (`IMemoryCache`). The answer is what the push service said, never that the device showed it:

| outcome | meaning | UI |
| --- | --- | --- |
| `sent` | push service answered 2xx | "Notificação enviada ao serviço push. …" (not "recebida") |
| `gone` | 404 / 410: row removed | ask to turn on again |
| `rejected` | other 4xx (400, 401/403 VAPID / JWT, 413) | refusal + code |
| `rate-limited`, `provider-error`, `network-error`, `failed` | 429 after retries, 5xx, unreachable, malformed request (bad VAPID) | try again later |
| `not-registered` / `not-configured` | not sent | turn on again / 409 |

Every send now returns a `PushSendResult`; only 404 / 410 delete. Failures log a warning with the notification tag,
subscription id, push service host, status and the push service's short reason (e.g. Apple `BadJwtToken`); never the
endpoint path or keys. `SendToUserAsync` / `BroadcastAsync` log one summary per fan-out (warning when anything failed,
debug otherwise). A failed delete of a gone row no longer stops the fan-out to the member's other devices.

## Tests

- `RTUB.Integration.Tests/Api/PushApiTests.cs`: visitors 401; status (public key only, the private key never, no-store,
  `disabled` / `not-configured`, Owner bypass); antiforgery; validation (http, IP, localhost, look-alike hosts, bad
  keys); storing once per endpoint, every browser service; `replaces` never touching another member; unsubscribe of one
  device / all, opt-out only when none is left, works with push off; the old controller routes are 404.
- `CookieApiCsrfTests`: the form content types still never reach `/api/push/*`; JSON with the token does.
- `IdentityCookieRefreshTests`: the role-refresh probe is now an Owner-only read (`/api/owner/db/tables`).
- `RTUB.Application.Tests/Services/PushNotificationServiceTests.cs`: `RemoveForUserAsync`, `IsRegisteredAsync`.
- `RTUB.Application.Tests/Services/PushDeliveryTests.cs` (#230): the real WebPush request (TTL, Urgency, VAPID,
  encrypted body) against a fake push service: 2xx sent, 404 / 410 removed, 400 / 401 / 403 / 413 / 5xx kept and
  logged with the reason (never keys or endpoint), network retries, test only to the caller's endpoint, fan-out survives
  a failed delete.
- `PushApiTests` (#230): `/api/push/test` 401 / token / another member's endpoint / cooldown / off / unconfigured.
- `RTUB.Web.Tests/ReactPortal/PushV2SourceTests.cs`: the profile section, every state, permission only from `turnOn`,
  capability detection, no storage, no user-agent checks, the old client gone, the worker's push / click / rotation
  contracts. `ServiceWorkerReliabilityTests`: the React client adopts the single registration.
- Owner custom push: `OwnerOperationsApiTests` (unchanged, green).

## Validation done (local, scratch data, locally generated VAPID keys)

- Playwright Chromium at 320, 375 and desktop with mocked browser push APIs over the real server: off → on → off,
  prompt refused → blocked, blocked, unsupported, unknown-to-server → "Corrigir subscrição", old key → "Ativar
  novamente", off on every device. No overflow, no console errors, no `/_blazor`.
- **Microsoft Edge (desktop, real push service, WNS)**: turned on from `/profile`, an Owner custom push from
  `/api/owner/notifications` was delivered and shown by the worker, turned off, still off after a reload.
- **Chrome desktop (real FCM, #230)**: turned on from `/profile`, "Enviar notificação de teste" → `sent`, the worker
  showed "RTUB: notificação de teste" with `/profile#push`; second click within 30 s refused.
- Not done: Android installed app, iPhone / iPad Home Screen app, and DEV with its real VAPID keys. Track them before a
  production release (see `STATE.md`).

## Follow-ups

- Real-device checks above (Android PWA, iOS Home Screen app, DEV).
- `WebPush` 1.0.13 sends the legacy `aesgcm` encoding and `Authorization: WebPush` + `Crypto-Key` (draft VAPID).
  WebKit still decrypts `aesgcm` and the library is reported working with iOS when the VAPID `sub` is a plain
  `mailto:`; if a test push to an iPhone comes back `rejected`, moving to RFC 8291 `aes128gcm` / RFC 8292 `vapid` is
  the next step. Tests inject an `HttpClient` through the internal constructor.
- No `CancellationToken` in the push API; `BroadcastAsync` fan-out is unbounded (no caller over HTTP now).
- The Play Store (TWA) app was never tested with this client.
