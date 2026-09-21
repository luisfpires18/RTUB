# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **017 (make `ResetDevDataAsync` explicit and safe) - implementation complete,
uncommitted, awaiting owner review.** Unit 016 is merged to `dev` at `6f9f9d8c`.

## Branch
`fix/017/development-data-reset-safety`, branched from `dev` (clean, in sync with `origin/dev` at
`6f9f9d8c`). Uncommitted - no commit authorized.
`chore/001`-`chore/011`, `fix/012`-`fix/014`, `chore/015` and `fix/016` still present; delete when
convenient.

## Last completed step
**Unit 017 - the development-data reset is now opt-in, Development-only and free of hardcoded
credentials.** The default startup no longer modifies any existing user's credentials.

### Before / after
`SeedData.InitializeAsync` called `ResetDevDataAsync` on every startup, before the existing-user
early return. The guard was only `if (environment.IsProduction()) return;`, so **Development,
Test *and* Staging** ran it automatically: it cleared `PushSubscriptions`, rewrote **every**
user's `PasswordHash` to one shared hardcoded hash via raw SQL, and normalised every email. Two
live problems: it silently undid unit 016's configured seed passwords on the next local startup,
and a future Azure DEV App Service running as `Staging` would have reset every user's credentials
on every restart.

The feature itself is useful, so it was **not deleted**. It is now gated twice:

| Environment | `DevelopmentDataReset:Enabled` | Result |
| --- | --- | --- |
| Production | anything | **never runs** |
| Staging | anything | **never runs** |
| Test | anything | **never runs** on the normal host path |
| Development | absent / `false` / unparseable | no reset, no password required |
| Development | `true` | password required, validated, then reset |

The environment check (`!environment.IsDevelopment()`) comes **first**, so no configuration value
can switch it on outside local Development.

### Configuration
```
DevelopmentDataReset:Enabled    /  DevelopmentDataReset__Enabled
DevelopmentDataReset:Password   /  DevelopmentDataReset__Password
```
No hardcoded fallback, no hardcoded `PasswordHash`, no generated-and-lost password. `Enabled` is
read with `bool.TryParse`, so absent / blank / garbage all resolve to **off** - the safe direction.

### Fail-closed / atomic
`DevelopmentDataReset:Password` is validated **before the first mutation**, reusing unit 016's
`RequireSeedPassword`. Missing, null, empty, whitespace or a documented placeholder
(`your-admin-password`, `changeme`, `change-me`, `password`, case-insensitive) throws
`InvalidOperationException` naming both `DevelopmentDataReset:Password` and
`DevelopmentDataReset__Password`. **The supplied value never appears in the message or any log**
- asserted by a test. A missing password therefore leaves push subscriptions, passwords and emails
exactly as they were; there is no partially-reset database.

### Password reset mechanism - no more raw SQL
The `UPDATE AspNetUsers SET PasswordHash = '<literal>'` is gone. Each user now goes through
Identity's own password-reset flow, in **one** call:
`GeneratePasswordResetTokenAsync` then `ResetPasswordAsync`.

**Deliberately not `RemovePasswordAsync` + `AddPasswordAsync`.** That is a two-step credential
mutation: a failure between the two steps would leave the account with **no password at all**.
`ResetPasswordAsync` verifies the token and validates the new password *before* it writes, so a
rejected reset leaves the existing hash untouched.

Every `IdentityResult` is checked explicitly. A failed reset throws `InvalidOperationException`
naming the affected `UserName` and the Identity error `Code: Description` pairs — those never echo
the password or the reset token, and neither does anything logged.

`ResetPasswordAsync` routes through `UpdatePasswordHash`, so the configured `IPasswordHasher`
produces the hash and **the security stamp is rotated**; authentication cookies issued before a
reset stop validating. A test asserts the stamp changes. The web host already supplies the
password-reset token provider via `AddDefaultTokenProviders()`
(`ServiceCollectionExtensions.cs:639`); the tests register the same `DataProtectorTokenProvider`
explicitly.

`DELETE FROM PushSubscriptions` became `RemoveRange` over a materialised list - provider-agnostic
(works on the InMemory provider the tests use), audited like any other delete, and no change
tracker mutation mid-enumeration.

Email normalisation to `{UserName}@rtub.pt` stays, but is assigned on the entity (plus
`NormalizeEmail` for `NormalizedEmail`, persisted by its own checked `UpdateAsync`) rather than via
`UserManager.SetEmailAsync` - **deliberate**: `SetEmailAsync` clears `EmailConfirmed`, and
`SignIn.RequireConfirmedAccount = true` (`ServiceCollectionExtensions.cs:627`), so using it would
lock every dev account out. A test pins `EmailConfirmed` staying `true`. The email is written only
**after** the password reset succeeds, so a rejected reset rewrites nothing.

### Visibility
`ResetDevDataAsync` went from `private` to `internal` (the project already declares
`InternalsVisibleTo RTUB.Application.Tests`), which is the explicit test-specific path required.
Nothing else in `src/` can call it, and the `IsDevelopment()` guard keeps it inert on the normal
`TestWebApplicationFactory` startup path (that host runs as `Test`).

### Interaction with unit 016 - proven
`FullSeed_ThenDefaultDevelopmentStartup_KeepsBothConfiguredSeedPasswords` runs the **real** bulk
seed (`isEmptyDb: false`, 82 members) so the database is in the genuine post-016 state, then
replays what the next Development startup does with no `DevelopmentDataReset` section configured
at all. The Owner still authenticates with `AdminUser:Password`, the member `nabo` still
authenticates with `SeedData:MemberPassword`, and the Owner's email is not rewritten.

### Files changed (3)
- `src/RTUB.Application/Data/SeedData.cs` - `ResetDevDataAsync` rewritten (guards, config,
  validation, UserManager-based reset, EF delete, logger); its call site passes `configuration`
  and `userManager` and gained a two-line comment. **Nothing else in the file changed** - the
  `isEmptyDb` manual switch and every other seed step are untouched.
- `README.md` - new "Resetting a local development database (destructive, opt-in)" section under
  Local Development, plus a "Development Data Reset" entry in the environment-variable list.
- `tests/RTUB.Application.Tests/Data/DevelopmentDataResetTests.cs` - **new**, the only new file.

### Not changed, by instruction
Password policy, MFA, the `TestWebApplicationFactory` SQLite race, Azure provisioning, CI/CD, the
`isEmptyDb` manual switch, seed-mode shape, email architecture, push architecture. No migration
and no model-snapshot change - none was needed.

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
None active.

## Next unit
**The `TestWebApplicationFactory` SQLite startup race** - serialize factory startup before the
hosted services run. It hits any integration class run in isolation at roughly 2 runs in 5, and it
was explicitly out of scope for 015 and 017. Detail under *Deferred* below.

Then: **Microsoft 10.0.11 -> 10.0.12 servicing train** across `src/` + tests, which also unblocks
`MockQueryable.Moq 10.0.12`.
Next *security* unit: **password policy** - Identity is currently `RequiredLength = 4` with every
complexity rule off (`AddIdentityServices`). Then security headers / CSP.

## Blockers
**None.** The only open blocker — `ASPNETCORE_FORWARDEDHEADERS_ENABLED` on the `rtub` App Service —
was **confirmed set on 2026-09-21** (resource group `rtub_group`, verified via Azure CLI). Unit
014's login rate limiter is now fully effective per client in production. See *Deployment
requirement* above.

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
- **Pre-existing integration-test flake, not caused by 013.**
  `AuthAntiforgeryTests.LoginPost_WithoutAntiforgeryToken_IsRejected` (unit 012, unmodified) fails
  intermittently — roughly 4 runs in 5 — **only when that class is run in isolation**, with
  `System.InvalidOperationException: Operations that change non-concurrent collections must have
  exclusive access` thrown from `SqliteConnection.CreateCollation` during
  `TestWebApplicationFactory` startup. It is a data race between the factory's `EnsureCreated()`
  and the background services starting on the same shared in-memory connection. It does **not**
  fire in the full suite (clean at 4497 / 0 / 60). **Re-measured by 014 and now the most annoying
  thing in the test suite:** it hits any integration class run in isolation, including the new
  `LoginRateLimitTests`, at roughly 2 runs in 5. Worth its own unit: serialize factory startup
  before the hosted services run.
- Out of scope by instruction and untouched: password policy, MFA, cookie
  validation / SQLite pressure, security headers / CSP, PWA cache strategy, push architecture
  refactor, `Program.cs` cleanup.
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

## Relevant files (unit 017)
- `src/RTUB.Application/Data/SeedData.cs` - `ResetDevDataAsync` (double gate, config validation,
  `UserManager`-based password reset, EF push-subscription delete, `ILogger`) and its call site.
- `tests/RTUB.Application.Tests/Data/DevelopmentDataResetTests.cs` - **new**.
- `README.md` - local opt-in via User Secrets + environment-variable reference.
- `STATE.md` - this file.

Unchanged and deliberately so: `SeedData.Member.cs`, `MemberBuilder.cs`, every `appsettings*.json`
(no `DevelopmentDataReset` section is committed anywhere - the switch is User Secrets / App
Service settings only), `TestWebApplicationFactory.cs`, migrations, the model snapshot, and CI.

## Tests (14 new, 0 removed, 0 changed)
All in `DevelopmentDataResetTests`. Every password comes from `TestSecret.NewPassword()`; the push
subscription's `P256dh`/`Auth` fixture values are generated GUIDs rather than literals for the same
reason. The only credential-ish literals are the *placeholders* the production code rejects.
1. `ResetDoesNotRun_LeavesCredentialsEmailsAndPushSubscriptionsUntouched` - **5 theory cases**
   (Development+`false`, Development+absent, **Staging**+`true`, **Production**+`true`,
   **Test**+`true`): old password still valid, reset password rejected, email unchanged, security
   stamp unchanged, push subscription still present.
2. `Development_EnabledWithoutUsablePassword_ThrowsBeforeAnyMutation` - **5 theory cases** (absent,
   empty, whitespace, `changeme`, `CHANGEME`): throws naming **both** `DevelopmentDataReset:Password`
   and `DevelopmentDataReset__Password`, the message does **not** contain the supplied value, and
   password / email / security stamp / push subscription are all untouched.
3. `Development_EnabledWithValidPassword_ResetsPasswordsEmailsAndPushSubscriptions` - new password
   authenticates, **old password no longer works**, email normalised to `{UserName}@rtub.pt` with
   `NormalizedEmail` upper-cased, `EmailConfirmed` preserved, **security stamp rotated**, push
   subscriptions cleared.
4. `Development_EnabledWithValidPassword_ResetsEveryUser` - two users, both reset.
5. `Development_WhenIdentityRejectsTheReset_ThrowsAndLeavesTheAccountUsable` - a stubbed
   `IPasswordValidator` rejects the password. The `IdentityResult` failure is **surfaced** as an
   `InvalidOperationException` carrying the Identity error code and the affected `UserName` and
   **not** the password; the account keeps a non-empty `PasswordHash`, still authenticates with its
   previous password, keeps its security stamp and keeps its original email. This is the test that
   would fail under a `RemovePassword` + `AddPassword` implementation.
6. `FullSeed_ThenDefaultDevelopmentStartup_KeepsBothConfiguredSeedPasswords` - the unit-016
   interaction, proven against the real bulk seed. See *Last completed step*.

## Latest validation (unit 017)
- Release build, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true` and
  `EnforceCodeStyleInBuild=true`.
- Focused first: `DevelopmentDataResetTests` alone - **14 total, 14 passed, 0 failed, 0 skipped**;
  together with `SeedDataBootstrapTests` - **29 total, 29 passed**.
- Full suite, `dotnet test --no-build -c Release`: **4526 passed, 0 failed, 60 skipped**
  (total 4586). Baseline was 4512 / 0 / 60 - **+14, exactly the tests added.**
- `git diff --check`: clean.
- **No hardcoded `PasswordHash` remains**: repo-wide `grep` for `PasswordHash = "` across `src/`
  returns nothing, and the removed `AQAAAA...` hash matches nowhere in `src/`, `tests/`, `docs/`
  or `README.md` (working tree; git history was not rewritten).
- Diff credential scan for `password = "<literal>"`, `secret = "<literal>"` and `?? "<literal>"`
  across the changed files: **no match**.
- `git status`: 1 modified source file, 1 modified `README.md`, 1 new test file, 1 modified
  `STATE.md`. **Migrations and the model snapshot are untouched.**
- No frontend build, no Playwright, no Graphify rebuild - one method body, not a structural change.
