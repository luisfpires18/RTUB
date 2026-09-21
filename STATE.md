# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **021 (security headers + CSP readiness) - COMPLETE, uncommitted, awaiting
owner review.** Unit 020 is merged to `dev` at `69a1c88f`.

## Branch
`fix/021/security-headers-csp-readiness`, branched from `dev` (clean, in sync with `origin/dev` at
`69a1c88f`). Uncommitted - no commit authorized.
`chore/001`-`chore/011`, `fix/012`-`fix/014`, `chore/015`, `fix/016`-`fix/018`, `perf/019`,
`fix/020` still present; delete when convenient.

## Owner decision (2026-09-21)
**Password-policy hardening is SKIPPED**, by instruction. Identity's password requirements were
not read for change and not touched by 021. It remains available as a future unit.

## Last completed step
**Unit 021 - four browser security headers added in one central middleware.
Content-Security-Policy is DEFERRED on evidence, not shipped weak.**

### Headers found BEFORE 021
Measured against live production, not inferred:
`curl -I https://rtub.azurewebsites.net/health` returns exactly one security header -
**`Strict-Transport-Security: max-age=2592000`**. Nothing else.

| Header | Before | Source |
| --- | --- | --- |
| `Strict-Transport-Security` | **present**, `max-age=2592000` | `app.UseHsts()`, `Program.cs`, non-Development only |
| `X-Content-Type-Options` | absent | - |
| `Referrer-Policy` | absent | - |
| `X-Frame-Options` | absent | - |
| `Permissions-Policy` | absent | - |
| `Content-Security-Policy` | absent | - |

A repo-wide grep for every one of those header names across `*.cs`, `*.razor`, `*.json`, `*.js`,
`*.ts`, `*.config`, `*.yml`, `*.html` returned **zero hits**. There is **no `web.config`, no
`staticwebapp.config.json`, no `*.pubxml`**, and `.github/workflows/ci.yml` sets no app settings
and no headers. **Azure App Service supplies nothing** beyond what the app itself emits - the
`Server: Kestrel` response confirms there is no IIS layer adding any.

So the earlier audit note "no meaningful CSP/security-header setup" was right about CSP but
**wrong about HSTS**, which has been live all along.

### Headers ADDED (one `app.Use` block, `Program.cs`, before `UseHttpsRedirection`)
| Header | Value | Why this value |
| --- | --- | --- |
| `X-Content-Type-Options` | `nosniff` | RTUB serves user-uploaded media plus JSON/manifest documents. Zero-risk, real value. |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Origin-only on the `target="_blank"` links out to YouTube/Spotify, nothing on downgrade. Codifies what current browsers already default to - it changes nothing on a modern browser and is insurance for one that does not. |
| `X-Frame-Options` | `DENY` | See the framing verification below. |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=()` | Exactly the four capability APIs verified unused. |

Placement: registered **after** the `UseExceptionHandler`/`UseHsts` block and **before**
`UseHttpsRedirection`, `UseResponseCompression`, `UseRouting`, `UseStaticFiles`, the
`/_blazor/initializers` short-circuit and every endpoint - so all of those are covered, including
static assets and 404s. Because `UseExceptionHandler` is registered upstream and **re-executes the
pipeline**, the middleware runs a second time on an error response; the headers are therefore
assigned **by indexer, not `Append`**, so re-execution is idempotent. A test pins that.

One `app.Use` block in `Program.cs`, ~13 effective lines. **No new file, no extra package**, and
no `Response.Headers` assignment was added to any page or controller.

### `X-Frame-Options: DENY` - why DENY and not SAMEORIGIN
Verified before choosing, not assumed. **RTUB never embeds itself:**
- no `window.top` / `window.parent` / `window.self` framing logic anywhere in `src/`;
- the PWA manifest is `"display": "standalone"` - not a framed surface;
- the Android TWA (`/.well-known/assetlinks.json`) uses a Chrome Custom Tab, **not an iframe**;
- no external identity provider, so no OAuth popup/frame handshake.

The app **does** contain two `<iframe>`s - `Pages/Media/Songs.razor:175` and
`Pages/Public/Roles.razor:730`, both PDF viewers. Both embed **R2-hosted** documents, whose
framing is governed by **R2's** response headers, not RTUB's. `X-Frame-Options` on RTUB's own
responses cannot affect them. DENY is therefore safe and strictly better than SAMEORIGIN for a
Blazor Server circuit.

### `Permissions-Policy` - why only four directives
No copied deny-list. A grep for `navigator.geolocation`, `getUserMedia`, `navigator.mediaDevices`,
`requestFullscreen`, `navigator.clipboard`, `capture=`, `PaymentRequest`, `navigator.usb`,
`navigator.bluetooth`, `navigator.xr`, `DeviceOrientation` and `accelerometer` across
`wwwroot/js/`, `Pages/`, `Shared/` and `RTUB.Shared` found **exactly one capability API in use:
`navigator.clipboard`** (`wwwroot/js/clipboardCopy.js:54`, `Pages/Share.razor:88`).

- **Denied** (verified unused): `camera`, `microphone`, `geolocation`, `payment`. With 15 `eval`
  sites still live (below), denying device access that RTUB never asks for is genuine
  defence-in-depth against XSS escalation, not theatre.
- **Deliberately NOT denied:** `clipboard-write` - **in use**, denying it would break Share and
  the copy helper; `fullscreen` - **in use**, `Songs.razor` and `Roles.razor` carry
  `allow="fullscreen"` on the PDF iframes, and `fullscreen=()` would break them.
- **Deliberately omitted entirely:** `usb`, `bluetooth`, `serial`, `hid`, `midi`,
  `xr-spatial-tracking`, `magnetometer`, `gyroscope`, `accelerometer` and the rest of the long
  tail. Directive support is inconsistent across browsers and the real-world risk for this app is
  not measurable - this is exactly the copied deny-list the brief ruled out.

### CSP - **DEFERRED**, and the blockers are exact
A policy that keeps RTUB working today would need **both `'unsafe-eval'` and `'unsafe-inline'`**
for `script-src`. That is worth less than no policy, so none was shipped.

**Blocker 1 - `eval`. 15 call sites, 6 production files.** The earlier audit was right that this
existed and right about where; it is all still present.

| File | Sites |
| --- | --- |
| `src/RTUB.Shared/Components/UI/PushNotificationToggle.razor` | 6 (`:74, :118, :135, :148, :194, :230`) |
| `src/RTUB.Shared/Components/UI/PushNotificationPrompt.razor` | 2 (`:83, :145`) |
| `src/RTUB.Shared/Components/Cards/NaipeCard.razor` | 2 (`:132, :147`) |
| `src/RTUB.Web/Pages/Index.razor` | 2 (`:264, :287`) |
| `src/RTUB.Web/Pages/Media/Gallery.razor` | 2 (`:1411, :1419`) |
| `src/RTUB.Web/Pages/Media/Albums.razor` | 1 (`:698`) |

All 15 are `JSRuntime.InvokeVoidAsync("eval", ...)` / `InvokeAsync<T>("eval", ...)` - **string
dispatch across the C#/JS interop boundary**. A plain `grep -E "\beval\s*\("` finds **none of
them**; the graph has no edge there either. They are found only by grepping the literal `"eval"`.
Worth remembering: this is the failure mode `CLAUDE.md` warns about for interop.

**Blocker 2 - inline `<script>`, 3 production blocks.** `src/RTUB.Web/App.razor:27` (service-worker
registration, must stay in `<head>` for PWABuilder detection),
`src/RTUB.Web/Shared/MainLayout.razor:343` (`Blazor.start({...})` with the SignalR circuit config
plus the offcanvas auto-dismiss handler), `src/RTUB.Web/wwwroot/offline.html:77`. Each needs a
nonce or a hash. A nonce is the harder one here: `App.razor` is the root document and `MainLayout`
feeds `HeadOutlet`, so the nonce has to reach both from the request.

**Blocker 3 - inline styles.** **11 `<style>` blocks** in production `.razor` files plus
`offline.html`, and **213 `style="..."` attributes across 29 `.razor` files**. Both forms are
covered by `style-src`, so a policy without `'unsafe-inline'` needs all of them moved out. (Note
for whoever picks this up: Bootstrap's *runtime* CSSOM writes - `el.style.x = ...` for offcanvas
and modal transforms - are **not** CSP-governed and are not a blocker.)

**Blocker 4 - the R2 origin is runtime configuration, not a constant.** `img-src`, `media-src` and
`frame-src` all need the Cloudflare R2 public origin, which comes from `Cloudflare:R2:PublicUrl`
(e.g. `https://pub-xxx.r2.dev`) and is **stored absolute in the database**. The policy string has
to be built from `IConfiguration` at startup, not written as a literal. Not hard - but it is work,
and getting it wrong dark-breaks every image.

**Not blockers - the external origins are known and enumerable.** Verified by reading what the
browser actually loads, not by guessing:
- `script-src`: `https://cdnjs.cloudflare.com` (cropper.js), `https://unpkg.com` (leaflet, SRI
  pinned), `https://cdn.jsdelivr.net` (pixi.js)
- `style-src`: `https://cdnjs.cloudflare.com` (cropper.css), `https://unpkg.com` (leaflet.css,
  SRI pinned)
- `img-src`: `'self' data:` plus `https://*.basemaps.cartocdn.com`
  (`wwwroot/js/memberMap.js:72` tile layer) plus the R2 origin
- `connect-src`: `'self'` - the `_blazor` WebSocket is **same-origin**, and CSP3 `'self'` matches
  `wss:` to the same host. The service worker (`wwwroot/service-worker.js`) fetches **nothing
  cross-origin**: every `fetch` is same-origin (`/api/push/*`, cached assets).
- `frame-src`: the R2 origin only (the two PDF viewers)
- `font-src`: `'self'` - **`https://fonts.googleapis.com` is a dead `preconnect`/`dns-prefetch`
  in `App.razor:24-25` with no matching stylesheet link and no `@font-face`. Nothing loads from
  it.** Do not add it to a policy; delete the hint instead (recorded in *Deferred*).
- YouTube / Spotify / Instagram / Facebook appear only as `<a href target="_blank">`
  **navigations**, never embeds - so they need **no** directive at all.

That inventory is the useful output of 021: when the blockers are cleared, the policy can be
written from it without re-auditing.

### HTTPS / HSTS - conclusion: change nothing
- **HSTS is already live and correct**: `UseHsts()` runs in every non-Development environment and
  production returns `max-age=2592000` (30 days). **Not touched.**
- **`includeSubDomains` and `preload` deliberately NOT added.** Production is
  `rtub.azurewebsites.net` - RTUB does **not own** the `azurewebsites.net` apex, which is shared
  Microsoft infrastructure. Asserting a subdomain-wide or preload-list policy from a tenant of a
  shared domain is wrong, and `preload` is effectively irreversible. Revisit only if RTUB moves to
  a domain it owns.
- **`UseHttpsRedirection` left skipped outside Development.** Unchanged, by prior decision: 014
  flagged it and the 2026-09-21 forwarded-headers confirmation made the skip redundant rather than
  load-bearing. Removing it is a production request-pipeline behaviour change and is its own unit,
  not a ride-along on a headers change. Still carried in *Deferred*.

### Browser validation - not run, and why that is correct
The brief requires a browser smoke run **only if CSP is enabled**. It is not. No frontend source
changed, no runtime frontend behaviour changed, and the four added headers do not alter rendering
or script execution. The seven integration tests exercise the real middleware pipeline end to end
(pages, a static file, a 404, `/health`), which is the proof that was actually needed.

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
None active. Unit 021 is complete and awaiting owner review.

## Next unit
**022 - remove the `eval` interop, so a CSP becomes possible.** The smallest unit that clears the
largest CSP blocker, and **not** a frontend refactor. Scope: the **15** `JSRuntime` `"eval"` call
sites in the 6 files tabulated above, replaced by named functions in the `wwwroot/js/` modules
that already exist for these areas (`push-notifications.js` covers the two push components;
`scrollSpy.js` / `mediaSession.js` are the natural homes for the scroll and video cases). No
nonce architecture, no inline-style work, no CSP header in 022 - those are separate.
Note for 022: `tests/RTUB.Shared.Tests/Components/UI/PushNotificationToggleTests.cs` sets up
bUnit `JSInterop` against the **`"eval"` identifier and the script text** (`EvalScriptContains`),
so those setups must move to the new function names in the same unit or they will fail.

Then, in order: **023** inline `<script>` removal / nonce-or-hash for the 3 blocks; **024** inline
styles (11 `<style>` blocks + 213 `style=` attributes); **025** enable CSP itself, built from the
directive inventory recorded above, with the R2 origin read from `Cloudflare:R2:PublicUrl` - and
with the browser smoke run 021 did not need.

Also still available, deliberately not taken in 021: **password-policy review / hardening**
(Identity is `RequiredLength = 4` with every complexity rule off, `AddIdentityServices`,
`ServiceCollectionExtensions.cs`) - the owner skipped it for this unit.
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
- **CSP is not enabled.** Four blockers, enumerated exactly in *Last completed step*: 15 `eval`
  interop sites, 3 inline `<script>` blocks, 11 `<style>` blocks + 213 `style=` attributes, and
  the R2 origin only being known at runtime. Units 022-025 above clear them in that order. No
  `Content-Security-Policy-Report-Only` was shipped either - a report-only policy is worth adding
  once the blockers are down and it can report something actionable, not while it would report
  every page load.
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
- **Phase 1C:** remaining optional custom skills — deliberately not created.
- Work-branch cleanup (`chore/001`–`chore/011`) — delete when convenient.
- Two `graphifyy 0.9.56 + MCP` installs (isolated venv, Microsoft-Store Python user site). The
  Store one wins PATH and works; both are compatible, so neither needs removing.
- Pending feature work — unchanged, not part of any phase.
## Relevant files (unit 021)
Changed (2):
- `src/RTUB.Web/Program.cs` - **the only production file touched.** One `app.Use` security-header
  block inserted between the `UseExceptionHandler`/`UseHsts` block and `UseHttpsRedirection`,
  carrying the four headers plus the reasoning for the placement, the indexer assignment, and the
  CSP omission. Nothing else in the file was edited - `UseHsts`, `UseHttpsRedirection`,
  `UseResponseCompression`, `UseResponseCaching`, `UseRouting`, `UseRateLimiter`, the
  `UseStaticFiles` branch and its `Cache-Control` rules, the `/_blazor/initializers` short-circuit
  and every endpoint are byte-for-byte unchanged.
- `tests/RTUB.Integration.Tests/SecurityHeaderTests.cs` - **new**, 7 tests.

Read and deliberately **not** changed: `App.razor` (inline script, dead font preconnect),
`Shared/MainLayout.razor` (inline script, CDN tags), `wwwroot/service-worker.js`,
`wwwroot/offline.html`, `wwwroot/js/memberMap.js`, `Pages/Media/Songs.razor`,
`Pages/Public/Roles.razor`, the 6 `eval` components, `Extensions/ServiceCollectionExtensions.cs`
(password policy - skipped by owner decision), `.github/workflows/ci.yml`, `appsettings*.json`.

## Tests (7 new, 0 removed, 0 existing assertions changed)
**`SecurityHeaderTests` (new, 7).** Only the headers RTUB intentionally sets are asserted.
CSP is **not** asserted in either direction, so enabling it in a later unit needs no edit here -
as the brief required. `Strict-Transport-Security` is not asserted either: `UseHsts` only runs
outside Development and the test host is not a production environment, so asserting it would pin
a value the test host never produces.
1-3. `Page_CarriesEverySecurityHeader` - `[Theory]` over `/`, `/login`, `/Events`; asserts the
   exact value of all four headers. Three cases.
4. `StaticFile_CarriesSecurityHeaders` - `/manifest.webmanifest`, proving the middleware sits
   ahead of the `UseStaticFiles` branch and is not endpoint-only.
5. `NotFound_CarriesSecurityHeaders` - an unrouted path still carries them.
6. `HealthEndpoint_CarriesSecurityHeaders` - `/health`, the one endpoint mapped outside the
   Razor component pipeline.
7. `Headers_AreSetOnce_NotAppendedPerPass` - each header has exactly one value. This is the test
   that pins the indexer-not-`Append` choice; with `Append` a re-executed pipeline would emit
   duplicates.

No credential literal. No existing test file was opened or modified.

## Latest validation (unit 021)
- Release build, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true` and
  `EnforceCodeStyleInBuild=true`.
- Focused first: `SecurityHeaderTests` - **7 total, 7 passed**.
- Full suite, `dotnet test --no-build -c Release`: **4554 passed, 0 failed, 60 skipped**
  (total 4614). Against the post-020 `dev` baseline of **4547**, the branch is **+7** - exactly
  the 7 tests listed above (3 `[Theory]` cases + 4 `[Fact]`s). Skipped count unchanged at 60.
- `git diff --check`: clean.
- Diff credential scan: no match.
- **No migration and no model-snapshot change** - no column, no entity, no `DbContext` edit.
- **No frontend build and no Graphify rebuild** - no frontend source and no application structure
  changed. Two files total: one production, one new test.
- **No browser run** - correct per the brief, since CSP was not enabled and no runtime frontend
  behaviour changed. See *Browser validation* above.
- Production headers were read live (`curl -I https://rtub.azurewebsites.net/health`) as the
  *before* evidence; nothing was deployed, pushed or changed on Azure.
- No unrelated refactor: password policy, MFA, cookie validation, login throttling, PWA
  architecture, CI/CD, Azure provisioning, dependency servicing and the `eval`/inline-JS cleanup
  were all left exactly as they were.
