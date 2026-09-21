# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **023 (remove inline scripts and inline JS event handlers) - COMPLETE,
uncommitted, awaiting owner review.** Unit 022 is merged to `dev` at `0b3d5a37`.

## Branch
`fix/023/remove-inline-scripts`, branched from `dev` (clean, in sync with `origin/dev` at
`0b3d5a37`). Uncommitted - no commit authorized.
`chore/001`-`chore/011`, `fix/012`-`fix/014`, `chore/015`, `fix/016`-`fix/018`, `perf/019`,
`fix/020`-`fix/022` still present; delete when convenient.

## Owner decision (2026-09-21)
**Password-policy hardening is SKIPPED**, by instruction. Identity's password requirements were
not read for change and not touched by 021. It remains available as a future unit.

## Last completed step
**Unit 023 - every application inline `<script>` block and inline JS event-handler attribute
removed. Final counts: 0 and 0.** No CSP header enabled; that is still unit 025.

### Counts before / after
| Thing | Before 023 | After 023 |
| --- | --- | --- |
| Application inline `<script>` blocks | 3 | **0** |
| Inline JS event-handler attributes (`on*="..."`) | **15** | **0** |
| `javascript:` URLs, `setAttribute('on*')`, `el.onclick =`, `innerHTML` with `<script>` | 0 | 0 |

The audit found **15** inline handlers, not the 1 that 021 recorded. 021 only enumerated
`offline.html`. The other 14 were never listed: **13 avatar `onerror` fallbacks** across 6 files
and **1 `onclick="location.reload()"`** in `ReconnectModal.razor`. All are real `script-src`
blockers - a strict policy blocks inline handler attributes exactly as it blocks inline blocks.

Blazor `@onclick` / `@onchange` / `@oninput` were **not** touched: they compile to server-side
delegates and are never emitted as HTML attributes.

### Inline handler inventory that existed before 023
| File | Sites | What it did |
| --- | --- | --- |
| `src/RTUB.Web/Pages/Messages/Inbox.razor` | 7 | avatar `onerror` -> default avatar |
| `src/RTUB.Web/Shared/MainLayout.razor` | 1 (`:211`) | navbar avatar `onerror` |
| `src/RTUB.Shared/Components/Cards/MemberCardLite.razor` | 1 | avatar `onerror` |
| `src/RTUB.Shared/Components/Discussion/CommentComposer.razor` | 1 | avatar `onerror` |
| `src/RTUB.Shared/Components/Discussion/PostComposer.razor` | 1 | avatar `onerror` |
| `src/RTUB.Shared/Components/Profile/ProfileHeader.razor` | 1 | avatar `onerror` |
| `src/RTUB.Web/Pages/Games/Games.razor` | 1 | avatar `onerror` |
| `src/RTUB.Web/Components/ReconnectModal.razor` | 1 | `onclick="location.reload()"` |
| `src/RTUB.Web/wwwroot/offline.html` | 1 | `onclick="window.location.reload(); return false;"` |

### Service worker registration decision - the duplicate was DELETED, not moved
`App.razor:27` claimed its head registration was "required for PWABuilder detection". **The comment
did not survive reading the code.** `wwwroot/js/sw-register.js` already calls
`registerServiceWorker()` at parse time with the **same script URL and the same options**
(`{ scope: '/', updateViaCache: 'none' }`), and it is loaded by `MainLayout` on **every** page -
there is no second layout and no host `.cshtml` in the repo, so `MainLayout` is universal.

The head block was therefore a **pure duplicate**, and registering the same script under the same
scope twice is idempotent. It was **removed**, not externalized. Page-load registration is now
`sw-register.js` alone. Confirmed in the browser: the SW registers and activates normally.

`wwwroot/js/push-notifications.js:91` also calls `register()` on demand when a user opts into
push. That is a **separate, pre-existing** path, unchanged by 023 and out of its scope.

### Blazor startup externalization - ordering preserved exactly
`MainLayout.razor`'s inline tail block was split into two purpose-specific files, both referenced
at the **same position** in the document:

- `wwwroot/js/blazorStartup.js` - the `Blazor.start({...})` call, transcribed verbatim.
  `withUrl("/_blazor")`, `withServerTimeout(300000)`, `withKeepAliveInterval(15000)` are
  **unchanged**. It also carries the `ReconnectModal` reload click handler (see below).
- `wwwroot/js/navOffcanvas.js` - the offcanvas auto-dismiss listener, transcribed verbatim:
  `dropdown-toggle` exclusion, `#topNav a[href]` and `#topNav button[type="submit"]`, hide only
  when `.show`, `bootstrap.Offcanvas.getInstance`.

**Ordering is safe and unchanged.** `blazor.web.js` keeps `autostart="false"`, and both it and the
new files are **classic** (non-`defer`, non-`async`) scripts, so document order guarantees the
`Blazor` global exists before `blazorStartup.js` runs. **Not** moved to default autostart - that
would drop the SignalR circuit configuration. Exactly one file calls `Blazor.start()`; a test
pins that.

`ReconnectModal.razor`'s reload button cannot become a Blazor `@onclick`: the modal is only visible
once the circuit is **already down**, so no server-side event can be dispatched. It is a delegated
plain-JS listener on `.reconnect-reload` instead.

### Avatar fallback - one delegated listener replaced 13 attributes
`wwwroot/js/avatarFallback.js` registers a single `error` listener in the **capture** phase
(`error` does not bubble) and swaps `src` to `/images/default-avatar.webp` for any `<img>` carrying
`data-avatar-fallback`. It removes the attribute first, which is the exact equivalent of the old
`this.onerror = null` guard against a looping fallback.

It is loaded from **`<head>` in `App.razor`**, not from `MainLayout`'s script tail, so the listener
is attached before any avatar element is parsed - preserving the inline attribute's timing.

### Offline page - external script, and the precache gap that precaching alone did NOT close
`offline.html` now loads `wwwroot/js/offline.js` (status polling, `online`/`offline` listeners,
immediate check, 3s interval, 1s reload-on-restore - all verbatim, Portuguese text unchanged). Its
inline `onclick` became an `addEventListener` on `#retry` that calls `preventDefault()` +
`location.reload()`; `href="/"` is kept so the link still degrades gracefully without JS.

`/js/offline.js` was added to `STATIC_ASSETS` in `service-worker.js`. **That was not sufficient.**
The SW's script branch is stale-while-revalidate against `DYNAMIC_CACHE` **only**, and on failure
it does `.catch(() => cached)` - so for a user who had never opened `offline.html` while online,
`cached` is `undefined`, `respondWith(undefined)` throws, and the offline page would have loaded
**without its script**. Verified in the browser before fixing.

Fix, scoped to the offline path only: `.catch(() => cached || caches.match(request))`. The
cross-cache lookup reaches the precached `STATIC_ASSETS` copy. Online behaviour is untouched.

**Proven end-to-end:** with the dev server stopped and the `DYNAMIC_CACHE` entry deleted,
`fetch('/js/offline.js')` through the SW returned **200, 1318 bytes, correct content**.
`CACHE_VERSION` was **not** bumped - the SW file content changed, so a new worker installs and
`cache.addAll` writes the new entry into the same `STATIC_CACHE`, with no cache churn for users.

### `Program.cs` comment - corrected, no runtime change
`Program.cs:336` still read "RTUB still dispatches 15 `JSRuntime.InvokeAsync("eval", ...)` calls
... plus inline `<script>` blocks", which 022 and 023 both made false. It now states the real
position: script-side blockers cleared by 022 + 023, **inline styles are what remain**, CSP is
unit 024/025. Comment only - the header middleware is byte-identical.

### The 15 `eval` sites that existed before 022 (6 files)
| File | Sites (pre-022 lines) | What it did |
| --- | --- | --- |
| `src/RTUB.Shared/Components/UI/PushNotificationToggle.razor` | 6 (`:74, :118, :135, :148, :194, :230`) | fetch `/api/push/status`; build `PushNotificationsManager`; `isSubscribed`; Android+PWA probe; subscribe; unsubscribe |
| `src/RTUB.Shared/Components/UI/PushNotificationPrompt.razor` | 2 (`:83, :145`) | `Notification.permission === 'granted'`; `validateAndRefreshSubscription` |
| `src/RTUB.Shared/Components/Cards/NaipeCard.razor` | 2 (`:132, :147`) | interpolated `[data-naipe-id="{Id}"] video` -> `play()` / `pause()` |
| `src/RTUB.Web/Pages/Index.razor` | 2 (`:264, :287`) | Bootstrap carousel init; `.portal-section` scroll-reveal IntersectionObserver |
| `src/RTUB.Web/Pages/Media/Gallery.razor` | 2 (`:1411, :1419`) | interpolated `[data-gallery-id="{mediaId}"] video` -> `play()` / `pause()` |
| `src/RTUB.Web/Pages/Media/Albums.razor` | 1 (`:698`) | interpolated `getElementById('{key}').scrollIntoView(...)` |

Three of these interpolated a C# value straight into JavaScript source. That injection surface is
gone: every id is now an ordinary argument, and the Gallery selector is built inside JS through
`CSS.escape`.

### Replacement strategy - reuse first, smallest new helper second
**No parallel push architecture. No IJSObjectReference lifecycle. No ES modules, no bundler, no
package** - the codebase uses global helper namespaces loaded by `<VersionedAsset>`, and 022 stays
consistent with that.

**Push (`wwwroot/js/pwa-helper.js`, +5 functions and 1 optional parameter, 0 new files).** Two
of the six Toggle sites needed **no new implementation at all** - `pwaHelper.getPushStatus` and
`pwaHelper.initializePushManager` already existed and already did exactly that work, so the Toggle
now calls the same helpers the Prompt has always used. The other four are thin named wrappers,
each a literal transcription of the `eval` body it replaces:

| New `pwaHelper` function | Replaces | Semantics preserved |
| --- | --- | --- |
| `setPushSubscription(enable)` | Toggle subscribe **and** unsubscribe | **Rethrows** when the manager is missing, with the same message - that throw is what makes the Toggle show its error alert. Deliberately *not* `subscribeToPush`, which swallows the error and would have silently changed the UX. |
| `isSubscribedToPush()` | Toggle `:135` | `false` when no manager |
| `isAndroidPwa()` | Toggle `:148` | `/Android/i` **and** `isPwaMode()`. Not `getAndroidClientMode()`, which returns `TWA` for an Android TWA where the old expression returned `true`. |
| `isPushPermissionGranted()` | Prompt `:83` | `Notification.permission === 'granted'`, now guarded by `'Notification' in window` |
| `validateAndRefreshPushSubscription()` | Prompt `:145` | `'error'` when no manager |

`PushNotificationsManager`, `window.rtubPushManager`, the service-worker registration paths, the
subscription model and the `/api/push/*` endpoints are **untouched**. No fetch moved between C#
and JS.

**Media (`wwwroot/js/mediaPreview.js`, new, ~40 lines, `window.rtubMediaPreview`).**
`play(video)` / `pause(video)` take the element directly - `NaipeCard` already had
`@ref="videoElement"` on its `<video>`, so it passes the `ElementReference` and the CSS selector
is gone entirely. `playInGalleryCard(id)` / `pauseInGalleryCard(id)` take the id as data because
`Gallery`'s `<video>` sits inside a `@foreach` with no `@ref`; that selector is assembled **inside
JS** via `CSS.escape`. `play` returns the `play()` promise, exactly as the old `eval` expression
did, so rejection behaviour across the interop boundary is unchanged.

**Homepage (`wwwroot/js/home.js`, new, ~50 lines, `window.rtubHome`).** `initCarousel()` and
`revealSections()` are transcriptions of the two `Index.razor` blocks - same carousel options,
same `threshold: 0.05` / `rootMargin: '0px 0px 80px 0px'`, same `window.innerHeight + 80`
pre-reveal test, same `unobserve` on reveal. Nothing simplified. Not folded into `scrollSpy.js`:
the carousel is not scroll behaviour, and `rtubScrollSpy` stays what it is.

**Scroll (`wwwroot/js/scrollHelper.js`, new, 12 lines, `window.rtubScroll`).** `toElement(id)` is
`getElementById(id)?.scrollIntoView({behavior:'smooth', block:'start'})` - same behaviour, same
block. Deliberately **not** `rtubScrollSpy.scrollToSection`, which is landing-page specific (it
adds `in-view` and offsets by `.portal-sticky-nav`) and would have changed what Albums does.

All three new files are plain hand-written `wwwroot/js`, registered in
`src/RTUB.Web/Shared/MainLayout.razor` next to `scrollSpy.js`. **No inline `<script>` added.**

### Security - no `eval` substitute introduced
The changed code was grepped for `new Function`, `setTimeout("string")`, `setInterval("string")`,
`innerHTML`, `document.write`, `javascript:` URLs and script-element injection: **zero hits**.
Removing the C# interpolation also removed three string-injection points.

### Behaviour delta - found in review, then removed
The first cut of 022 had `PushNotificationToggle` call `pwaHelper.getPushStatus` bare. That helper
also reconciles the local `rtub-push-opted-out` cache (`markOptedOut` / `clearOptedOut`) from the
server's `isOptedOut` - **a side effect the Toggle's `eval` fetch never had.** Owner review caught
it. Fixed, without duplicating the fetch:

```js
getPushStatus: async function(syncOptOut = true) { ... if (syncOptOut && data && ...) { ... } }
```

- `PushNotificationToggle` calls `getPushStatus(false)` - a **read-only** status fetch. Rendering
  the settings toggle no longer touches the local opt-out flag, exactly as before 022.
- `PushNotificationPrompt` and every other caller pass **no argument**, so JS applies the
  `= true` default and their behaviour is byte-identical to pre-022. `PushNotificationPrompt.razor`
  was not edited for this fix and `PushNotificationPromptTests` still passes unchanged (12 tests,
  2 pre-existing skips).
- Pinned by `BootsThrough_NamedHelpers`, which now asserts the Toggle's `getPushStatus` invocation
  carries the single argument `false`.

**022 therefore has no behavioural delta.** Everything else - gating, ordering, error text, success
text, icons, the Android hint - is unchanged. The Toggle's private `PushStatusDto` was deleted in
favour of the existing public `RTUB.Application.DTOs.PushStatusDto` the Prompt already uses, so
the manual `System.Text.Json` round-trip is gone too.

### CSP inventory carried forward from 021 (still the plan for 025)
**Blocker 1 - `eval`: CLEARED by 022.** `'unsafe-eval'` is no longer needed.

**Blocker 2 - inline `<script>` and inline `on*` handlers: CLEARED by 023.** All 3 blocks and all
15 handler attributes are gone, so **`script-src` no longer needs `'unsafe-inline'`**. No nonce and
no hash were needed - every block became an external file, which sidesteps the hard part entirely
(a nonce would have had to reach both the root document and `HeadOutlet` from the request).
**`script-src` is now clean: `'self'` plus the three pinned CDN origins below.**

**Blocker 3 - inline styles. THE ONLY REMAINING SCRIPT-OR-STYLE BLOCKER.** **11 `<style>` blocks** in production `.razor` files plus
`offline.html`, and **213 `style="..."` attributes across 29 `.razor` files**. Both forms are
covered by `style-src`. (Bootstrap's *runtime* CSSOM writes - `el.style.x = ...` for offcanvas and
modal transforms - are **not** CSP-governed and are not a blocker.) **Unit 024.**

**Blocker 4 - the R2 origin is runtime configuration, not a constant.** `img-src`, `media-src` and
`frame-src` all need the Cloudflare R2 public origin, which comes from `Cloudflare:R2:PublicUrl`
(e.g. `https://pub-xxx.r2.dev`) and is **stored absolute in the database**. The policy string has
to be built from `IConfiguration` at startup, not written as a literal.

**Not blockers - the external origins, verified in 021 by reading what the browser loads:**
- `script-src`: `https://cdnjs.cloudflare.com` (cropper.js), `https://unpkg.com` (leaflet, SRI
  pinned), `https://cdn.jsdelivr.net` (pixi.js)
- `style-src`: `https://cdnjs.cloudflare.com` (cropper.css), `https://unpkg.com` (leaflet.css,
  SRI pinned)
- `img-src`: `'self' data:` plus `https://*.basemaps.cartocdn.com`
  (`wwwroot/js/memberMap.js:72` tile layer) plus the R2 origin
- `connect-src`: `'self'` - the `_blazor` WebSocket is **same-origin**, and CSP3 `'self'` matches
  `wss:` to the same host. The service worker fetches **nothing** cross-origin.
- `frame-src`: the R2 origin only (the two PDF viewers)
- `font-src`: `'self'` - `https://fonts.googleapis.com` is a **dead** `preconnect`/`dns-prefetch`
  in `App.razor:24-25` with no matching stylesheet and no `@font-face`. Do not add it to a policy;
  delete the hint instead (carried in *Deferred*).
- YouTube / Spotify / Instagram / Facebook appear only as `<a href target="_blank">`
  **navigations**, never embeds - so they need **no** directive.

The four security headers shipped by 021 (`X-Content-Type-Options: nosniff`,
`Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY`,
`Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=()`) and the HSTS decision
(already live at `max-age=2592000`; no `includeSubDomains`, no `preload`, because RTUB does not own
the `azurewebsites.net` apex) are unchanged by 022.

**`Program.cs` comment: CORRECTED by 023** (022 deliberately left it stale). It now reads that
022 + 023 cleared the script-side blockers and that inline styles are what remain. No runtime
change - the security-header middleware is byte-identical and still emits **no** CSP.

## Deployment requirement — `AdminUser__Password` on a fresh database (unit 016)

**No immediate Azure action is required for this deploy.** Production `rtub` has an existing
database with users, so `InitializeAsync` returns before the bootstrap check is ever evaluated.
Deploying unit 016 to current production changes nothing at startup, and the existing Owner
account is unaffected — its password is whatever it was set to, not the removed default.

**Any fresh/empty database will now refuse to start without it.** Before the future Azure **dev**
environment (or any new App Service, container, or local database created from scratch) is first
started, `AdminUser__Password` must be supplied as an App Service application setting or
equivalent secure configuration source, with a real value — not a placeholder. Without it,
seeding throws and no privileged account is created. Like
`ASPNETCORE_FORWARDEDHEADERS_ENABLED` below, this is portal/CLI configuration, is **not in the
repo**, is not applied by a code redeploy, and nothing in CI will warn if it is missing.

**`SeedData__MemberPassword`** is needed only by a developer who flips `isEmptyDb` to `false` for a
full local seed. It is never required in Azure unless that switch is used there.

## Deployment requirement — Azure forwarded headers (CONFIRMED 2026-09-21, no longer blocking)

**Resolved.** `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` **is set** on App Service `rtub`
(resource group `rtub_group`), verified by the owner via Azure CLI on **2026-09-21**. Unit 014's
login rate limiter therefore partitions on the real client address in production, as designed.

**Production `rtub` is Azure App Service on Linux.** `curl -I https://rtub.azurewebsites.net/health`
returns `Server: Kestrel` — no IIS layer, so there is no `UseIISIntegration` auto-wiring of
forwarded headers. The setting above is Microsoft's documented switch for App Service Linux /
containers: the host wires `ForwardedHeadersMiddleware` itself, ahead of the app pipeline, with
cloud-appropriate settings. Nothing is in RTUB's own code for it — deliberately: no
`UseForwardedHeaders` call, no `ForwardedHeadersOptions`, no clearing of
`KnownProxies`/`KnownNetworks`, and no manual `X-Forwarded-For` parsing.

It is portal/CLI configuration and is **not in the repo** — `.github/workflows/ci.yml` only
publishes and deploys, and sets no app settings. So it is not reproduced by a redeploy of code
alone, and **a new App Service (including the future Azure dev environment) needs it set again.**
Nothing in CI will warn if it is missing.

What `RemoteIpAddress` is, per environment:
- **Local / `dotnet run`** — the real client address; correct with no extra configuration.
- **Integration tests (`TestServer`)** — `null` for every request (no transport), so all callers
  share the `"unknown"` partition. `RemoteIpTestStartupFilter` (test host only) works around this
  by setting the address from an `X-Test-Remote-Ip` header; see *Previous validation (unit 014)*.
- **Azure App Service (current prod, Linux/Kestrel)** — **the real client address**, as of the
  2026-09-21 confirmation above.
- **Future Azure dev environment** — platform front end until the same setting is applied there.

### Consequence now live — `UseHttpsRedirection`
Turning the setting on makes `Request.IsHttps` true behind the proxy. The skip of
`UseHttpsRedirection` outside Development at `Program.cs:325` (comment: "HTTPS is handled at the
load balancer level") was the workaround for exactly the case that no longer applies. The skip is
now redundant rather than load-bearing. Still **not changed** — it is its own decision, and
removing it is a behavior change to the production request pipeline. Carried in *Deferred* below.

## Current task
None active. Unit 023 is complete and awaiting owner review.

## Next unit
**024 - inline-style CSP cleanup.** Inline styles are now the **only** remaining script-or-style
CSP blocker. Scope: **12 `<style>` blocks** in production `.razor`/`.html` and **213
`style="..."` attributes across 29 `.razor` files** - both covered by `style-src`. Bootstrap's
runtime CSSOM writes (`el.style.x = ...` for offcanvas/modal transforms) are **not** CSP-governed
and are **not** a blocker. Note `sw-register.js` builds its update toast with `innerHTML` + inline
`style="..."` and injects a `<style>` element; it executes no inline JS (not a `script-src`
problem) but its styles are a `style-src` one, so 024 owns it.

Then **025**: enable CSP itself, built from the directive inventory above, with the R2 origin read
from `Cloudflare:R2:PublicUrl`, and a browser smoke run.

Also still available, deliberately not taken: **password-policy review / hardening** (Identity is
`RequiredLength = 4` with every complexity rule off, `AddIdentityServices`,
`ServiceCollectionExtensions.cs`) - the owner skipped it in 021.
Still queued, not security: **Microsoft 10.0.11 -> 10.0.12 servicing train** across `src/` + tests,
which also unblocks `MockQueryable.Moq 10.0.12`.

## Blockers
**None.**

**Owner action, not a blocker:** the historical GitGuardian incidents stay historical. The
literals remain in old commits, and unit 015 deliberately did **not** rewrite git history to clear
them. Mark those incidents "false positive / test credential" in GitGuardian by hand. 015 only
stops *future* commits from raising new ones. No GitGuardian ignore comment was added either.

## Deferred / owner decisions

### Security — raised by 015, deliberately not changed
- ~~**Two hardcoded production password defaults in `src/`**~~ — **fixed by unit 016.** Both now
  fail closed; see *Last completed step* above.
- ~~**`ResetDevDataAsync` runs in EVERY environment except Production**~~ - **fixed by unit 017.**
  Now opt-in (`DevelopmentDataReset:Enabled`), local-Development-only, disabled by default, no
  hardcoded hash, validated before any mutation. Staging and Production can no longer run it at
  all. See *Last completed step*.
- ~~"the bulk member seed is dead code"~~ — **that earlier claim was wrong and is retracted.** It
  is an intentional manual full-seed developer path (`isEmptyDb`), now documented in place and
  covered by tests. Nothing to clean up.
- **Old commits still contain the removed literals.** History was not rewritten, by instruction.
- The `src/` scan was run for completeness only; **no production file was touched by 015** and
  production `appsettings*.json` contain no credential keys at all.

### Security — raised by 014, deliberately not changed
- **`RateLimiterOptions.OnRejected` is global, not per-policy.** Today only the `login` policy
  exists, so the Portuguese rejection body can only be reached by a login rejection. **A second
  policy added later must either share that message or move the callback onto its own policy.**
- **A 429 on a browser form POST renders as a bare text page**, not the styled `/login?error=...`
  page the endpoint's other failures use. Redirecting instead would mean answering 302, which
  contradicts the required 429. Left as-is; a status-code page for 429 is a UX decision.
- **Existing login tests share the `"unknown"` partition.** `AuthAntiforgeryTests` (5 login POSTs)
  and `CookieApiCsrfTests` (4) are under the limit of 10 and each class gets its own factory, so
  they are safe today. Adding a sixth login POST to one of those classes would start hitting 429 —
  use the `X-Test-Remote-Ip` header for a distinct partition if that happens.
- **`UseHttpsRedirection` is skipped outside Development** (`Program.cs:325`). **Now actionable:**
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is confirmed on as of 2026-09-21, so `Request.IsHttps` is
  true behind the proxy and the skip no longer works around anything. Removing it is a production
  request-pipeline behavior change, so it is its own unit, not a ride-along.

### Security — reviewed by 013, deliberately not changed
- **`AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN")`** is still configured and still unused by
  every endpoint and client. 013 confirmed no endpoint needs it. Harmless; drop it, or keep it as
  the hook for a future header-token client.
- **`POST /api/push/broadcast` and `POST /api/push/send-to-selected` have no browser caller.**
  Dead HTTP surface, but role-gated and not CSRF-reachable — deletion is cleanup, not security.
- **`Set-Cookie` carries no `Secure` flag under the test host**, because the default is
  `CookieSecurePolicy.SameAsRequest` and the test client speaks http. Over https in production the
  flag is emitted. Not changed; an explicit `Always` is a separate hardening decision.
- ~~**Pre-existing integration-test flake, not caused by 013**~~ — **fixed by unit 018.** The
  shared `SqliteConnection` is gone and the seed now runs in `IHostedLifecycleService.StartingAsync`.
  Measured before/after in the same harness: **12 failures in 40 isolated runs → 0 in 40.** See
  *Last completed step*.
- ~~Out of scope by instruction and untouched: ... cookie validation / SQLite pressure ...~~ -
  **done by unit 019** (Option A, 5-minute write throttle). Still out of scope and untouched:
  password policy, MFA, security headers / CSP, PWA cache strategy, push architecture refactor,
  `Program.cs` cleanup.

### Raised by 019, deliberately not changed
- **`LastLoginDate` is named "last login" but means "last authenticated activity".** Unit 019 kept
  the name by owner decision. Renaming it (say to `LastActivityAt`) needs a migration plus an edit
  to `LoginStatusBadge`, `AvatarCard`, `UserCard`, `UserRoles`, `UserProfileService`,
  `LoginStatisticsButton` and `AuditLogAppender`. Its own unit if wanted.
- ~~**RTUB's `OnValidatePrincipal` replaces Identity's `SecurityStampValidator` entirely**~~ -
  **fixed by unit 020.** The handler now calls the framework validator after its own checks. The
  stricter per-request stamp check was kept, `ValidationInterval` stays at the framework default,
  and the principal refresh / `ShouldRenew` behaviour is back. See *Last completed step*.
- **The `SQLITE_LOCKED` retry loop was kept**, now correctly commented. The `IsInRoleAsync` pair is
  still 2 SELECTs and could be one join - not done, out of 019's scope.
- Known and accepted framework limitation (from 012): antiforgery tokens are bound to the user
  identity, so multiple tabs signed in as different users are unsupported. Documented ASP.NET Core
  behavior, not an RTUB defect.

### Raised by 020, deliberately not changed
- **`UserRoles.razor:613` and `RoleManagementService.cs:121` call `UpdateSecurityStampAsync` on a
  role change**, commented "force fresh cookies and token refresh". That is not what it does in
  stock Identity either: bumping the stamp makes `VerifySecurityStamp` fail, so it is a **forced
  logout**, not a claims refresh. With 020's composition a plain role change would now propagate
  to a live session within `ValidationInterval` without logging anyone out. Deciding whether
  either call site should drop the stamp bump is a UX/security decision, not a ride-along.
- **`ValidationInterval` is still the framework default (30 min) and still unconfigured.** Nothing
  in the repo sets `SecurityStampValidatorOptions`. Lowering it would only speed up claim refresh -
  every security check already runs per request - at 2 extra SELECTs per user per interval.
- **The `IsInRoleAsync` pair is still 2 SELECTs** and could be one join. Out of 020's scope, as it
  was out of 019's; the framework composition did not eliminate it.

### Raised by 021, deliberately not changed
- **CSP is not enabled.** Of the four blockers enumerated in *Last completed step*, the first -
  the 15 `eval` interop sites - is **cleared by 022**. Three remain: 3 inline `<script>` blocks
  (023), 11 `<style>` blocks + 213 `style=` attributes (024), and the R2 origin only being known
  at runtime (025). No `Content-Security-Policy-Report-Only` was shipped either - a report-only
  policy is worth adding once the blockers are down and it can report something actionable, not
  while it would report every page load.
- **`Cross-Origin-Opener-Policy` / `Cross-Origin-Embedder-Policy` / `Cross-Origin-Resource-Policy`
  were not added.** Outside the brief, and COOP in particular needs its own check of the
  `LoginPopup` flow and anything relying on `window.opener` before it can be called safe. Cheap to
  add later; not free to add blind.
- **`X-XSS-Protection` was not added.** It is removed from Chrome and Edge, ignored by Firefox,
  and its legacy filter mode was itself an exploitable primitive. Adding it is pure theatre.
- **`https://fonts.googleapis.com` is a dead `preconnect` + `dns-prefetch`** in `App.razor:24-25`:
  no stylesheet link, no `@font-face`, nothing loads from it. Two wasted connection hints. Delete
  them in whichever frontend unit is next in that file - not worth a unit of their own, and 021
  had no reason to touch `App.razor`.
- **`UseHttpsRedirection` is still skipped outside Development** (`Program.cs`). Unchanged for the
  third unit running; see the 014 entry above. Now that forwarded headers are confirmed on, this
  is a one-line change gated only on someone accepting a production pipeline behaviour change.

### Carried forward (unchanged)
- **`xUnit1051` suppressed, not adopted** (1634 sites). Its own unit if wanted: mechanical, but it
  touches nearly every test file, so it must not ride along with anything else.
- **`Microsoft.Testing.Extensions.Telemetry/2.4.0`** arrives transitively via `xunit.v3` and
  reports usage metrics to Microsoft. Opt out with `TESTINGPLATFORM_TELEMETRY_OPTOUT=1`.
- **`global.json` raises the SDK floor to 10.0.100** for running tests. No pin added.
- Running a test `.exe` directly gives xUnit's native console runner, not the MTP CLI, because
  `UseMicrosoftTestingPlatformRunner` was not set. `dotnet test` is unaffected.
- **Remaining dependency findings:**
  - Microsoft 10.0.11 → **10.0.12** train — one servicing bump across src + tests. Not started.
  - `MockQueryable.Moq 10.0.12` — held at 10.0.8; gated on the EF Core 10.0.12 bump above.
  - `AWSSDK.Core 4.0.3.8` → 4.0.102.6 and `AWSSDK.S3 4.0.10` → 4.0.103.3 — independent train.
  - `QuestPDF 2024.10.3` → 2026.9.0 — major train, likely breaking. Not started.
  - `AWSSDK.Core` is referenced directly by `RTUB.Integration.Tests`, `RTUB.Shared.Tests` and
    `RTUB.Web.Tests` but **used by none of them**. Three dead direct references — AWS train.
  - `Portable.BouncyCastle 1.9.0` — transitive via `WebPush 1.0.13`. Legacy id, not deprecated,
    no advisories. Note it if an advisory lands.
- **Test-suite hygiene:** `RTUB.Web.Tests` skips **56 of 791** tests (mostly `*PageTests` modal /
  button cases). Pre-existing.
- **Push subsystem debt:** `WebPushClient` is still newed up inside `PushNotificationService`, so
  it cannot be mocked and no test covers an actual send, a retry, or 404/410 cleanup. Also: two
  service-worker registration paths, unbounded `BroadcastAsync` fan-out, no `CancellationToken`,
  no delivery metrics.
### Raised by 022, deliberately not changed
- **`src/RTUB.Web/wwwroot/js/rtub.carousel.js` is dead.** A minified Bootstrap-Carousel fallback
  shim that self-registers on `DOMContentLoaded` and immediately bails when `bootstrap.Carousel`
  exists. It is referenced by **no** `<VersionedAsset>`, no `<script>` and no interop call, so it
  is never loaded at all. Delete it, or wire it up deliberately - 022 left it exactly as it was.
- **`PushNotificationsManager` is still constructed inside `pwaHelper.initializePushManager` and
  parked on `window.rtubPushManager`.** 022 kept that arrangement on purpose (it is the push
  architecture, not the eval cleanup). The push-modernization unit owns it.

- **Phase 1C:** remaining optional custom skills — deliberately not created.
- Work-branch cleanup (`chore/001`–`chore/011`) — delete when convenient.
- Two `graphifyy 0.9.56 + MCP` installs (isolated venv, Microsoft-Store Python user site). The
  Store one wins PATH and works; both are compatible, so neither needs removing.
- Pending feature work — unchanged, not part of any phase.

### Raised by 023, deliberately not changed
- **Two service-worker registration paths still exist.** 023 removed the `App.razor` duplicate, so
  page-load registration is `wwwroot/js/sw-register.js` alone - but
  `wwwroot/js/push-notifications.js:91` still calls `navigator.serviceWorker.register()` on demand
  during push opt-in, with **different options** (no `scope`, no `updateViaCache`). Pre-existing,
  out of scope for an inline-script unit, and already flagged by the `rtub-push` skill. The push
  unit owns consolidating it.
- **`sw-register.js` builds its update toast with `innerHTML` + inline `style="..."` and injects a
  `<style>` element.** It executes **no** inline JS, so it is not a `script-src` problem and 023
  correctly left it alone - but those styles are a `style-src` problem. **Unit 024 owns it.**
- **`offline.html` still has a large inline `<style>` block.** Same reason: `style-src`, not
  `script-src`. Unit 024.
- **`App.razor:23-24` still has the dead `fonts.googleapis.com` `preconnect` / `dns-prefetch`.**
  Carried over from 021's inventory; no matching stylesheet and no `@font-face` anywhere. 023 was
  editing adjacent lines but did not take it - out of scope. Delete it in 024 or 025.
- **`rtub.carousel.js` is still dead** (raised by 022, unchanged).

## Relevant files (unit 023)
**New JS (4 files, all plain `wwwroot/js`, no bundler, no package):**
- `src/RTUB.Web/wwwroot/js/avatarFallback.js` (26 lines) - delegated capture-phase `error`
  listener for `[data-avatar-fallback]`. Loaded from `<head>` in `App.razor`.
- `src/RTUB.Web/wwwroot/js/blazorStartup.js` (34 lines) - `Blazor.start({...})` verbatim, plus the
  `.reconnect-reload` delegated click handler.
- `src/RTUB.Web/wwwroot/js/navOffcanvas.js` (24 lines) - offcanvas auto-dismiss, verbatim.
- `src/RTUB.Web/wwwroot/js/offline.js` (40 lines) - offline-page status logic verbatim, plus the
  `#retry` click listener.

**Edited:**
- `src/RTUB.Web/App.razor` - head inline `<script>` **deleted** (duplicate SW registration);
  `avatarFallback.js` added via `<VersionedAsset>`.
- `src/RTUB.Web/Shared/MainLayout.razor` - tail inline `<script>` replaced by two
  `<VersionedAsset>` tags at the same position; 1 avatar `onerror` -> `data-avatar-fallback`.
- `src/RTUB.Web/Components/ReconnectModal.razor` - `onclick="location.reload()"` removed.
- `src/RTUB.Web/wwwroot/offline.html` - inline `<script>` -> `<script src="/js/offline.js">`;
  inline `onclick` -> `id="retry"`.
- `src/RTUB.Web/wwwroot/service-worker.js` - `/js/offline.js` added to `STATIC_ASSETS`; script
  branch's failure path widened to `cached || caches.match(request)`.
- `src/RTUB.Web/Program.cs` - CSP comment corrected. **No runtime change.**
- 12 avatar `onerror` -> `data-avatar-fallback` in `Inbox.razor` (7), `MemberCardLite.razor`,
  `CommentComposer.razor`, `PostComposer.razor`, `ProfileHeader.razor`, `Games.razor`.

## Tests (1 new file, +10; 0 existing test files touched)
`tests/RTUB.Web.Tests/Security/InlineScriptPolicyTests.cs` - static source scans, matching the
existing `PortalContentStyleTests` `GetProjectRoot()` pattern. **10 tests.**

The two repository-wide scans are **aggregated `[Fact]`s, not `[Theory]` sweeps.** An earlier cut
parameterized them per markup file and contributed **+377** cases that said nothing individually;
collapsed to 2 facts at owner request. **Coverage is unchanged** - both still walk every
`.razor` / `.cshtml` / `.html` under `src/` except `wwwroot/lib/`:

- `ApplicationMarkup_HasNoInlineExecutableScripts`
- `ApplicationMarkup_HasNoInlineJavaScriptEventHandlers`

Each collects **all** violations (no early exit) and fails once with `relative/path:line  snippet`
per hit. Matching runs against whole file content, not line by line, because an opening tag can
span lines (MainLayout's leaflet tag does); the line number is derived from the match offset for
reporting only.

The 8 focused architecture facts that materially pin behaviour are retained:
`blazor.web.js` keeps `autostart="false"`; `blazorStartup.js` is referenced **after**
`blazor.web.js`; exactly one JS file calls `Blazor.start(`; `offline.html` references
`/js/offline.js` and has no inline script or handler; the SW precaches `/js/offline.js`; the SW
script branch's offline fallback still reads `cached || caches.match(request)` (the cross-cache
lookup 023 added); `avatarFallback.js` is referenced **inside `<head>`** and the
`data-avatar-fallback` hook is in use; `App.razor` no longer registers the service worker.

**Not vacuous - verified by injection.** A temporary probe file with `onclick=`, `onerror=`, an
external `<script src>` and an inline `<script>` was dropped under `wwwroot/` and the scans
reported **exactly** the 3 real violations with correct line numbers, ignoring the external tag.
Probe deleted.

**Regex note (a real bug caught while writing these):** the handler regex is deliberately
**case-sensitive and lowercase-only**. With `RegexOptions.IgnoreCase` it matched Blazor component
parameters like `OnClose="..."` and produced **71 false failures**. HTML attributes in this repo
are lowercase; Blazor parameters are PascalCase. The `(?<![@\w-])` lookbehind keeps `@onclick` out.

## Latest validation (unit 023)
- `dotnet build RTUB.sln -c Release` - **succeeded, 0 warnings, 0 errors.**
- `dotnet test --solution RTUB.sln -c Release --no-build` -
  **total 4631 / passed 4571 / failed 0 / skipped 60.**
- **Test delta accounted for exactly:** baseline on `dev` before 023 was 4561 passed / 0 failed /
  60 skipped (4621 total). 4571 - 4561 = **+10**, which is precisely the collapsed
  `InlineScriptPolicyTests` class. Skipped count unchanged at 60. No existing test changed state.
- `node --check` clean on all 4 new JS files **and** on `service-worker.js`.
- `git diff --check` clean. Diff is **43 insertions / 90 deletions across 12 files** - no
  line-ending churn (`.gitattributes` has `* text=auto`, so the index normalizes to LF).
- **No migrations** added or touched.
- **Credential scan clean** on the diff and on all new files: no password, key, token, VAPID,
  bearer or connection-string literals.

## Static scan (unit 023) - the acceptance gate
Run over all tracked `.razor` / `.cshtml` / `.html` under `src/`, excluding `wwwroot/lib/`:

| Pattern | Hits |
| --- | --- |
| `<script>` / `<script type=...>` without `src` | **0** |
| any `on*="..."` (`onclick` `onload` `onerror` `onchange` `onsubmit` `oninput` `onkey*` `onmouse*` `onfocus` `onblur`) | **0** |
| `javascript:` URLs | 0 |
| `setAttribute("onclick"` / `setAttribute('onclick'` | 0 |
| `element.onclick =` | 0 |
| `innerHTML` containing `<script` | 0 |
| C#/TS source emitting inline handlers | 0 |

Every surviving `<script>` tag is external: `blazor.web.js`, the 3 pinned CDN origins
(cdnjs/unpkg/jsdelivr), `<VersionedAsset>`-emitted `/js/*.js`, and `offline.html`'s
`/js/offline.js`.

**Confirmed against the live server, not just source:** the served HTML for `/` contains **0**
inline `<script>` and **0** inline `on*` attributes, and the live DOM after Blazor render reports
`inlineScriptCount: 0`, `inlineHandlerCount: 0`.

## Browser validation (unit 023) - RUN
Local `dotnet run` on `http://localhost:58870`, built-in browser pane.

- **`/`** - loads, Blazor circuit connects (`WebSocket connected to ws://localhost:58870/_blazor`).
  **No duplicate-`Blazor.start` error.** Service worker registers and activates.
- **`/login`** - loads, no new console errors.
- **`/music`** (Blazor-routed navigation) - interactive navigation works.
- **Script ordering verified in served markup:** `blazor.web.js` (`autostart="false"`) at line 312,
  `blazorStartup.js` at 347, `navOffcanvas.js` at 348. All 4 new files return **200**.
- **Mobile offcanvas (375x812):** opened `#topNav`, clicked a `#topNav a[href]` -> offcanvas
  closed (`show` removed) and Blazor navigated to `/music`. **No exception thrown.**
- **Avatar fallback:** a broken `<img data-avatar-fallback>` was swapped to
  `/images/default-avatar.webp` and had its attribute removed (loop guard); a broken `<img>`
  **without** the attribute was left untouched.
- **ReconnectModal:** `.reconnect-reload` has **no** inline `onclick`, and clicking it reloads the
  page - the delegated handler is wired.
- **Offline page:** external script runs (status text transitions `A verificar ligação...` ->
  `Ligação restaurada! A recarregar...`); simulating `offline` gives `Ainda offline`; the `#retry`
  click is `defaultPrevented` by the new listener and `getAttribute('onclick')` is `null`.
- **Offline availability proven with the server stopped:** `/js/offline.js` precached in
  `rtub-static-rtub-v2.6.0` alongside `/offline.html`, and `fetch('/js/offline.js')` through the SW
  returned **200 / 1318 bytes / correct content** with the `DYNAMIC_CACHE` entry deleted.
- **Console:** only pre-existing `401`s from `pwa-helper.js` polling `/api/push/status` while
  anonymous (reproduced with `curl`: `/api/push/status -> 401`), plus `404`s from the broken test
  images deliberately injected above. **No JS errors from any 023 change.**
- Viewport reset to desktop; dev server stopped; no SW/cache state left in an odd shape.
- *(Not covered: a signed-in session - no dev credentials were available and none were entered.
  The authenticated-only behaviour that 023 actually changed is the avatar fallback, which was
  exercised directly in the live DOM instead, and by the static tests.)*

## Blockers found by 023
**None outstanding.** One was found and fixed inside the unit: precaching `/js/offline.js` into
`STATIC_ASSETS` was **not** enough, because the SW's script branch only consulted `DYNAMIC_CACHE`
(see *Offline page* above). Fixed and proven with the server stopped.

**Remaining CSP blockers after 023:** inline **styles** only - 12 `<style>` blocks and 213
`style="..."` attributes. `script-src` is clear.
