# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **025 (enable enforced Content-Security-Policy) - COMPLETE, uncommitted,
awaiting owner review.** Unit 024 is merged to `dev` at `ecabb0fc`. **The CSP / security-header
modernization track is COMPLETE**: 021 shipped the four supporting headers, 022 removed `eval`,
023 removed inline script, 024 removed inline style, and 025 ships the policy itself - enforced,
with no `'unsafe-inline'` and no `'unsafe-eval'`.

## Branch
`fix/025/enable-csp`, branched from `dev` (clean, in sync with `origin/dev` at `ecabb0fc`).
Uncommitted - no commit authorized.
`chore/001`-`chore/011`, `fix/012`-`fix/014`, `chore/015`, `fix/016`-`fix/018`, `perf/019`,
`fix/020`-`fix/024` still present; delete when convenient.

## Owner decision (2026-09-21)
**Password-policy hardening is SKIPPED**, by instruction. Identity's password requirements were
not read for change and not touched by 021 or 025. It remains available as a future unit.

## Last completed step
**Unit 025 - RTUB serves an enforced `Content-Security-Policy`. No `'unsafe-inline'`, no
`'unsafe-eval'`, no `'unsafe-hashes'`, no nonce, no hash, no wildcard origin.**

### The final policy
    default-src 'self';
    base-uri 'self';
    object-src 'none';
    frame-ancestors 'none';
    form-action 'self';
    script-src 'self' https://cdnjs.cloudflare.com https://unpkg.com https://cdn.jsdelivr.net;
    script-src-attr 'none';
    style-src 'self' https://cdnjs.cloudflare.com https://unpkg.com;
    style-src-attr 'none';
    img-src 'self' data: https://*.basemaps.cartocdn.com <R2-public>;
    media-src 'self' <R2-public> <R2-endpoint>;
    font-src 'self';
    manifest-src 'self';
    worker-src 'self';
    frame-src <R2-endpoint>            (or 'none' when unconfigured);
    connect-src 'self' <ws|wss>://<request host>

`<R2-public>` and `<R2-endpoint>` are filled from configuration at startup; the WebSocket source
is filled per request. Neither is a literal in source.

### Every non-'self' source, and the resource that earns it
| Directive | Source | Why it is there |
| --- | --- | --- |
| `script-src` | `https://cdnjs.cloudflare.com` | `cropper.min.js` (MainLayout) |
| `script-src` | `https://unpkg.com` | `leaflet.js`, SRI-pinned (MainLayout) |
| `script-src` | `https://cdn.jsdelivr.net` | `pixi.min.js` (MainLayout) |
| `style-src` | `https://cdnjs.cloudflare.com` | `cropper.min.css` (App.razor `HeadContent`) |
| `style-src` | `https://unpkg.com` | `leaflet.css`, SRI-pinned |
| `img-src` | `data:` | `ImageCropper.razor` and `Gallery.razor` render the picked file as a `data:` URL before upload |
| `img-src` | `https://*.basemaps.cartocdn.com` | Leaflet dark-matter tiles in `memberMap.js` |
| `img-src` | R2 public origin | avatars, gallery and event images, stored absolute |
| `media-src` | R2 public origin | `<video><source>` URLs, stored absolute |
| `media-src` | R2 S3 endpoint | **pre-signed** album audio (`CloudflareAudioStorageService`) |
| `frame-src` | R2 S3 endpoint | **pre-signed** PDFs: `Songs.razor` lyrics, `Roles.razor` RGI |
| `connect-src` | `ws(s)://<request host>` | the Blazor Server circuit |

**Two R2 origins, not one - this corrected 024's plan.** 024 recorded `Cloudflare:R2:PublicUrl`
as the only dynamic origin. Reading the storage services showed that is half of it:
`GeneratePreSignedUrlAsync` (audio, documents, lyrics) issues URLs against the **S3 API
endpoint**, `https://{Cloudflare:R2:AccountId}.r2.cloudflarestorage.com`, which is a different
origin from the public bucket. So `media-src` needs both, and `frame-src` needs only the endpoint -
the two PDF viewers are pre-signed, never public-bucket URLs.

**Considered and deliberately left out:**
- `connect-src` gets **no** R2 and **no** CDN. Nothing on the page fetches them: Pixi sprites are
  local `wwwroot` paths (`StageBiomeService` enumerates a folder; `CharacterService` matches
  `boss_{username}.png` under `wwwroot`), Pixi's R2 images go through the same-origin
  `/api/cdn/image` proxy, game music is `/sound/*.mp3`, and every API call is same-origin.
- `img-src` gets **no** S3 endpoint - there are no pre-signed image URLs.
- `img-src` gets **no** `blob:`. The only `createObjectURL` is in `fileDownload.js` and it feeds an
  `<a download>` href, which CSP does not govern.
- `media-src` gets **no** `data:`. `MediaUploadManager.IsVideo` tests the URL's file extension, so
  a `data:` preview always renders through the `<img>` branch, never `<source>`.
- `font-src` stays `'self'`: bootstrap-icons ships its `woff2` relatively, and there is no
  `@font-face` anywhere in `wwwroot/css`.
- YouTube / Spotify / Instagram / Facebook appear only as `<a target="_blank">` **navigations**,
  never embeds, and navigation is not governed by any directive in this policy.
- `'unsafe-eval'` / `'wasm-unsafe-eval'`: not needed. Every component is `InteractiveServer`;
  there is no `InteractiveAuto` or `InteractiveWebAssembly` render mode in the repo.

### The last script-src blocker - `memberMap.js`
024's markup sweep could not see it: `memberMap.js` builds its Leaflet popup as an HTML **string**,
and that string carried `onerror="this.src='/images/default-avatar.webp';"`. Under
`script-src-attr 'none'` that is blocked exactly like a handler written in `.razor`.

Fixed by reusing 023's mechanism rather than inventing a second one: the `<img>` now carries
`data-avatar-fallback` and is served by the single capture-phase listener in `avatarFallback.js`.
That works for dynamically inserted images because Leaflet's `DivOverlay.onAdd` appends the popup
container to the pane **before** `update()` sets its `innerHTML` - the images are already in the
document when their non-bubbling `error` event fires, so the capture-phase listener on `document`
sees it. Proven in the browser, not assumed: see *Browser validation*.

**The regression gate is a repository-wide scan of application JS, not a one-file assertion.**
`ApplicationJavaScript_BuildsNoMarkupCarryingInlineEventHandlers` sweeps every hand-written `.js`
and `.ts` under `src/` (excluding `wwwroot/lib`, `node_modules`, `obj`, `bin`, `*.d.ts`) for
`on<name>=` followed by a quote, and aggregates every hit into one failure. A DOM property write
(`el.onerror = fn`) is not a CSP violation and is excluded by a negative lookbehind. Two comments
in `avatarFallback.js` and `memberMap.js` that quoted the old attribute verbatim were reworded so
the scan can stay a plain regex rather than needing a JS tokenizer.

### R2 handling - configuration in, normalized origin out
`ContentSecurityPolicyBuilder` is the only place either origin is produced.
- **Public URL** is parsed with `Uri.TryCreate(..., UriKind.Absolute)`, accepted only for
  `http`/`https` with a non-empty host, and reduced to `scheme://host[:port]`. Path, query,
  fragment and userinfo are discarded, so a configured value cannot carry a `;` or a second
  directive into the header.
- **Account id** is interpolated into a hostname, so it is accepted only as a single DNS label
  (ASCII alphanumerics and interior hyphens, 1-63 chars) before
  `https://{id}.r2.cloudflarestorage.com` is formed.
- **Missing or malformed configuration contributes nothing.** The source is dropped, never
  replaced with a wildcard; `frame-src` degrades to `'none'`. R2 content may then be unavailable,
  which is the correct failure direction. No configuration value is logged.

### WebSocket handling
`connect-src 'self'` is **not** relied on to cover the Blazor circuit. MDN notes `'self'` is not
consistently taken to match `ws:`/`wss:` across browsers, so the exact origin of the current
request is emitted: `wss://host[:port]` for an https request, `ws://host[:port]` for http, built
from `Request.Scheme` and `Request.Host`. The Host header is client-controlled, so it is validated
as a plain host-with-optional-port (DNS name, IPv4, or bracketed IPv6) and dropped if it is not - a
refused circuit is recoverable, attacker-chosen text inside a security header is not. The broad
`ws:` / `wss:` schemes are never used.

### Header architecture - 021's middleware extended, nothing new added
The policy is set inside the existing security-header middleware in `Program.cs`. No new
middleware, no third-party CSP package, no second header system. The builder is a small class with
two constructors: one taking `IConfiguration` for production, one taking the two raw strings so the
tests can drive it directly without `InternalsVisibleTo`.

**Assignment, not `Append`**, and the same idempotence argument as 021: `UseExceptionHandler`
re-executes the pipeline, so the callback registers twice on the same response; assigning by
indexer makes the second pass a no-op. Pinned by
`SecurityHeaderTests.Headers_AreSetOnce_NotAppendedPerPass`.

### Scoping: HTML documents only, and why
The header is set from `Response.OnStarting`, where `Content-Type` is final, and only when it
starts with `text/html`. A deliberate decision, not an optimisation:

1. **A CSP header served with a worker script governs that worker's own execution context.**
   RTUB's service worker intercepts and re-fetches the cross-origin subresources it caches - R2
   media, the three script/style CDNs, the Carto tiles. None of those are in `connect-src`,
   because the *page* never fetches them. A blanket policy would hand `/service-worker.js` a
   `connect-src 'self'` and break offline caching. Measured: the worker's caches hold 5
   cross-origin entries on a single anonymous homepage load.
2. **On other subresource responses the header buys nothing.** The directives that matter are
   enforced by the embedding document's policy at fetch time, and `frame-ancestors` applies only
   to documents - where `X-Frame-Options: DENY`, set on every response since 021, already covers
   the same ground.

Verified live: `/` and `/login` carry exactly one CSP header; `/service-worker.js`,
`/css/site.css`, `/js/avatarFallback.js`, `/js/offline.js` and `/manifest.webmanifest` carry none.

### Report-Only was not used and is not shipped
The enforced header was correct on the first browser run, so no report-only phase was needed.
`Content-Security-Policy-Report-Only` appears nowhere in the branch, and
`SecurityHeaderTests.Page_CarriesExactlyOneEnforcedContentSecurityPolicy` fails if it ever does.

### Safe cleanup taken
`App.razor`'s `preconnect` and `dns-prefetch` to `https://fonts.googleapis.com` are **deleted**.
021 found them dead - no matching stylesheet, no `@font-face`, no `fonts.gstatic.com` reference
anywhere. Re-verified in 025 before removal, and pinned by a test, so `font-src 'self'` is not
quietly hiding a missing source.
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
None active. Unit 025 is complete and awaiting owner review.

## Next unit
**PWA / Service Worker reliability modernization.** 025's scoping decision is a constraint on it:
whatever changes, `/service-worker.js` must keep being served **without** a CSP header, or the
worker's cross-origin caching breaks. Three cases pin that
(`NonDocumentResponse_CarriesNoContentSecurityPolicy`).

Carry into that unit as a specific regression case: **the observed iPhone / PWA `MobileBottomNav`
transient drift** - the bottom nav shifts position briefly on iOS in standalone mode. Not
reproduced or investigated in 021-025; none of them touched it.

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

## Relevant files (unit 025)

**New (2 files):**
- `src/RTUB.Web/Security/ContentSecurityPolicyBuilder.cs` - the whole policy. Public `Build(scheme,
  host)`; everything before `connect-src` is built once in the constructor, only the WebSocket
  source is per-request. Two constructors: `IConfiguration` for production, `(publicUrl,
  accountId)` for tests.
- `tests/RTUB.Web.Tests/Security/ContentSecurityPolicyTests.cs` - 35 cases, listed below.

**Changed (4 files):**
- `src/RTUB.Web/Program.cs` - `using RTUB.Security;`, one builder instance, and an
  `OnStarting` callback inside the existing security-header middleware. The four headers from 021
  are untouched.
- `src/RTUB.Web/wwwroot/js/memberMap.js` - popup avatar: inline `onerror` -> `data-avatar-fallback`.
- `src/RTUB.Web/wwwroot/js/avatarFallback.js` - comment reworded only; no behaviour change.
- `src/RTUB.Web/App.razor` - dead Google Fonts `preconnect` / `dns-prefetch` deleted.
- `tests/RTUB.Integration.Tests/SecurityHeaderTests.cs` - CSP delivery assertions added.

**No migration, no entity, service, repository or DI change.** `git diff --name-only` matches no
migration or snapshot file.

## Tests (1 new file, +35; 1 existing test file extended, +7)

**New - `ContentSecurityPolicyTests`, 35 cases.** The policy assertions go through the public
builder rather than a hard-coded expected string, so a deliberate directive change does not have
to be restated in ten places; only a change that actually weakens the policy fails.
1. `Policy_NeverAllowsUnsafeInlineOrUnsafeEval` - also bans `'unsafe-hashes'`.
2. `Policy_ContainsNoWildcardOrWholeSchemeSources` - every source in every directive is checked
   against `*`, `https:`, `http:`, `ws:`, `wss:` and a leading `*.`; `data:` is additionally
   banned from `script-src`, `style-src`, `frame-src` and `object-src`.
3. `Policy_PinsTheDirectivesThatLockOutInjectedContent` - `default-src`/`base-uri`/`form-action`
   `'self'`; `object-src`/`frame-ancestors`/`script-src-attr`/`style-src-attr` `'none'`;
   `manifest-src`/`worker-src`/`font-src` `'self'`.
4. `ExternalOrigins_AppearOnlyInTheDirectivesThatNeedThem` - exact source sets for `script-src`
   and `style-src`, plus negative checks (jsdelivr is not a style source, Carto is not a script
   source, cdnjs is not a connect source).
5. `ConfiguredR2Origins_LandOnlyInTheDirectivesThatUseThem` - public origin in `img-src` and
   `media-src`; endpoint in `media-src` and `frame-src`; endpoint **not** in `img-src`; public
   origin **not** in `connect-src` or `script-src`.
6. `ConfiguredR2PublicUrl_ContributesOnlyItsNormalizedOrigin` / `..._KeepsANonDefaultPort`.
7. `MalformedR2PublicUrl_CannotInjectPolicyText` - 9 cases including
   `https://evil.example; script-src 'unsafe-inline'`, `javascript:`, `file://`, `*`, bare host,
   empty, whitespace, null. Each asserts the injected text is absent **and** that `img-src` /
   `media-src` fall back to exactly their static source sets.
8. `MalformedR2AccountId_YieldsNoFrameSource` - 7 cases; `frame-src` must be `'none'`.
9. `WebSocketSource_MatchesTheRequestSchemeAndHost` - https/http, with and without a port, plus
   bracketed IPv6.
10. `MalformedRequestHost_ContributesNoWebSocketSource` - 5 cases including a host carrying
    `; script-src 'unsafe-inline'`, a path, a non-numeric port, a non-http scheme, and empty.
11. `MemberMap_BuildsPopupAvatarsWithoutAnInlineHandler` - the named 025 regression case.
12. `ApplicationJavaScript_BuildsNoMarkupCarryingInlineEventHandlers` - the aggregated
    repository-wide sweep, in the 023/024 style.
13. `NoWebFontServiceIsReferenced` - the deleted preconnect stays deleted and no `@font-face`
    appears in `wwwroot/css`, which is what keeps `font-src 'self'` honest.

**Extended - `SecurityHeaderTests`, +7 cases** (delivery, not contents):
- `Page_CarriesExactlyOneEnforcedContentSecurityPolicy` (3 paths) - `text/html`, exactly one
  `Content-Security-Policy`, **no** `Content-Security-Policy-Report-Only`, and the key directives
  present.
- `Page_AllowsTheBlazorCircuitOnTheRequestHost` - the `ws`/`wss` source matches the actual
  request authority.
- `NonDocumentResponse_CarriesNoContentSecurityPolicy` (3 paths, incl. `/service-worker.js`).
- `Headers_AreSetOnce_NotAppendedPerPass` now also asserts a single CSP header.

Directive **ordering** is deliberately not asserted anywhere - the implementation does not depend
on it.

## Latest validation (unit 025)
| Check | Result |
| --- | --- |
| `dotnet build RTUB.sln -c Release` | **Succeeded, 0 warnings, 0 errors** |
| `RTUB.Core.Tests` | 791 total, **0 failed**, 0 skipped |
| `RTUB.Shared.Tests` | 768 total, **0 failed**, 2 skipped |
| `RTUB.Application.Tests` | 1997 total, **0 failed**, 0 skipped |
| `RTUB.Web.Tests` | 846 total, **0 failed**, 56 skipped |
| `RTUB.Integration.Tests` | 281 total, **0 failed**, 2 skipped |
| **Total** | **4683 tests, 0 failures, 60 skipped** |
| `node --check` on both changed JS files | clean |
| `git diff --check` | clean |
| Migration / snapshot files touched | **none** |
| Credential scan over diff + new files | clean - only synthetic placeholders (`pub-test.r2.dev`, `abc123`, `localtestaccount`) |

**The expected 024 baseline failure did NOT reproduce.**
`BetServiceTests.PlaceBetAsync_WithInsufficientBalance_ThrowsException` **passed** here, both in
the full 1997-test `RTUB.Application.Tests` run and in isolation. 024 recorded it as failing in a
full-project run because of shared-fixture ordering; on this Release run it did not. Nothing in
025 touches `BetService` or any test fixture, so this is the pre-existing flake behaving
differently, not a fix and not a regression. **No new failure was introduced by 025.**

Test runner: the native executables were used (`tests/<proj>/bin/Release/net10.0/<proj>.exe`),
filtered with `-class` / `-method`. The `dotnet test` driver remains broken for xUnit v3 in this
repo - see *Deferred*. 025 did not attempt to fix it.

## Static scan (unit 025) - the acceptance gate
Run against the whole repository, application-owned files only:
| Thing | Count |
| --- | --- |
| Inline executable `<script>` in browser-served markup | **0** |
| Inline `on*=` handlers in browser-served markup | **0** |
| **Inline `on*=` handlers inside JS-built markup strings** | **0** (was 1: `memberMap.js`) |
| `style="..."` in browser-served markup | **0** |
| `<style>` elements in browser-served markup | **0** |
| `style.cssText` / `setAttribute('style')` / injected `<style>` in app JS | **0** |
| `JSRuntime` `eval` dispatches | **0** |
| `'unsafe-inline'` / `'unsafe-eval'` / `'unsafe-hashes'` in the policy | **0** |

## Browser validation (unit 025) - RUN, enforced policy, not report-only
Headless Chromium against a local Development build, with the real security header enforced. The
`Cloudflare:R2:*` values were supplied as environment variables for the run only; **no hostname
was written into source, and the local `app.db` is gitignored.**

**Zero CSP violations across every exercised surface.** Violations were collected two ways at once
- a `securitypolicyviolation` listener installed before page script, and the console - so a
violation could not be missed by either channel.

**Public / anonymous, desktop 1280 and mobile 375:** `/`, `/login`, `/music`, `/gallery`,
`/events`, `/roles`, `/calotes`, `/privacy`, plus the auth redirects for `/images`, `/leaderboard`,
`/naipes`, `/documentation`, `/hall-of-fame`, `/members`, `/member/map`. All clean; no horizontal
overflow at 375.

**Blazor circuit:** connects (`Blazor` global present, reconnect modal never shown), and
client-side navigation works over it (`/` -> `/music`). No WebSocket rejection - `connect-src`
carried `ws://localhost:5199`.

**Third-party, all loaded under the policy:** `Cropper` (function), Leaflet `L` (object), `PIXI`
(object), `bootstrap` (object). 9 Carto tiles fetched on the map, which exercises
`img-src https://*.basemaps.cartocdn.com`.

**memberMap popup - the 025 regression case, driven end to end.** `/member/map` needs
authentication, so the popup builder was driven directly on `/`, where `memberMap.js` is already
loaded, with synthetic city data and a deliberately missing avatar URL. Result: popup rendered,
`onerror` attribute **absent**, `data-avatar-fallback` consumed by the listener, and
`img.src` swapped to `/images/default-avatar.webp` - **with zero CSP violations**. The delegated
listener handles dynamically inserted popup images exactly as intended.

**Directive probes** (the auth-gated media paths, proven without credentials): an inline
`onclick` injected via `innerHTML` **did not run** and raised `script-src-attr`; an injected
`style="width:123px"` was **blocked** (`style-src-attr`, computed width stayed `auto`) while
`el.style.setProperty('--probe','7px')` **applied** - 024's CSSOM distinction still holds under the
real header. Cross-origin `img` / `audio` / `iframe` to an unlisted origin were **blocked**; the
configured R2 public and endpoint origins were **allowed** in `img-src`, `media-src` and
`frame-src`; and the R2 public origin was **rejected as a script source**, confirming it is not
over-granted.

**R2, A/B proven.** A first run with a *deliberately mismatched* configured public origin produced
`img-src` violations for the homepage's real R2 slideshow images. Re-running with the origin that
matches the data produced **zero**. That is direct evidence that the configured value, and only
it, admits R2 content. The bucket itself answers **401** to this machine, so nothing decodes
locally - a network/permission fact, not a CSP outcome, and the absence of any `img-src` violation
is what the policy is responsible for.

**PWA:** service worker registers and activates (`scope /`, state `activated`), `manifest.webmanifest`
200 with 10 icons, and `/service-worker.js` carries **no** CSP header. The worker's caches
(`rtub-static/dynamic/images-rtub-v2.6.0`) hold 5 cross-origin entries - the exact traffic a
globally scoped policy would have broken.

**Offline page, with the origin actually down:** `/offline.html` renders from cache with its
external stylesheet only - 1 linked sheet (`/css/offline.css`), 9 rules, **0 `<style>` elements,
0 inline style attributes** - heading "Sem Conexão", the `135deg` gradient and the `8px` button
radius all resolving. Identical online and offline. `offline.js` loads from cache and runs (its
`navigator.onLine` redirect to `/` fires), which is why the DOM had to be snapshotted at
`domcontentloaded`; 024 already recorded that in-page origin probing is meaningless behind a
service worker that answers 200 from cache.

**Not browser-tested, and why:** no authenticated session was available and **no credentials were
invented or requested**. That leaves the *rendered* audio player and the two PDF iframes
(`/music` album detail, `/roles` RGI) unexercised as real pages. Both directives were instead
proven by direct probe as described above, and the R2 bucket returns 401 to this machine anyway,
so a logged-in run here would not have loaded the media either. **An authenticated smoke run in a
real environment is the one outstanding confirmation.**

## Blockers found by 025
**None outstanding.** Two things were found and dealt with inside the unit:
1. **024's R2 plan was incomplete.** It named only `Cloudflare:R2:PublicUrl`; the pre-signed audio
   and PDF URLs actually resolve against the S3 API endpoint. Both origins are now derived, each
   only in the directives that use it.
2. **`memberMap.js`'s JS-built `onerror`**, the blocker 024 flagged. Fixed, and the class of bug
   is now swept repository-wide rather than pinned file by file.

**Remaining security debt after 025:** password policy (`RequiredLength = 4`, owner-skipped) and
the historical GitGuardian incidents (owner action). Neither is a CSP concern. **The CSP /
security-header track itself is complete.**
