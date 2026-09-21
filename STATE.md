# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **020 (restore Identity cookie-refresh semantics without weakening RTUB's
security checks) - COMPLETE, uncommitted, awaiting owner review.** Unit 019 is merged to `dev` at
`b45459ec`.

## Branch
`fix/020/identity-cookie-validation`, branched from `dev` (clean, in sync with `origin/dev` at
`b45459ec`). Uncommitted - no commit authorized.
`chore/001`-`chore/011`, `fix/012`-`fix/014`, `chore/015`, `fix/016`-`fix/018`,
`perf/019` still present; delete when convenient.

## Owner decision (2026-09-21) - Option A
**`LastLoginDate` keeps its activity semantics.** It is not renamed, not split, not restricted to
login time. The only change is that the write is now throttled to **one per 5 minutes per user**,
which is the throttle PR #185's comment already claimed to have. Options B and C (login-only, with
or without a new `LastActivityAt` column) were rejected: both change what the presence UI shows.

## Last completed step
**Unit 020 - RTUB's cookie handler now *calls* Identity's `SecurityStampValidator` instead of
replacing it. Every RTUB check still runs on every request; the framework's principal refresh and
cookie renewal are back.**

### Why the custom handler replaced Identity's in the first place
`AddIdentity` wires the application cookie with
`OnValidatePrincipal = SecurityStampValidator.ValidatePrincipalAsync`.
`AddCookieAuthenticationServices` assigned **a whole new `CookieAuthenticationEvents` object**,
which replaced that delegate outright. It did so to add three rules Identity does not have, all of
which had to be **immediate** rather than interval-gated:
- **expulsion** - `IsExpelled` is an RTUB column; Identity knows nothing about it;
- **Admin-role consistency** - `UserManager.RemoveFromRoleAsync` does **not** bump the security
  stamp, so the stock validator never notices a revoked Admin role;
- login/activity logging and the `LastLoginDate` activity write (unit 019).

Replacing the delegate was the cheap way to get them, and it cost two framework behaviours: the
success-path principal refresh, and cookie renewal. Unit 019 recorded both as deferred.

### What changed (one production file, one call)
`OnValidatePrincipal` now runs, in this order:
1. security-stamp check -> reject
2. expelled check -> reject
3. Admin-consistency check -> reject
4. **`await SecurityStampValidator.ValidatePrincipalAsync(context);` then
   `if (context.Principal is null) return;`**
5. throttled login logging + throttled `LastLoginDate` write (unit 019, untouched)

Step 4 resolves the configured `ISecurityStampValidator` from DI (`AddIdentity` registers
`SecurityStampValidator<ApplicationUser>` scoped) and runs the real framework validator. Nothing
of Identity is reimplemented and no subclass was added.

### The ordering is the design, and it is deliberate
The framework call goes **last**, not first. Verified against the .NET 10 source
(`src/Identity/Core/src/SecurityStampValidator.cs`):

- `ValidateAsync` computes `validate = timeElapsed > Options.ValidationInterval` and, when that is
  false, **returns without touching the database or the principal**. Putting RTUB's checks behind
  it would have diluted all three to the 30-minute interval.
- On the refresh path it calls `SecurityStampVerified` -> `SignInManager.CreateUserPrincipalAsync`
  -> `context.ReplacePrincipal(newPrincipal)` + `ShouldRenew = true`. That rebuild **scrubs the
  stale `Admin` claim** the role probe exists to catch. Running the framework first would have
  silently downgraded such a session instead of rejecting it - and only on the requests where a
  refresh happened to fall due. `AdminRoleRemoved_IsStillRejectedImmediately_WhileRefreshIsDue`
  pins that this does not happen.
- `CookieValidatePrincipalContext.RejectPrincipal()` is `Principal = null`, which is why the null
  check after the call is the correct rejection test.
  `CookieAuthenticationHandler.HandleAuthenticateAsync` then returns `NoPrincipal`; on
  `ShouldRenew` it calls `RequestRefresh(ticket, context.Principal)` and `FinishResponseAsync`
  emits the new `Set-Cookie`.

### Validation cadence - exact
| Check | Cadence | Changed by 020? |
| --- | --- | --- |
| RTUB security-stamp verification | **every request** | no |
| RTUB expelled check | **every request** | no |
| RTUB Admin-consistency check | **every request** | no |
| Login logging | every request, log line throttled 1 h per cookie | no |
| `LastLoginDate` write | throttled, 1 per user per 5 min (unit 019) | no |
| Framework principal rebuild + `ShouldRenew` | every `ValidationInterval` (**30 min, framework default, unchanged**) | **restored** |

**No check became less frequent.** The framework default was not touched - RTUB simply keeps a
stricter stamp check in front of it. **The stop condition in the brief was not reached**: nothing
moved from per-request to 30 minutes.

### Claim / role refresh - what actually works
- `UserManager.AddToRoleAsync` / `RemoveFromRoleAsync` do **not** touch the security stamp. Role
  changes therefore reach a live session through the framework rebuild, at the next validation
  interval - now that the rebuild runs again. Before 020 they never reached it at all: a session's
  claims were frozen at the moment the cookie was issued.
- `UserManager.UpdateSecurityStampAsync` is **not** a refresh. In stock Identity it makes
  `VerifySecurityStamp` return null, i.e. it is the *revocation* path and forces a re-login. RTUB
  already calls it on role change in `RoleManagementService.cs:121` and `UserRoles.razor:613`; the
  comment at the latter ("force fresh cookies and token refresh") is misleading, and the behaviour
  is a forced logout. Left alone - out of scope, recorded in *Deferred*.
- Admin **revocation** stays RTUB's immediate rejection, not a claim refresh.

### DB cost - measured before and after, same harness
One authenticated `GET /Events`, throttle already armed, via the `DbCommandInterceptor` in
`CookieValidationFactory`. "before" = the same probe run with the production file reverted to `dev`.

| | total SQL | `AspNetRoles` | `AspNetUserRoles` | `AspNetUserClaims` | `LastLoginDate` writes |
| --- | --- | --- | --- | --- | --- |
| before (dev) - any request | 13 | 1 | 1 | 0 | 0 |
| after - inside `ValidationInterval` | **13** | 1 | 1 | 0 | 0 |
| after - refresh due | **15** | 2 | 2 | 1 | 0 |

- **Inside the interval the cost is byte-for-byte unchanged.** The framework call returns before
  any database work.
- A refresh costs **+2 commands**: one role join and one user-claims SELECT, from
  `CreateUserPrincipalAsync`. Once per user per 30 minutes.
- The framework's `VerifySecurityStamp` repeats RTUB's stamp check but **costs no SELECT**:
  `UserManager.GetUserAsync` resolves off the request-scoped `DbContext`'s change tracker, which
  RTUB's own check has already populated. Subclassing `SecurityStampValidator<ApplicationUser>` to
  deduplicate it would have bought nothing, so it was not done.
- Role queries were **not** optimised - out of scope, and nothing eliminated them naturally.

### Carried over unchanged from unit 019 (still true, still load-bearing)
- `LastLoginDate` means **"last authenticated request"**, not "last login". The presence UI
  (`LoginStatusBadge`, `AvatarCard:306`, `UserCard:107`, `UserRoles:427`, `UserProfileService:93`,
  `LoginStatisticsButton`) buckets at an hour or coarser, so the 5-minute write throttle is
  invisible to all of it. Owner decision Option A above. Naming remains debt - see *Deferred*.
- `OnValidatePrincipal` fires **once per HTTP request per cookie scheme** -
  `HandleAuthenticateAsync` is memoised by `HandleAuthenticateOnceAsync`.
- Static assets cost nothing: `UseStaticFiles` (`Program.cs:363`) precedes `UseAuthentication()`
  (`Program.cs:414`). `/images/*` is excluded from that branch so `ImagesController` can add
  ETags, so every image request - including a 404 - still runs a full cookie validation.
- The `SQLITE_LOCKED` (6) retry loop around the activity write, and its corrected comment, stand.

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
None active. Unit 020 is complete and awaiting owner review.

## Next unit
**Password-policy review / hardening.** Identity is currently `RequiredLength = 4` with every
complexity rule off (`AddIdentityServices`, `ServiceCollectionExtensions.cs`). Then security
headers / CSP.
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

## Relevant files (unit 020)
Changed (3):
- `src/RTUB.Web/Extensions/ServiceCollectionExtensions.cs` - **the only production file touched.**
  One `await SecurityStampValidator.ValidatePrincipalAsync(context);` plus the `context.Principal
  is null` guard inside `OnValidatePrincipal`, after the three security checks and before the
  logging/activity block, with the ordering rationale in comment; `AddCookieAuthenticationServices`
  XML summary updated. No other method touched.
- `tests/RTUB.Integration.Tests/IdentityCookieRefreshTests.cs` - **new**, 7 tests,
  `RefreshingCookieFactory`, and `CookieTestSession` (the shared sign-in / `Set-Cookie` helpers).
- `tests/RTUB.Integration.Tests/CookieValidationTests.cs` - 1 test added; the sign-in helper and
  the cookie predicate moved to `CookieTestSession` so the two classes cannot drift
  (**-57 duplicated lines**). No existing assertion or count changed.

Read and deliberately **not** changed: `Program.cs`, `AddIdentityServices` (password policy is the
next unit), `RoleManagementService.cs`, `UserRoles.razor`, `TestWebApplicationFactory.cs`.

Framework source read for this unit (.NET 10, `release/10.0`): `SecurityStampValidator.cs`,
`SecurityStampValidatorOptions.cs`, `IdentityServiceCollectionExtensions.cs`,
`CookieValidatePrincipalContext.cs`, `CookieAuthenticationHandler.cs`, `SignInManager.cs`.

## Tests (8 new, 0 removed, 0 existing assertions changed)
**`IdentityCookieRefreshTests` (new, 7).** `RefreshingCookieFactory` derives from
`CookieValidationFactory` - same database, same SQL recorder - and sets
`SecurityStampValidatorOptions.TimeProvider` to a +31-minute offset clock, so every request is one
on which the refresh falls due. **The clock is moved, not the interval**, so the tests exercise the
real 30-minute default rather than a value invented for them.
1. `ValidationIntervalElapsed_RenewsTheCookie_AndKeepsTheSessionAuthenticated` - asserts a real
   non-deleting `Set-Cookie`, not an implementation detail.
2. `RoleAddedWithoutStampChange_ReachesTheLiveSession_AtTheNextValidation` - user without `Owner`
   gets **403** from `POST /api/push/broadcast`; `AddToRoleAsync` only (**no stamp bump, no claim
   surgery in the test**); the next request is authorised and reaches the action body (**400**,
   "Web Push is not configured"). No re-login.
3. `AdminRoleRemoved_IsStillRejectedImmediately_WhileRefreshIsDue` - **the ordering proof.**
4. `ExpelledUser_IsStillRejectedImmediately_WhileRefreshIsDue`.
5. `SecurityStampChange_IsStillRejectedImmediately_WhileRefreshIsDue`.
6. `RefreshRequest_StillWritesLastLoginDate_OnlyOncePerThrottleWindow` - 5 requests -> **1** write.
7. `RefreshRequest_CostsTwoExtraSelects_ToRebuildThePrincipal` - pins 2 / 2 / 1.

**`CookieValidationTests` (1 added, now 11).**
8. `WithinTheValidationInterval_TheFrameworkValidatorCostsNothing_AndDoesNotRenew` - no `Set-Cookie`
   inside the interval; the existing exact-count tests around it are the rest of the proof that
   020 added a framework call and not one database command.

All 10 unit-019 tests still pass **unmodified**, including the three immediate-rejection ones.
`TestSecret.NewPassword()` throughout; no credential literal.

## Latest validation (unit 020)
- Release build, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true` and
  `EnforceCodeStyleInBuild=true`.
- Focused first: `CookieValidationTests` + `IdentityCookieRefreshTests` - **18 total, 18 passed**.
- Full suite, `dotnet test --no-build -c Release`: **4547 passed, 0 failed, 60 skipped**
  (total 4607). Against the post-019 `dev` baseline of **4539**, the branch is **+8** - exactly the
  8 tests listed above, 7 + 1.
- `git diff --check`: clean.
- Diff credential scan: no match.
- **No migration and no model-snapshot change** - no column, no entity, no `DbContext` edit.
- No frontend build, no Graphify rebuild - no application structure changed.
- No unrelated auth refactor: password policy, MFA, rate limiting, `UseHttpsRedirection`, CSP and
  the role-query shape were all left exactly as they were.
