# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **026 (PWA / service-worker reliability) - COMPLETE, uncommitted, awaiting
owner review.** Unit 025 is merged to `dev` at `b8798b79`; the CSP / security-header track is
closed. 026 is a reliability unit, not a redesign - the PWA architecture is unchanged.

## Branch
`fix/026/pwa-service-worker-reliability`, branched from `dev` (clean, in sync with `origin/dev`
at `b8798b79`). Uncommitted - no commit authorized.
`chore/001`-`chore/011`, `fix/012`-`fix/014`, `chore/015`, `fix/016`-`fix/018`, `perf/019`,
`fix/020`-`fix/025` still present; delete when convenient.

## Owner decision (2026-09-21)
**Password-policy hardening is SKIPPED**, by instruction. Identity's password requirements were
not read for change and not touched by 021 or 025. It remains available as a future unit.

## Last completed step
**Unit 026 - the PWA's four reliability defects are fixed: the iPhone bottom-nav drift, the
duplicate service-worker registration, private HTML in the cache, and an update prompt the user
could never actually reach. Plus E, the offline-page correctness defect 026's own offline
validation turned up: `offline.js` bounced off the offline page whenever the device had a
network, whether or not RTUB was reachable.**

### A. MobileBottomNav drift - ROOT CAUSE FOUND AND FIXED
`2-layout/navbar.css` reintroduced the exact rule `1-base/mobile.css` exists to prevent:

    /* 1-base/mobile.css, @media (max-width: 768px) */
    html, body { overflow-x: clip; }          <- deliberate, with a comment saying why

    /* 2-layout/navbar.css, @media (display-mode: standalone) and (max-width: 991.98px) */
    html, body { overflow-x: hidden; }        <- silently won

Same selector, same specificity (0,0,2); media queries contribute none. `site.css` imports
`1-base/mobile.css` at line 18 and `2-layout/navbar.css` at line 24, so **source order decided it
and navbar.css won**. `overflow-x: hidden` on html/body makes iOS Safari treat the viewport root
as the scroll container, and a `position: fixed; bottom: 0` descendant then drifts off the bottom
edge - which is the reported bug.

**Why it was never caught before:** the override is gated on `(display-mode: standalone)`. It does
not apply in a browser tab at any width. It applies **only in the installed PWA** - exactly where
the owner saw it, on Albums (`/music`).

**Fix:** one declaration, `hidden` -> `clip`, in that navbar.css block, plus a comment naming the
cascade trap. The `.navbar` / `.offcanvas` `overflow-x: hidden` rules in the same file are left
alone - they are not ancestors of MobileBottomNav and do not affect the viewport root. **No
JavaScript repositioning was added, and no `transform: translateZ(0)`** - a transform would itself
create a containing block for fixed descendants, which is the failure mode, not the fix.

**Also audited and cleared** (none can misplace the nav): `body.modal-open` and
`body:has(.rtub-messages)` set `position: fixed`, which is *not* a containing-block trigger for
fixed descendants; `.no-scroll` likewise; `modalHelper.js` only toggles `modal-open`; no
`transform` / `filter` / `contain` / `will-change` / `perspective` on any layout ancestor;
`.pwa-mode .navbar { overflow-x: hidden }` is scoped to the navbar, not the root; the nav's own
`env(safe-area-inset-bottom)` padding is intact. Only **two** `html, body` overflow-x rules exist
in the whole stylesheet set, and both are now `clip`.

### B. One service-worker registration owner
Before: **two** `navigator.serviceWorker.register` calls - `sw-register.js` (scope `/`,
`updateViaCache: 'none'`) and `push-notifications.js` (no options at all). `sw-register.js` also
*called its own registration function twice* (immediately, then again on `load`), attaching a
second `updatefound` listener, a second `visibilitychange` listener and a second update-check
timer chain - the mechanism behind a duplicate update toast.

After: **exactly one** registration call in application source. `PushNotificationsManager` adopts
it through `navigator.serviceWorker.ready`, bounded by a 10s timeout so `initialize()` cannot hang
if nothing ever registers. Its public behaviour is unchanged: still sets `this.registration` /
`this.subscription`, still returns the registration, still throws on failure, and `ready` still
guarantees an **active** worker, which is the iOS Safari precondition for
`pushManager.subscribe()`. Push architecture untouched. `registerServiceWorker()` in
`sw-register.js` is now single-shot.

### C. Cache safety - the private-HTML leak, fixed
The old fetch handler's document branch matched `!url.pathname.includes('.')`, so **every
extension-less path fell into it** and every 200 response was written to `DYNAMIC_CACHE`.

Measured live, anonymous, after browsing four pages and fetching the excluded paths:

| Cache | v2.6.0 (before) | v2.7.0 (after) |
| --- | --- | --- |
| `rtub-static` | 10 entries, **2 HTML**: `/` and `/offline.html` | 9 entries, **1 HTML**: `/offline.html` |
| `rtub-dynamic` | 127 entries, **4 application HTML** (`/music`, `/roles`, `/calotes`, `/`) and **`/health`** | 122 entries, **0 HTML, 0 excluded paths** |
| `rtub-images` | 5 entries | 5 entries |

For a signed-in user those four documents are rendered *authenticated* HTML sitting on disk,
servable offline or after logout. `/api/push/status`, `/auth/login` and `/_blazor/negotiate`
escaped caching in that run only because they answered 401/405/405 - status, not policy.

Now:
- `NEVER_CACHE_PREFIXES = ['/api/', '/auth/', '/_blazor', '/hubs/', '/health']`, checked by
  `isNeverCached()` **before any cache branch**, together with a non-GET guard. Each prefix maps to
  a real route (`MapPost /auth/login`, `MapPost /auth/logout`, `MapHub /hubs/messages`,
  `MapHealthChecks /health`, the `/api` controllers).
- Documents / navigations are **network-only**: the response is returned straight through and
  never written to a cache. On network failure the fallback is `offline.html`, with a plain 503 as
  a last resort. The old `caches.match('/offline.html') || caches.match('/')` was dead code anyway
  - `caches.match` returns a Promise, which is always truthy.
- `'/'` removed from `STATIC_ASSETS`: it is user-specific HTML.
- Both guards are scoped to same-origin, so R2 and CDN caching is **unchanged** (cdnjs / unpkg /
  jsdelivr still cached, as before). No caching optimisation was attempted.

### D. Update lifecycle
`install` called `self.skipWaiting()` **unconditionally**. Every new worker therefore activated at
once, `clients.claim()` took control, `controllerchange` fired and `sw-register.js` reloaded the
page. The "Nova versão disponível / Atualizar" prompt was effectively unreachable, and
`SKIP_WAITING` was not in fact user-gated.

Fixed: `install` no longer forces activation. The worker waits; the **only** activation trigger is
the `SKIP_WAITING` message posted by the "Atualizar" click. The user-controlled prompt is
preserved, not replaced. No new worker is forced to activate immediately.

`controllerchange` also fired on a **first-ever** install, where `clients.claim()` takes control of
a page that was never controlled - a pointless extra reload. It is now guarded by
`hadControllerAtStartup`, alongside the existing `refreshing` guard. One reload path, two guards,
no loop. Periodic update checks (30s after load, hourly while visible, on `visibilitychange`,
5-minute debounce) are sensible and were left alone.

### E. Offline page bounced off itself - FIXED with an origin-reachability probe
`offline.js` treated `navigator.onLine === true` as proof that RTUB was reachable. It is not: the
flag reports only whether the device has *a* network interface up, never whether *this origin*
answers.

The failure, reproduced end to end:

| Step | What happened |
| --- | --- |
| 1 | Origin stopped; device still on a network |
| 2 | Service worker correctly served `offline.html` from cache |
| 3 | `navigator.onLine` stayed **true** |
| 4 | `offline.js` waited ~1s and navigated to `/` |
| 5 | `/` failed, the worker re-served `offline.html`, and it bounced again |

**The fix probes the origin instead of the device.** `/health` is the probe, which is appropriate
precisely because 026 had already made it network-only:

1. `navigator.onLine === false` -> show the offline state immediately. No probe, no redirect -
   the request could not succeed anyway.
2. `navigator.onLine === true` -> `fetch('/health', { cache: 'no-store' })`. Only
   `response.ok` counts as reachable. `no-store` keeps the browser's HTTP cache out of it;
   `NEVER_CACHE_PREFIXES` already keeps the service worker's caches out of it. **No service-worker
   caching change was made, and `/health` stays network-only.**
3. Rejection, abort, 4xx or 5xx -> stay on `offline.html` and show the offline state. **No
   redirect.**
4. Reachable -> the accepted behaviour is unchanged: "Ligação restaurada! A recarregar...", then
   `/` after the same ~1s delay.

**Robustness, kept small.** Three callers can fire a probe - the initial check, the `online`
event, and the existing 3s interval - so a single `probeInFlight` flag suppresses overlap, and a
5s `AbortController` timeout stops a black-holed connection leaving the page waiting for ever.
No dependency was added.

**No redirect loop is possible from this code.** A `redirecting` flag makes the navigation
single-shot and clears the interval at the same moment, and the navigation is now reachable *only*
through a proven-reachable origin - which is the precise condition the old code got wrong. A
cross-page-load bounce counter was considered and deliberately not built: it would guard a state
that cannot persist (a `/health` that answers while `/` fails at the network layer), and that
extra state is likelier to misfire than the scenario is to occur.

**Preserved unchanged:** the `online` and `offline` listeners, the "Tentar Novamente" button
(still `location.reload()`), the 3s periodic retry, the ~1s redirect delay, and all three
Portuguese strings. `offline.html` itself was not touched. The other 026 work - the
MobileBottomNav fix, the single-registration work, the cache policy and the update lifecycle - is
byte-identical.

### Cache version: bumped once, v2.6.0 -> v2.7.0, with a reason
Not mechanical. Installed clients are holding cached application HTML and a cached `/health`
written by v2.6.0. The `activate` handler deletes every `rtub-` cache outside the current set, so
**the bump is the mechanism that purges those entries**. Without it the fix would stop new leaks
but leave the existing ones on disk.

### CSP constraint: honoured, unchanged
No CSP change was needed or made. `/service-worker.js` still carries **no**
`Content-Security-Policy` header, pinned by `NonDocumentResponse_CarriesNoContentSecurityPolicy`.

## Relevant files (unit 026)
| File | Change |
| --- | --- |
| `src/RTUB.Web/wwwroot/css/2-layout/navbar.css` | `overflow-x: hidden` -> `clip` on `html, body` in the standalone block (+ comment). The whole bottom-nav fix. |
| `src/RTUB.Web/wwwroot/service-worker.js` | `NEVER_CACHE_PREFIXES` + `isNeverCached()`; non-GET and never-cache guards before any cache branch; document branch is network-only with an `offline.html` fallback; `'/'` dropped from `STATIC_ASSETS`; no `skipWaiting()` on install; `CACHE_VERSION` v2.7.0. |
| `src/RTUB.Web/wwwroot/js/sw-register.js` | single-shot `registerServiceWorker()`; `controllerchange` reload guarded by `hadControllerAtStartup`. |
| `src/RTUB.Web/wwwroot/js/push-notifications.js` | stops registering; adopts the existing registration via `navigator.serviceWorker.ready` with a 10s bound. |
| `src/RTUB.Web/wwwroot/js/offline.js` | `navigator.onLine` -> a `/health` reachability probe (`cache: 'no-store'`, `AbortController` timeout, single-flight, single-shot redirect). Section E. |
| `tests/RTUB.Web.Tests/Pwa/ServiceWorkerReliabilityTests.cs` | **new**, 22 tests. |
| `tests/RTUB.Web.Tests/Pwa/OfflineReachabilityTests.cs` | **new**, 8 tests. |
| `tests/RTUB.Web.Tests/Security/InlineScriptPolicyTests.cs` | doc comment only - it said push-notifications.js registering was out of scope; it no longer registers. |

Not touched: `offline.html`, `offline.css`, the manifest, the push handlers, the CSP builder,
`Program.cs`, and - for the section E follow-up - `service-worker.js`, `sw-register.js`,
`push-notifications.js` and `navbar.css`. No migrations.

## Tests (unit 026) - 2 new files, +30
`tests/RTUB.Web.Tests/Pwa/ServiceWorkerReliabilityTests.cs`. Source-level scans, because the
behaviour lives in a service worker with no origin, no DOM and no test host. Compact, not
parameterised into hundreds of cases.

| # | Test | Pins |
| --- | --- | --- |
| 1 | `ApplicationSource_ContainsExactlyOneServiceWorkerRegistration` | exactly one register call, and it is `sw-register.js` |
| 2 | `PushNotificationsManager_AdoptsTheExistingRegistrationInsteadOfRegistering` | uses `serviceWorker.ready`, never registers |
| 3 | `MainLayout_LoadsTheRegistrationOwner` | `ready` can actually resolve |
| 4-8 | `ServiceWorker_DeclaresPathAsNeverCached` (Theory x5) | `/api/`, `/auth/`, `/_blazor`, `/hubs/`, `/health` |
| 9 | `ServiceWorker_AppliesTheNeverCacheGuardBeforeAnyCacheBranch` | the guard runs *before* any `caches.` use |
| 10 | `ServiceWorker_PassesNonGetRequestsStraightToTheNetwork` | non-GET bypasses cache |
| 11 | `ServiceWorker_DoesNotPersistApplicationHtml` | no `cache.put` / `caches.open` in the document branch |
| 12 | `ServiceWorker_DoesNotPrecacheTheApplicationRoot` | `'/'` not in `STATIC_ASSETS` |
| 13 | `ServiceWorker_FallsBackToOfflinePageForFailedNavigations` | `offline.html` is the document fallback |
| 14-16 | `ServiceWorker_PrecachesOfflineAsset` (Theory x3) | `offline.html`, `offline.js`, `offline.css` reachable from cache |
| 17 | `ServiceWorker_SkipsWaitingOnlyOnUserRequest` | no `skipWaiting` in `install`; only the message handler activates |
| 18 | `SwRegister_PostsSkipWaitingOnlyFromTheUpdateButton` | one `SKIP_WAITING`, from the "Atualizar" click |
| 19 | `SwRegister_HasOneGuardedReloadPath` | one `location.reload`, both guards present |
| 20 | `SwRegister_WiresTheUpdateLifecycleOnlyOnce` | single-shot registration |
| 21 | `MobileBottomNav_KeepsFixedBottomAndSafeAreaContract` | `position: fixed`, `bottom/left/right: 0`, `env(safe-area-inset-bottom)` |
| 22 | `NoStylesheet_SetsOverflowHiddenOnTheViewportRoot` | **the 026 regression guard** - no stylesheet may set `overflow-x: hidden` on `html`/`body` again, in any media query |

Test 22 was **negative-controlled**: reverting the one navbar.css declaration to `hidden` makes it
fail naming that exact file; restoring `clip` makes it pass. It catches the real regression, not a
proxy for it.

The `/service-worker.js` CSP-header contract is not duplicated here - it stays pinned over the
wire by `RTUB.Integration.Tests.SecurityHeaderTests.NonDocumentResponse_CarriesNoContentSecurityPolicy`.

`tests/RTUB.Web.Tests/Pwa/OfflineReachabilityTests.cs` - **8 tests** for section E, same
source-scan approach and for the same reason: the behaviour runs on a page served from a cache
with no origin, which no test host reproduces.

| # | Test | Pins |
| --- | --- | --- |
| 1 | `OfflineScript_DoesNotRedirectOnNavigatorOnLineAlone` | **the defect** - exactly one navigation exists, it sits behind the probe's success path, and the `navigator.onLine` branch cannot reach it |
| 2 | `OfflineScript_ProbesTheHealthEndpointAndNothingElse` | `/health` is the only endpoint the page calls |
| 3 | `OfflineScript_ProbeBypassesTheBrowserHttpCache` | `cache: 'no-store'` on the probe |
| 4 | `ServiceWorker_KeepsHealthNetworkOnly` | `/health` in `NEVER_CACHE_PREFIXES` and absent from `STATIC_ASSETS` |
| 5 | `OfflineScript_StaysOnThePageWhenTheProbeFails` | no navigation in the failure path; `catch`, `response.ok` and `AbortController` all present |
| 6 | `OfflineScript_KeepsTheDelayedRedirectWhenTheOriginIsReachable` | the restored message and the ~1s `REDIRECT_DELAY_MS` are unchanged |
| 7 | `OfflineScript_AllowsOnlyOneProbeInFlightAndOneRedirect` | `probeInFlight` and `redirecting` guards |
| 8 | `OfflineScript_KeepsItsExistingControlsAndCopy` | `online`/`offline` listeners, retry reload, 3s interval, all three Portuguese strings |

## Latest validation (unit 026)
Release build, 0 warnings / 0 errors. All five xUnit v3 native executables:

| Suite | Total | Failed | Skipped |
| --- | --- | --- | --- |
| `RTUB.Core.Tests` | 791 | 0 | 0 |
| `RTUB.Application.Tests` | 1997 | 0 | 0 |
| `RTUB.Shared.Tests` | 768 | 0 | 2 |
| `RTUB.Web.Tests` | 876 | 0 | 56 |
| `RTUB.Integration.Tests` | 281 | 0 | 2 |
| **Total** | **4713** | **0** | **60** |

Delta accounting, exact:
- 025 baseline: **4683** / 0 / 60.
- 026 before the section E fix: **4705** / 0 / 60 - **+22**, entirely `ServiceWorkerReliabilityTests`.
- 026 after the section E fix: **4713** / 0 / 60 - **+8**, entirely `OfflineReachabilityTests`.

Skips unchanged at 60 throughout. No unrelated BetService / test-runner flake was touched, and no
existing test needed editing for the section E fix.

Also: `node --check` clean on all four changed JS files (`service-worker.js`, `sw-register.js`,
`push-notifications.js`, `offline.js`), `git diff --check` clean, no migrations, credential scan
over the diff and both new test files clean.

## Browser / PWA validation (unit 026) - RUN
Headless Chromium against a local Release build over `https://localhost:58869` (trusted ASP.NET
dev cert, so the origin is a secure context and service workers really register).

**Bottom nav - did it reproduce in tooling? Partly, and the part that matters did.**
Headless Chromium **cannot** emulate `display-mode: standalone`: CDP
`Emulation.setEmulatedMedia` ignores the `display-mode` feature (`matchMedia('(display-mode:
standalone)')` stayed `false`) and `--app=` did not navigate. So the standalone condition was
removed from the matching `@media` rule **at runtime**, leaving the width condition and everything
else - source order, specificity, the real cascade - untouched. That isolates exactly the one
condition the emulator cannot supply.

Rule inventory read out of the live CSSOM, in document order, at 375px:

| Order | File | Media | Value |
| --- | --- | --- | --- |
| 1 | `1-base/mobile.css` | `(max-width: 768px)` | `clip` |
| 2 | `2-layout/navbar.css` | `(display-mode: standalone) and (max-width: 991.98px)` | `hidden` *(before)* / `clip` *(after)* |

- **Before:** ungating the standalone query flipped computed `html { overflow-x }` from `clip` to
  **`hidden`** on every case. The mechanism, reproduced.
- **After:** it stays **`clip`** on every case.

Scroll stress, 375px and 390px, `/music` (Albums - the reported page) and `/roles`, plus a repeat
after opening and closing the navbar offcanvas: **36 samples per case, 432 samples total**,
top -> quarter -> middle -> bottom -> back, with direction reversals.
`visualViewport.offsetTop + visualViewport.height - nav.getBoundingClientRect().bottom` stayed
**0 for every sample** (`delta_min = delta_max = 0`), `navTop` pinned at 805, `position: fixed`,
`display: flex` throughout. Desktop Chromium does **not** itself reproduce the iOS momentum-scroll
drift - it never treats html/body as the scroll container - so the drift is proven by the
mechanism and the cascade, not by a visible jump in this browser. **The remaining confirmation is
a real installed iPhone PWA.**

Modal open/close was not driven: the reachable modals on these anonymous pages need a signed-in
session. The scroll-lock states were audited in source instead (see A).

**PWA:** exactly **1** registration, scope `/`, script `/service-worker.js`, state `activated`,
page controlled. `manifest.webmanifest` 200 with 10 icons, `display: standalone`.
`registration.update()` resolved without throwing and left no waiting worker.
`PushNotificationsManager.initialize()` ran with **registrations 1 before, 1 after** - it no
longer creates a second one (it returned `false` because `/api/push/status` is 401 anonymously,
which is correct without credentials). **0 update toasts, 5 main-frame navigations for 5 `goto`
calls - no duplicate toast, no reload loop.**

**Offline, with the origin actually stopped** (`taskkill dotnet`, origin then answering nothing),
using a persistent browser profile warmed beforehand:
`/music`, `/profile`, `/messages`, `/events`, `/leaderboard` all returned **200 `text/html`,
694 bytes, `offline.html`** - heading "Sem Conexão", `/css/offline.css` linked and applying 9
rules, the `135deg` gradient resolving, `offline.js` present and running, retry control present,
**0 `<style>` elements and 0 inline style attributes** (no CSP regression). Every response had
**no `.mobile-bottom-nav`, no `.navbar`, no `blazor.web.js`** - i.e. **no authenticated or private
application HTML was served as a stale cached page.** `/api/push/status` and `/health` returned
`TypeError: Failed to fetch` - network-only, no cache fallback, exactly as designed.

A second offline pass using `context.set_offline(True)` gave the same result on four paths.

### Offline reachability (section E) - ORIGIN DOWN, NETWORK UP, proven both ways
The decisive case cannot be tested with `context.set_offline(True)`, because that flips
`navigator.onLine` and hides the exact bug. The origin process was killed instead, leaving the
machine online. 11/11 checks passed.

| Case | Result |
| --- | --- |
| **A. Origin UP** | `offline.html` loaded, `/health` succeeded, page transitioned to `/`. |
| **B. Origin DOWN, machine online** | Worker served `offline.html` from cache (`h1` "Sem Conexão", `/css/offline.css` linked with 9 rules, `offline.js` present). `navigator.onLine` **stayed true** - the bug's precondition. Sampled once a second for **12 seconds: `location.pathname` was `/offline.html` on every sample**, status `Ainda offline`. **No redirect, no bounce.** |
| **C. Origin restored** | The periodic probe detected `/health` and redirected to `/`; the real application page loaded. |
| Cache audit | `/health` present in **none** of `rtub-static/dynamic/images-rtub-v2.7.0`. |
| Console | No errors beyond the expected failed `/health` fetch while the origin was unavailable. |

**Negative-controlled, like test 22.** The pre-fix `offline.js` was staged back in and case B
re-run under identical conditions: it **bounced to `/` inside the first second** - the sampler's
own execution context was destroyed mid-navigation - and ended on `/`. The fixed file stayed on
`/offline.html` for the full 12s. The harness reproduces the real defect; it is not a proxy.

**Console:** no new JS errors and **zero page errors**. The console does carry pre-existing
`img-src` CSP violations for the R2 public origin - that is 025's documented behaviour when
`Cloudflare:R2:PublicUrl` is not supplied to the local run, not a 026 regression - plus the
401/405 responses from the excluded-path probes this validation deliberately issued.

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
None active. Unit 026 is complete and awaiting owner review.

## Next unit
**027 - CI/CD + Azure DEV.** The one PWA correctness defect 026's validation uncovered - the
offline-page bounce - was fixed inside 026 rather than deferred, so nothing PWA-related is
outstanding except the device confirmation below.

Constraints 026 hands forward:
- `/service-worker.js` must keep being served **without** a CSP header (025's rule, still pinned
  by `NonDocumentResponse_CarriesNoContentSecurityPolicy`).
- `sw-register.js` is the **sole** service-worker registration owner. Anything needing the
  registration adopts it via `navigator.serviceWorker.ready`.
- No stylesheet may set `overflow-x: hidden` on `html`/`body` again, in any media query - it must
  be `clip`. Pinned by `NoStylesheet_SetsOverflowHiddenOnTheViewportRoot`.
- Service-worker caches must stay free of application HTML and of `/api`, `/auth`, `/_blazor`,
  `/hubs`, `/health`. `/health` in particular must stay network-only: `offline.js` uses it as its
  origin-reachability probe, and a cached 200 would resurrect the bounce bug.

**One confirmation is outstanding and needs a real device:** the MobileBottomNav fix is proven by
the cascade and by the computed `overflow-x` flipping under a runtime-ungated standalone query,
but desktop Chromium cannot reproduce iOS momentum-scroll drift and cannot emulate
`display-mode: standalone`. A run on an **installed iPhone PWA on Albums (`/music`)** would close
it. Deploying 026 is what makes that check possible.

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

**Deployment note for 025:** the policy is built from `Cloudflare:R2:PublicUrl` and
`Cloudflare:R2:AccountId`. Both are already required by the storage services, so **no new
configuration key is introduced** and no portal change is needed for this deploy. If either were
ever unset in an environment, CSP would omit that source - R2 images/audio/PDFs would be blocked
rather than the policy being weakened.

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
- ~~**CSP is not enabled.**~~ - **done.** All four blockers 021 enumerated are cleared: `eval`
  (022), inline `<script>` and `on*` handlers (023, plus the JS-built handler 025 found), inline
  `<style>` and `style=` (024), and the runtime R2 origin (025 - which turned out to be *two*
  origins). The enforced header ships in 025. No report-only policy was ever needed.
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
  and already flagged by the `rtub-push` skill. The push unit owns consolidating it.
- ~~`sw-register.js` toast styles~~ and ~~`offline.html` inline `<style>`~~ - **both done by 024.**
- ~~**`App.razor` still has the dead `fonts.googleapis.com` `preconnect` / `dns-prefetch`**~~ -
  **deleted by unit 025**, where `font-src` was decided. Re-verified dead first, and now pinned by
  `NoWebFontServiceIsReferenced`.
- **`rtub.carousel.js` is still dead** (raised by 022, unchanged).

### Raised by 024 (two items closed by 025)
- ~~**`memberMap.js` still builds an inline `onerror` handler inside its popup HTML string**~~ -
  **fixed by unit 025.** Swapped for `data-avatar-fallback`, and the whole class of bug is now
  swept repository-wide by `ApplicationJavaScript_BuildsNoMarkupCarryingInlineEventHandlers`
  rather than pinned file by file. Verified in the browser with a failing avatar URL.
- **`.meeting-today-badge` is orphaned CSS.** Now in `css/3-components/meeting-card.css`; no element
  has ever carried the class. Kept because deleting it is unrelated cleanup, and its absence from the
  markup is now recorded in `MeetingCardTests`.
- **The rare-tab badge animation is dead.** `animation: rare-tab-pulse` cannot resolve, because the
  keyframes exist only in scoped CSS where Blazor renames them. Broken before 024 and identically
  broken after. Fixing it would start an animation that has never run - a visual change, so it is the
  owner's call.
- **`Profile.razor`'s `overflow-x: hidden` intent is unimplemented.** The `:global(...)` rule was
  inert and was deleted. If horizontal overflow on the profile page was a real concern, it needs a
  real rule - that is a visual change, not a CSP one.
- **`dotnet test` does not work in this repo.** It reports "Zero tests ran" / exit 5 for every
  project, with or without a filter, so the test executables have to be run directly
  (`tests/<proj>/bin/<config>/net10.0/<proj>.exe`, filtered with `-class` / `-method`). Confirmed
  still broken in 025, which used the native executables throughout. Worth fixing, since CI and the
  recorded baselines depend on it.
- **`BetServiceTests.PlaceBetAsync_WithInsufficientBalance_ThrowsException` is flaky, not
  consistently failing.** 024 saw it fail in a full-project run and pass in isolation at
  `00e8009f`, and attributed it to shared-fixture ordering. On 025's Release run it **passed in
  both** - full 1997-test project run and isolation. Nothing in 025 touches `BetService` or any
  fixture. Still pre-existing, still its own unit; the ordering dependency is real even when the
  symptom does not appear.

### Found by 026
- ~~**`offline.js` redirects off the offline page whenever `navigator.onLine` is true**~~ -
  **FIXED before merge; see *E. Offline page bounced off itself* above.** It now probes `/health`
  with `cache: 'no-store'` and only a successful response redirects. Proven with the origin
  stopped and the machine still online, and negative-controlled against the pre-fix file.
- **`.no-scroll` sets `position: fixed` with no `top` offset** (`1-base/global.css`). Applying it
  scrolls the page back to the top and does not restore the offset on removal. It does **not**
  affect MobileBottomNav - `position: fixed` on `body` is not a containing-block trigger - so it is
  unrelated to the 026 bug. Pre-existing; a scroll-position fix is a behaviour change.

### Carried forward unchanged from 025
- **`dotnet test` still does not work in this repo.** 026 used the five native xUnit v3
  executables throughout, as 025 did.
- **`BetServiceTests.PlaceBetAsync_WithInsufficientBalance_ThrowsException`** passed in 026's full
  Release run. Not touched, as instructed. Still its own unit.
