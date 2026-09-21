# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **022 (remove all eval-based JS interop) - COMPLETE, uncommitted, awaiting
owner review.** Unit 021 is merged to `dev` at `e03f4a5d`.

## Branch
`fix/022/remove-eval-interop`, branched from `dev` (clean, in sync with `origin/dev` at
`e03f4a5d`). Uncommitted - no commit authorized.
`chore/001`-`chore/011`, `fix/012`-`fix/014`, `chore/015`, `fix/016`-`fix/018`, `perf/019`,
`fix/020`, `fix/021` still present; delete when convenient.

## Owner decision (2026-09-21)
**Password-policy hardening is SKIPPED**, by instruction. Identity's password requirements were
not read for change and not touched by 021. It remains available as a future unit.

## Last completed step
**Unit 022 - every `eval` JS interop call removed. Final count: 0.**
C# now passes DATA to named JS functions; it never builds JavaScript source.

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

**Blocker 2 - inline `<script>`, 3 production blocks.** `src/RTUB.Web/App.razor:27` (service-worker
registration, must stay in `<head>` for PWABuilder detection),
`src/RTUB.Web/Shared/MainLayout.razor` (`Blazor.start({...})` with the SignalR circuit config plus
the offcanvas auto-dismiss handler), `src/RTUB.Web/wwwroot/offline.html:77`. Each needs a nonce or
a hash. A nonce is the harder one: `App.razor` is the root document and `MainLayout` feeds
`HeadOutlet`, so the nonce has to reach both from the request. **This is unit 023.**

**Blocker 3 - inline styles.** **11 `<style>` blocks** in production `.razor` files plus
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

**`Program.cs` is NOT touched by 022.** An earlier cut of this unit refreshed the CSP explanatory
comment there; owner review ruled it out of scope and the file was reverted to its merged 021
state. Consequence to know about: that comment at `Program.cs:337` now **understates** the
position - it still reads "RTUB still dispatches 15 `JSRuntime.InvokeAsync("eval", ...)` calls",
which 022 made false. It is a comment only, with no runtime effect. **Unit 023 owns that comment**
- it edits the same inline-`<script>` blocks the comment describes, so it can correct both in one
place.

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
None active. Unit 022 is complete and awaiting owner review.

## Next unit
**023 - inline `<script>` removal / nonce-or-hash for the 3 remaining blocks.** With `eval` gone,
`'unsafe-inline'` on `script-src` is the last thing standing between RTUB and a real CSP. Scope:
`src/RTUB.Web/App.razor:27` (service-worker registration - must stay in `<head>` for PWABuilder
detection), `src/RTUB.Web/Shared/MainLayout.razor` (`Blazor.start({...})` circuit config plus the
offcanvas auto-dismiss handler) and `src/RTUB.Web/wwwroot/offline.html:77`. Pick nonce or hash per
block; the nonce path has to reach both the root document and `HeadOutlet` from the request.
Still **no CSP header in 023**.

Then, in order: **024** inline styles (11 `<style>` blocks + 213 `style=` attributes); **025**
enable CSP itself, built from the directive inventory above, with the R2 origin read from
`Cloudflare:R2:PublicUrl`, and a browser smoke run.

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
## Relevant files (unit 022)
Production changed (7):
- `src/RTUB.Shared/Components/UI/PushNotificationToggle.razor` - 6 `eval` sites -> 6 named
  `pwaHelper.*` calls; private `PushStatusDto` deleted; `@using RTUB.Application.DTOs` added.
- `src/RTUB.Shared/Components/UI/PushNotificationPrompt.razor` - 2 `eval` sites -> 2 named calls.
  Nothing else in the component touched.
- `src/RTUB.Shared/Components/Cards/NaipeCard.razor` - 2 sites -> `rtubMediaPreview.play/pause`
  with the existing `videoElement` `ElementReference`.
- `src/RTUB.Web/Pages/Index.razor` - 2 sites -> `rtubHome.initCarousel` / `rtubHome.revealSections`.
  The `rtubScrollSpy.init` call between them is untouched.
- `src/RTUB.Web/Pages/Media/Gallery.razor` - 2 sites -> `rtubMediaPreview.*InGalleryCard(mediaId)`.
- `src/RTUB.Web/Pages/Media/Albums.razor` - 1 site -> `rtubScroll.toElement(key)`.
- `src/RTUB.Web/Shared/MainLayout.razor` - 3 `<VersionedAsset>` lines added next to `scrollSpy.js`.

JS changed (1) / new (3):
- `src/RTUB.Web/wwwroot/js/pwa-helper.js` - **+5 functions**, inserted before
  `getAndroidClientMode`, plus **one optional parameter** on the existing `getPushStatus`
  (`syncOptOut = true`), which leaves every pre-existing caller on its current behaviour.
  Nothing else edited, nothing removed.
- `src/RTUB.Web/wwwroot/js/mediaPreview.js` - **new**, `window.rtubMediaPreview`.
- `src/RTUB.Web/wwwroot/js/home.js` - **new**, `window.rtubHome`.
- `src/RTUB.Web/wwwroot/js/scrollHelper.js` - **new**, `window.rtubScroll`.

Read and deliberately **not** changed: `wwwroot/js/push-notifications.js` (the whole
`PushNotificationsManager` class), `wwwroot/js/sw-register.js`, `wwwroot/service-worker.js`,
`wwwroot/js/scrollSpy.js`, `wwwroot/js/scrollToTop.js`, `wwwroot/js/modalHelper.js`,
`wwwroot/js/rtub.carousel.js` (an unreferenced Bootstrap fallback shim - left alone, recorded in
*Deferred*), `App.razor` and `MainLayout.razor`'s inline `<script>` blocks (unit 023),
`src/RTUB.Application/DTOs/PushStatusDto.cs`, `PushController`, and - after the review revert -
**`src/RTUB.Web/Program.cs`**, which carries no 022 change at all.

## Tests (1 file rewritten: 8 -> 15, net +7; 0 other test files touched)
**`tests/RTUB.Shared.Tests/Components/UI/PushNotificationToggleTests.cs` - rewritten.** The
`EvalScriptContains` helper and every `JSInterop.Setup<T>("eval", ...)` are **gone**; no test
asserts generated JavaScript source anywhere in the repo any more. Setups now name the real
helpers (`pwaHelper.getPushStatus`, `initializePushManager`, `isSubscribedToPush`, `isAndroidPwa`,
`setPushSubscription`) and assert on argument values.
The 8 original behaviours are all still covered; 7 tests are new:
1. `StaysGraceful_WhenPushNotConfigured` - unconfigured server: no toggle, no error, and
   `initializePushManager` is never called.
2. `BootsThrough_NamedHelpers` - the boot sequence uses the three named identifiers, `"eval"` is
   absent, and `getPushStatus` is called with the single argument **`false`** (`syncOptOut`), which
   pins the read-only status fetch described above.
3. `SkipsSubscriptionCheck_WhenInitializeFails` - no manager means `isSubscribedToPush` is never
   invoked.
4. `ShowsAndroidHint_WhenSubscribedOnAndroidPwa` - the hint comes from the named probe.
5. `HidesAndroidHint_WhenNotAndroidPwa` - the negative case.
6. `Unsubscribes_WithFalseArgument` - same helper, `false` as the data value.
7. `NeverDispatchesGeneratedScript` - drives the whole subscribe flow, then asserts every
   identifier is a `pwaHelper.*` function and no string argument looks like JS source. This is the
   test that keeps `eval` from coming back.
`Subscribes_WithTrueArgument` replaces the old timing-dependent `ShowsProcessingState_WhenToggling`
with an assertion that actually pins the contract (`true` -> subscribe).
`PushNotificationPromptTests` was read and **not changed**: it already mocked only named
`pwaHelper.*` identifiers, and neither of the Prompt's two replaced calls is exercised by it (they
sit on the PWA recovery path, which its default `isPwaMode -> false` skips). Its 2 pre-existing
skips are unrelated and untouched.

No credential literal. No `#nullable disable` needed after the rewrite.

## Latest validation (unit 022)
- Release build, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true` and
  `EnforceCodeStyleInBuild=true`.
- Focused first: `PushNotificationToggleTests` - **15 total, 15 passed**;
  `PushNotificationPromptTests` - 12 total, 0 failed, 2 pre-existing skips, **not edited**. Then
  the whole `RTUB.Shared.Tests` project - 768 total, 0 failed, 2 skipped.
  Re-run unchanged after the review fixes: the `getPushStatus(false)` change **modified** one
  existing assertion and **added no test**, so every count below is identical before and after.
- Full suite: **4561 passed, 0 failed, 60 skipped** (total 4621). Against the post-021 baseline of
  **4554 passed / 60 skipped**, the branch is **+7 passed, +7 total, skips unchanged** - exactly
  the 7 new tests listed above. Per project: Core 791, Application 1997, Shared 768, Web 791,
  Integration 274.
  **Note on the runner:** `dotnet test` (MTP driver) reports *"Zero tests ran"* for **all five**
  projects on this machine, including ones 022 never touched - an environment/driver problem, not
  a regression. The numbers above come from running each `tests/<project>/bin/Release/net10.0/
  <project>.exe` directly, which gives xUnit v3's native console runner. Worth knowing before
  someone debugs a phantom failure.
- **Static scan - the acceptance criterion.** Grepping the literal `"eval"` (not `eval(`, which
  never matched these) across `src/` and `tests/`, excluding `obj/`, `bin/` and `node_modules/`:
  **two hits, neither a dispatch** - the (021-authored, 022-untouched) explanatory comment in
  `Program.cs:337` and the negative assertion in `PushNotificationToggleTests`.
  `Invoke(Void)?Async[^;]*"eval"` returns **zero**.
  `Setup...("eval"` in `tests/` returns **zero**. Legitimate English matches (`EvaluateRetirement
  StatusAsync`, `evaluatorResult`, ...) were separated out and left alone, as required.
  **Production JSInterop identifiers equal to `"eval"`: 0.**
- **Frontend build: correctly not run.** `src/RTUB.Web/package.json` drives **only** the three
  PixiJS bundles (`npm run build:pixi`, `pixi/vite.config.ts`, fired on `BeforePublish`). The four
  files 022 touches are plain hand-written `wwwroot/js` with no bundling step, so the right
  validation is a JS parse - `node --check` passes on all four.
- `git diff --check`: clean. Diff credential scan (including the 3 new files): no match.
- **No migration and no model-snapshot change.** No entity, column or `DbContext` edit.
- **No Graphify rebuild** - no application structure changed; only interop call targets moved.

## Browser validation (unit 022) - RUN, unlike 021
Release build served at `http://localhost:5199` (Development env, local `app.db`, backed up first).
- **Homepage loads, no JS console errors.** The only console errors on the whole run are
  service-worker registration failures - **pre-existing and environmental**: `/service-worker.js`
  itself serves `200 text/javascript`, and no SW file appears in the 022 diff. Zero
  `is not a function`, zero `ReferenceError`, zero JSInterop identifier errors.
- All four helper namespaces resolve at runtime: `rtubHome.initCarousel`, `rtubHome.revealSections`,
  `rtubScroll.toElement`, `rtubMediaPreview.{play,pause,playInGalleryCard,pauseInGalleryCard}` and
  the 5 new `pwaHelper.*` - all `typeof === "function"`. All 4 JS files serve `200`.
- **Carousel:** `#homeCarousel` present with exactly 1 active item after `initCarousel`.
- **Scroll-reveal:** 6 `.portal-section`s, 1 `in-view` at the top of the page, **2 after
  scrolling** - the IntersectionObserver is live, so `revealSections` reproduced the old behaviour
  rather than just marking what was already visible.
- **Albums section scroll:** `rtubScroll.toElement('section-history')` moved the page 0 -> 2902 and
  left the target 64px from the top (`block:'start'` under the sticky nav). A missing id is a
  silent no-op. *(Caveat: this exercised the helper directly on the homepage sections; the
  anonymous `/music` page renders one section and no mobile nav, and the authed mobile-nav path
  needs a login, which was not performed.)*
- **Gallery / Naipe video preview:** the live `/gallery` currently holds 9 image cards and 0
  videos, so hovering could not reach the handler. Instead both code paths were driven directly
  against instrumented `<video>` elements: `playInGalleryCard(41)`/`(42)` resolved to the correct
  per-card video and nothing else, and the direct-element path (what `NaipeCard` passes as an
  `ElementReference`) reached the same element. A **missing id, a `null` element and a hostile id
  (`1"] video, [x="`) all no-op without throwing** - the `CSS.escape` selector holds.
- **Push - read-only probes only. No permission was requested, no subscription created or
  destroyed, no manager instantiated.** `Notification.permission` was `denied` before and after.
  `getPushStatus()` -> `null` (401 for anonymous, handled gracefully - that 401 is the only other
  console error and it was self-inflicted by the probe); `isSubscribedToPush()` -> `false`;
  `validateAndRefreshPushSubscription()` -> `'error'`; `isPushPermissionGranted()` -> `false`;
  `isAndroidPwa()` -> `false`; `setPushSubscription(true)` **threw** the same message the old
  `eval` threw - which is exactly what drives the Toggle's error alert. The unsupported /
  not-configured path stays graceful.
  *(Limitation: the Toggle and the Prompt's recovery branch need an authenticated session and a
  working service worker; neither was available, and no credentials were entered. Those paths are
  covered by the 15 bUnit tests instead.)*
- No dev server, viewport override or launch config left behind; the local `app.db` was backed up
  before the run and the app only did its normal idempotent startup seeding.
- **Not re-run after the review fixes**, by instruction: the only runtime change since the smoke
  run is the `getPushStatus(syncOptOut = true)` default parameter and the Toggle passing `false`.
  `node --check` passes on `pwa-helper.js`, existing callers are untouched by JS default-parameter
  semantics (Blazor sends no argument, so `undefined` selects the default), and the Toggle's
  argument is pinned by `BootsThrough_NamedHelpers`.
