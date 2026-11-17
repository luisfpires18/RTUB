# Push: members' push notifications in React

The member turns Web Push on and off in the React `/profile`, section "Notificações push", separate from "Notificações
por email". Sending: domain services and background jobs call `IPushNotificationService`; the Owner's custom push is on
`/notifications` (`/api/owner/notifications`). What is sent and where a tap lands: [push-notifications](push-notifications.md).
Server delivery internals (VAPID, retries, inbox copy, key encoding): `rtub-push` skill.

Tables: `PushSubscriptions`, `PushPreferences`, `PendingActionDismissals`; user flag `AspNetUsers.PushNotificationsOptedOut`.

## How it works

- Capability detection only: `'serviceWorker' in navigator`, `'PushManager' in window`, `'Notification' in window`,
  `Notification.permission`, `navigator.serviceWorker.ready`, `pushManager.getSubscription()`. No user-agent checks,
  client or worker.
- Nothing is asked on load. `readPush()` (portal `push.ts`) reads the server status and the browser's subscription and
  compares them. The permission prompt and `pushManager.subscribe()` happen only in `turnOn()`, from a button, and the
  prompt comes first in the click, before any network request, so browsers that need the click still have it.
- Nothing is stored in the browser. The sources are the server (`/api/push/status`, `/api/push/check`) and the
  browser's `PushSubscription`.
- One registration path: `wwwroot/js/sw-register.js` registers the worker on every page; the React client only adopts
  it (`navigator.serviceWorker.ready`, `getRegistration()`). `ServiceWorkerReliabilityTests` counts the calls.
- Each device/browser is a separate subscription. The section says how many of the member's other devices are on and
  offers to turn this one on or off, or all of them off.
- Nothing is repaired or re-subscribed without a click, except the browser's own endpoint rotation.

### States shown

| State | When | Offered |
| --- | --- | --- |
| unsupported | no Service Worker / Push API / Notification | text; outside an installed app it also says iPhone and iPad need the app added to the Home Screen |
| disabled | `WebPush:Enabled` false and not Owner | text |
| not-configured | no VAPID keys | text |
| no-worker | the worker never became ready (8 s) | "Tentar outra vez" |
| denied | notifications blocked for the site | how to allow them; "Verificar outra vez"; "Remover este dispositivo" if its old subscription is still stored |
| off | no subscription in this browser, or permission not granted | "Ativar neste dispositivo"; "Desativar em todos os dispositivos" when others are on |
| on | subscribed here, permission granted, stored for the member | "Enviar notificação de teste"; "Desativar neste dispositivo" (+ all) |
| unregistered | subscribed here, unknown to the server | "Corrigir subscrição" (stores it) |
| stale | made with another VAPID key, or expired | "Ativar novamente" (new subscription, old endpoint replaced) |
| error | the status could not be read | "Tentar outra vez" |

## API (`src/RTUB.Web/Endpoints/PushEndpoints.cs`)

Signed-in members only (401), every answer `no-store`, every write needs `X-CSRF-TOKEN`. A member reads, adds and
removes only their own subscriptions.

| Route | Body | Answer |
| --- | --- | --- |
| `GET /api/push/status` | | `{enabled, configured, reason, vapidPublicKey, optedOut, subscriptions}`; `reason` is `disabled`, `not-configured` or null; only the public key, and only when available |
| `POST /api/push/check` | `{endpoint}` | `{registered}`: whether that endpoint is one of the caller's |
| `POST /api/push/subscribe` | `subscription.toJSON()` + `replaces` | the status; 403 when push is off for the caller, 409 without keys, 400 with field errors |
| `POST /api/push/unsubscribe` | `{endpoint}` or `{all: true}` | the status; works even when push is switched off |
| `POST /api/push/test` | `{endpoint}` | `{outcome, status}`; 403 / 409 as subscribe; 429 within 30 s of the member's last test |
| `GET /api/push/preferences` | | `{items: [{key, group, label, help, mandatory, enabled}]}`: the whole catalogue with the caller's choices |
| `PUT /api/push/preferences` | `{disabled: string[]}` | the same `items` after the save; 400 with field `disabled` for the mandatory or an unknown key (nothing stored) |
| `GET /api/push/reminder` | | `{reformed, acknowledged}` (startup reminder) |
| `POST /api/push/reminder/ack` | | 204, idempotent across tabs |

- Validation (`PushSubscriptionRules`): the endpoint is an https URL on a browser push service (FCM
  `fcm.googleapis.com` / `android.googleapis.com`, Mozilla `push.services.mozilla.com`, Apple `push.apple.com`, Windows
  `notify.windows.com`, and their subdomains), no IP address, no user info, ≤ 2048 characters: the server POSTs every
  notification to it, so an arbitrary URL would let a member point server requests anywhere. `p256dh` is a 65-byte
  uncompressed P-256 key, `auth` 16 bytes, base64url (standard base64 accepted). A browser on another push service is
  refused with a message; add its host here if one turns up.
- `replaces`: the same device's previous endpoint (browser rotation, or a repair) is removed in the same call, the
  caller's only.
- An endpoint already stored for another account moves to the caller (same browser, another member signing in).
- Opt-out (`PushNotificationsOptedOut`): cleared on subscribe, set when an unsubscribe leaves the member no
  subscription. Only the worker's rotation handler reads it, so it does not bring a subscription back for a member who
  turned push off.
- The old `PushController` routes (`/api/push/broadcast`, `/api/push/send-to-selected`) no longer exist (404); sending
  is `/api/owner/notifications`.

## Service worker (`wwwroot/service-worker.js`)

- `push`: shows the notification inside `waitUntil` and nothing else; unique tag per notification (`baseTag` kept for
  click routing); Portuguese defaults. Vibration and other platform behavior follow the platform's defaults.
- `notificationclick`: `notificationTarget()` opens the notification's own same-origin URL (anything else opens `/`);
  an open RTUB window is navigated (`client.navigate`, or the `rtub:navigate` message `sw-register.js` handles, also
  same-origin only) and focused, otherwise a window opens. Target persistence for installed apps: rule 3 in
  [push-notifications](push-notifications.md).
- `pushsubscriptionchange`: re-sends the browser's new subscription to `/api/push/subscribe` with `replaces` = the old
  endpoint and an antiforgery token fetched in the worker; never calls unsubscribe (that would opt the member out).
  When the browser gives no new subscription, it subscribes again with the server's key unless the member opted out.
  Failures are logged; `/profile` then shows the device as off.

## Test push and delivery outcomes

`/profile` → "Enviar notificação de teste" (state `on` only) posts this browser's endpoint to `/api/push/test`.
`SendTestAsync` sends only to the caller's own row with that endpoint (another member's endpoint is `not-registered`,
nothing sent), with fixed text (`CreatePushTestNotification`, opens `/profile#push`). One test per member per 30 s
(`IMemoryCache`). The answer is what the push service said, never that the device showed it:

| outcome | meaning | UI |
| --- | --- | --- |
| `sent` | push service answered 2xx | "Notificação enviada ao serviço push. …" (not "recebida") |
| `gone` | 404 / 410: row removed | ask to turn on again |
| `rejected` | other 4xx (400, 401/403 VAPID / JWT, 413) | refusal + code |
| `rate-limited`, `provider-error`, `network-error`, `failed` | 429 after retries, 5xx, unreachable, malformed request (bad VAPID) | try again later |
| `not-registered` / `not-configured` | not sent | turn on again / 409 |

Every send returns a `PushSendResult`; only 404 / 410 delete. Failures log a warning with the notification tag,
subscription id, push service host, status and the push service's short reason (e.g. Apple `BadJwtToken`); never the
endpoint path or keys. `SendToUserAsync` / `BroadcastAsync` log one summary per fan-out (warning when anything failed,
debug otherwise). A failed delete of a gone row does not stop the fan-out to the member's other devices.

## Startup reminder

"Ativa as notificações" opens once per app opening for a signed-in member whose push is off on this device.

- Opening = a new tab or window, or a cold launch of the installed app. Each portal page is its own document (no client
  router), so the launch flag lives in `sessionStorage` (`rtub-startup-done`): moving between pages, coming back to the
  app, polls and re-renders never show it again in that launch. A confirmed visitor page (sign-out, or `/login` before
  signing in or switching account) clears the flag, so the next signed-in page starts a new launch.
- Who: visitors never. By `readPush()` state: `off`, `stale`, `unregistered` (subscribed elsewhere only counts as off
  here) and `denied` remind. `unsupported` shows the iPhone/iPad install steps only in Safari on iPhone/iPad
  (`navigator.standalone` exists) and general "use an up-to-date browser or another device" text elsewhere; inside the
  installed app nothing shows. `on`, push disabled / not configured, no worker and any read error do not (unknown is
  never "off"). Rules in `portal/src/startupRules.ts`.
- Active vs Reformado (`ApplicationUser.IsRetired`): active = every opening while off ("Agora não" is for that opening
  only). Reformado = once per account across devices: closing or enabling a shown reminder stores
  `POST /api/push/reminder/ack` (a `PendingActionDismissals` row, kind `startup.push`, no source or period). A failed
  save is not reported as saved; the reminder shows again on a later opening. A member back to active gets the active
  rule whatever was stored.
- The browser permission is asked only by the "Ativar notificações" click (`turnOn`, as `/profile`). Denied shows how to
  allow it in the site/app settings; the native prompt is never repeated by RTUB.
- Queue (`portal/src/Startup.tsx`, `StartupModals`): one dialog at a time, after the page settles (1.5 s). A
  `MutationObserver` watches for any other open `<dialog>` (a deep-link dialog, the system announcement, even one that
  opens later): the reminder is hidden meanwhile (not closed, nothing stored) and returns once it closes, so two modals
  never stack and the URL is never changed. Order: release notes ([release-and-rollback](release-and-rollback.md) →
  *Release notes*), then this reminder. The system announcement keeps its own path.
- The reminder reads the device state and the Reformado acknowledgement, never a preference.
- Checks: `npm run check:startup`, `PushApiTests.Reminder_*`, `StartupReminderPortalTests`.

## Preferences

`/profile` → Notificações push → "Escolher o que receber" opens `PushPreferences.tsx` (the shared `Dialog`): one
checkbox per catalogue key, grouped (Geral, Atuações, Ensaios, Reuniões, Tuna), with the key's help under it
(schedules such as "Segunda-feira, uma vez por semana"). "Lembrete: As minhas ações pendentes" is checked, disabled and
marked Obrigatório. Guardar sends the full set of keys that are off; the answer re-renders the list, so two tabs or
devices end on what the server has (last write wins). Loading, failure ("Tentar outra vez") and save errors show in the
dialog; the device state is not re-read, because the save never touches a subscription.

- The server is the source: `PushPreferenceCatalog` (keys, labels, groups, mandatory flag), `PushPreferenceService`
  (validates and stores `PushPreferences`), `PushNotificationService` (applies it to every send). The client holds no
  label and no policy (`PushV2SourceTests`). Keys and policy: [push-notifications](push-notifications.md) → *Preference keys*.
- Independent of the device: turning a key off never unsubscribes a device and never sets `PushNotificationsOptedOut`;
  turning a device off keeps the account's choices. A key on still needs this device's push on, OS permission granted
  and a stored subscription to show anything; the dialog says so.
- `pending-actions.daily` cannot be turned off through the API (400), has no row, and is still sent only when the page
  has something pending.
- `PushPreferenceService`: a rival tab writing the same row first is still a success; any other write failure throws, so
  the dialog never says "guardado" over the old choices.

## Tests

`PushApiTests` (API, auth, CSRF, validation, test push, preferences, reminder), `CookieApiCsrfTests`,
`PushDeliveryTests` (real WebPush request against a fake push service), `PushNotificationServiceTests`,
`PushPreferenceFilterTests`, `PushPreferenceServiceTests`, `PushV2SourceTests` (profile section, states, permission only
from `turnOn`, capability detection, no storage, no user-agent checks, worker contracts), `ServiceWorkerReliabilityTests`.

## Follow-ups

- Real-device checks: Android installed app, iPhone/iPad Home Screen app, DEV with its real VAPID keys; the Play Store
  (TWA) app was never tested with this client.
- `WebPush` 1.0.13 sends the legacy `aesgcm` encoding and `Authorization: WebPush` + `Crypto-Key` (draft VAPID).
  WebKit still decrypts `aesgcm` and the library is reported working with iOS when the VAPID `sub` is a plain `mailto:`;
  if a test push to an iPhone comes back `rejected`, move to RFC 8291 `aes128gcm` / RFC 8292 `vapid`. Tests inject an
  `HttpClient` through the internal constructor.
- No `CancellationToken` in the push API; `BroadcastAsync` fan-out is unbounded.
