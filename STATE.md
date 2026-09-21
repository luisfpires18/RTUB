# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **019 (reduce authentication cookie-validation SQLite pressure) - COMPLETE,
uncommitted, awaiting owner review.** Unit 018 is merged to `dev` at `0492cc81`.

## Branch
`perf/019/cookie-validation-db-pressure`, branched from `dev` (clean, in sync with `origin/dev` at
`0492cc81`). Uncommitted - no commit authorized.
`chore/001`-`chore/011`, `fix/012`-`fix/014`, `chore/015`, `fix/016`-`fix/018` still present;
delete when convenient.

## Owner decision (2026-09-21) - Option A
**`LastLoginDate` keeps its activity semantics.** It is not renamed, not split, not restricted to
login time. The only change is that the write is now throttled to **one per 5 minutes per user**,
which is the throttle PR #185's comment already claimed to have. Options B and C (login-only, with
or without a new `LastActivityAt` column) were rejected: both change what the presence UI shows.

## Last completed step
**Unit 019 - the cookie-validation `LastLoginDate` write is throttled; every security check still
runs on every request.** The investigation that led here is preserved below, because the throttle
only makes sense against it.

### What changed
`AddCookieAuthenticationServices` (`ServiceCollectionExtensions.cs`) now, after the security checks
and before the write:
1. reads the user id from `ClaimTypes.NameIdentifier` (no extra SELECT - it is already in the
   cookie);
2. returns immediately if `activity-lastlogin:{userId}` is present in `IMemoryCache`;
3. otherwise performs the existing raw `UPDATE`, and **only once it has succeeded** caches that key
   for `ActivityWriteThrottle`. A failed update leaves the key unset so the next request retries.

Two new members on the same class, both public so tests can pin them:
- `ActivityWriteCachePrefix = "activity-lastlogin:"`
- `ActivityWriteThrottle = TimeSpan.FromMinutes(5)`

**The throttle key is deliberately separate from the authentication-log key**
(`login-log:{userName}:{issuedUtc.Ticks}`). One limits how often a line is logged, the other how
often a row is written; they are different concerns and the log key is keyed by *username* while
this one is keyed by *user id*, as instructed.

Also corrected narrowly, no behaviour change: the retry's `ex.SqliteErrorCode == 6` now carries a
comment saying 6 is **`SQLITE_LOCKED`**, not `SQLITE_BUSY` (5) - `SQLITE_BUSY` is already absorbed
by `PRAGMA busy_timeout = 30000` in `SqliteConnectionInterceptor`, which does not cover
`SQLITE_LOCKED`. The stale log message "Error while **initializing** LastLoginDate" (a leftover from
the one-time-backfill version) now reads "updating". No other SQLite change - the retry loop and
the raw SQL both stay.

### Before / after - measured the same way, by test
Five consecutive authenticated `GET /Events` on one session:

| | `LastLoginDate` writes | `AspNetRoles` SELECTs | `AspNetUserRoles` SELECTs |
| --- | --- | --- | --- |
| before | **5** | 5 | 5 |
| after | **1** | 5 | 5 |

Security reads are **unchanged and still per request**, by design. Only the write was removed.
Per-request cost drops from 4 database operations to 3, and from 1 write to 0 on every request
inside the window. The avatar-grid multiplier noted below collapses to a single write per user per
5 minutes regardless of how many `/images/*` requests a page makes.

### Cookie-validation frequency - measured, not inferred
`AddIdentity` wires the application cookie with
`OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync`.
`AddCookieAuthenticationServices` then assigns **a whole new `CookieAuthenticationEvents` object**,
which **replaces** that delegate outright. Consequences:

- `SecurityStampValidatorOptions.ValidationInterval` (**default 30 minutes**) is **never consulted**
  - nothing in the repo configures it and nothing reads it. The stock validator skips the database
  entirely when `now - Properties.IssuedUtc <= ValidationInterval`; RTUB's handler has no such gate.
  **This was left exactly as it was** - lowering the security-check frequency was out of scope.
- `SecurityStampVerified`'s principal refresh (`ReplacePrincipal` + `ShouldRenew = true`) is also
  gone, so a legitimately refreshed stamp is a forced logout rather than a claims refresh. Deferred.
- `OnValidatePrincipal` fires **once per HTTP request per cookie scheme** - `HandleAuthenticateAsync`
  is memoised per request by `HandleAuthenticateOnceAsync`, so repeat `[Authorize]`/`User` reads in
  the same request are free.

Measured per request kind, signed in, via an EF `DbCommandInterceptor` in the test host (the write
column is the first request of a session; subsequent ones inside the window are 0):

| Request | SQL | `LastLoginDate` writes | Role lookups |
| --- | --- | --- | --- |
| `GET /` | 57 | 1 | 1 |
| `GET /Events` | 14 | 1 | 1 |
| `GET /images/...` (**404**) | 4 | 1 | 1 |
| `GET /favicon.ico`, `/css/site.css`, `/service-worker.js` | 0 | 0 | 0 |

Static assets are free because `UseStaticFiles` is registered at `Program.cs:363`, **before**
`UseAuthentication()` at `Program.cs:414`. `/images/*` is deliberately excluded from that branch so
`ImagesController` can add ETags, so every image request - including a 404 - still runs a full
cookie validation. The throttle is what stops that costing a write per tile.

### `LastLoginDate` semantics - resolved by history, and it is NOT "last login"
- `bacd166c` ("LOGIN UPDATED ON DB") introduced the cookie-validation write as a **one-time
  backfill**: `... WHERE Id = {userId} AND LastLoginDate IS NULL;`, commented *"Atomic,
  concurrency-safe 'set once if null'"*.
- `7293f32d` (**PR #185**, "Fix LastLoginDate not updating on cookie validation") **deleted the
  `AND LastLoginDate IS NULL` predicate on purpose**, with the new comment: *"Update LastLoginDate
  to track user activity (both normal login and cookie validation) - **This is throttled by the
  cache above to prevent excessive DB writes**"*. The throttle claim was false: the cache entry
  guarded only the log line. Unit 019 makes it true.

The field means **"last authenticated request"**, and the UI depends on it:

| Consumer | Use |
| --- | --- |
| `LoginStatusBadge.razor` | `ONLINE_THRESHOLD_HOURS = 1` -> "Online"/"Recente"/"Esta Semana"/"Este Mes"/"Inativo" |
| `AvatarCard.razor:306`, `UserCard.razor:107` | green online dot, `< 1 h` |
| `UserRoles.razor:427` | sorts the member list by online-first |
| `UserProfileService:93`, `LoginStatisticsButton.razor` | login-statistics report |
| `AuditLogAppender.cs:41` | excluded from audit logs - *"already logged separately"* |

Every one of those buckets at an hour or coarser, so a 5-minute write window is invisible to all of
them. `Program.cs:489` still writes the field once per real login, unchanged.

**The naming mismatch remains technical debt.** The column is called `LastLoginDate` and means
"last activity". Renaming it needs a migration and an edit to every consumer above; it was
explicitly excluded from this unit. Carried in *Deferred*.

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
None active. Unit 019 is complete and awaiting owner review.

## Next unit
**Microsoft 10.0.11 -> 10.0.12 servicing train** across `src/` + tests, which also unblocks
`MockQueryable.Moq 10.0.12`.
Next *security* unit: **password policy** - Identity is currently `RequiredLength = 4` with every
complexity rule off (`AddIdentityServices`). Then security headers / CSP.
New *auth-cleanup / design* unit, raised by 019 - see *Deferred*.

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
- **RTUB's `OnValidatePrincipal` replaces Identity's `SecurityStampValidator` entirely** - a whole
  new `CookieAuthenticationEvents` object is assigned, so the stock delegate never runs. Two
  consequences, both still live and both deliberately untouched by 019:
  - `SecurityStampValidatorOptions.ValidationInterval` (default 30 min) is never consulted, so the
    stamp is checked on **every** request rather than every 30 minutes. Stricter than the framework
    default, and deliberately left that way.
  - the stock success path's principal refresh (`CreateUserPrincipalAsync` -> `ReplacePrincipal` +
    `ShouldRenew = true`) is lost, so a legitimately refreshed stamp logs the user out instead of
    refreshing their claims.
  **A future auth-cleanup/design unit should decide whether to compose with the framework validator
  rather than replace it.** It cannot simply be swapped in: `RemoveFromRoleAsync` does not bump the
  security stamp, so the stock validator would miss Admin-role revocation. `AdminRoleRemoved_RejectsCookie`
  pins that. Do not weaken the current checks to regain the framework refresh.
- **The `SQLITE_LOCKED` retry loop was kept**, now correctly commented. The `IsInRoleAsync` pair is
  still 2 SELECTs and could be one join - not done, out of 019's scope.
- Known and accepted framework limitation (from 012): antiforgery tokens are bound to the user
  identity, so multiple tabs signed in as different users are unsupported. Documented ASP.NET Core
  behavior, not an RTUB defect.

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

## Relevant files (unit 019)
Changed (3):
- `src/RTUB.Web/Extensions/ServiceCollectionExtensions.cs` - `ActivityWriteCachePrefix` and
  `ActivityWriteThrottle` constants; the throttle check, the post-success `cache.Set`, the
  `SQLITE_LOCKED` comment and the corrected log message, all inside `OnValidatePrincipal`.
  **The only production file touched.**
- `tests/RTUB.Integration.Tests/CookieValidationTests.cs` - **new**, 10 tests plus the recording
  factory, `SqlRecorder` and `RecordingCommandInterceptor`.
- `tests/RTUB.Integration.Tests/TestWebApplicationFactory.cs` - one added member,
  `protected string ConnectionString`, so a derived factory can re-register the context against the
  same database in order to attach an interceptor. Nothing else touched.

Read and deliberately **not** changed: `Program.cs` (pipeline order, login write),
`SqliteConnectionInterceptor.cs`, and every presence consumer listed above.

## Tests (10 new, 0 removed, 0 changed elsewhere)
All in `CookieValidationTests`, all green. Cost assertions match on the raw, unquoted
`UPDATE AspNetUsers` the handler emits - EF Core quotes identifiers, so nothing else collides.

**Throttle and cost:**
1. `ValidCookie_IsAccepted_AndFirstRequestCostsOneWritePlusTwoRoleReads` - the first request of a
   session writes once and is not rejected.
2. `RepeatedAuthenticatedRequests_WriteLastLoginDate_OnlyOncePerThrottleWindow` - 5 requests ->
   **1** write, but **5** `AspNetRoles` and **5** `AspNetUserRoles` reads, and all five responses
   are 200 with no cookie deletion. This is the before/after test and the "still authenticated"
   test in one.
3. `ThrottleIsKeyedPerUser_NotShared` - two users, two requests each -> 2 writes.
4. `ActivityWriteThrottle_IsFiveMinutes` - pins the constant and the key prefix. Real expiry is
   **not** exercised: a controllable cache clock would be disproportionate here, so the cache-hit
   path is what the tests cover, as instructed.
5. `StaticFileRequest_SkipsCookieValidation` - `/service-worker.js` -> 0.
6. `ImageControllerRequest_CostsAFullCookieValidation_EvenWhenTheImageIsMissing` - a 404 -> 1 write.

**Security - each one now arms the throttle with a warm-up request first, so it proves rejection
still happens on a request that skips the write:**
7. `SecurityStampChange_RejectsCookie`.
8. `ExpelledUser_RejectsCookie` - uses `UpdateAsync`, which leaves the stamp alone, so it exercises
   the expulsion branch specifically.
9. `AdminRoleRemoved_RejectsCookie` - `RemoveFromRoleAsync` does **not** bump the security stamp, so
   the stock `SecurityStampValidator` would not catch this. Only RTUB's own role probe does.
10. `Login_SetsLastLoginDate`.

Rejection is asserted by the rejecting response deleting `.AspNetCore.Identity.Application`.

## Latest validation (unit 019)
- Release build, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true` and
  `EnforceCodeStyleInBuild=true`.
- Focused first: `CookieValidationTests` alone - **10 total, 10 passed**.
- Full suite, `dotnet test --no-build -c Release`: **4539 passed, 0 failed, 60 skipped**
  (total 4599). The investigation pass of 019 took the branch to 4537 / 0 / 60 with 8 tests; this
  pass added 2 (`ThrottleIsKeyedPerUser_NotShared`, `ActivityWriteThrottle_IsFiveMinutes`) and
  rewrote 1 in place. **4537 + 2 = 4539.** Against `dev`'s 4529, the branch is **+10**.
- `git diff --check`: clean.
- Diff credential scan: no match. The tests use `TestSecret.NewPassword()`; no literal.
- **No migration and no model-snapshot change** - the throttle is cache-only and no column moved.
- No frontend build, no Graphify rebuild - no application structure changed.
