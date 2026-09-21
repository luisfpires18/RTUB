# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **016 (harden production bootstrap credentials) — implementation complete,
uncommitted, awaiting owner review.** Unit 015 is merged to `dev` at `bbe61213`.

## Branch
`fix/016/bootstrap-credentials`, branched from `dev` (clean, in sync with `origin/dev` at
`bbe61213`). Uncommitted — no commit authorized.
`chore/001`-`chore/011`, `fix/012`-`fix/014` and `chore/015` still present; delete when convenient.

## Last completed step
**Unit 016 — the two hardcoded production password defaults unit 015 found are gone.** Neither
was a leaked credential; both were insecure application defaults. Bootstrap now fails closed.

### Owner bootstrap — before / after
`SeedData.Member.cs` read `configuration["AdminUser:Password"] ?? <hardcoded default>`. With the
setting unset, an empty database silently produced an Owner+Admin account whose password is in
the source tree.

After: the fallback is gone. `RequireBootstrapPassword` throws `InvalidOperationException` when
the value is null, empty, whitespace, or one of the documented placeholders (`your-admin-password`
— the README example — plus `changeme` / `change-me` / `password`, matched case-insensitively).
The message names `AdminUser:Password` and `AdminUser__Password` and **never contains the value**.
No replacement default, and no generated password nobody can retrieve.

**Empty database:** no `AdminUser:Password` → seeding throws, zero users created, startup fails.
With the setting supplied → the Owner is created exactly as before, same username/email defaults
(`rtub` / `admin@rtub.pt`), same `Owner` + `Admin` roles, same early return.

**Existing populated database:** unaffected and does **not** require the setting.
`InitializeAsync` still returns as soon as any user exists, and the new check sits *inside* the
`ownerUser == null` branch, so it is only evaluated when an Owner is actually about to be created.

One supporting change: the `if (isEmptyDb) return;` was hoisted out of the create-success branch
to just after the `ownerUser == null` block, so the Owner-only bootstrap returns whether or not the
Owner was created on this run. It does not fire when `isEmptyDb` is `false`, so the full-seed path
is unaffected.

### MemberBuilder / bulk member seed — CORRECTED
An earlier draft of this unit called the bulk member seed dead legacy code. **That was wrong.** It
is an **intentional manual developer switch**: `isEmptyDb` at `SeedData.cs` is flipped to `false`
by hand against a fresh database to build a full development dataset, and back to `true` otherwise.
It is now commented as such in place. The boolean stays, the ~40 member definitions stay, and
nothing about the switch was turned into configuration.

`MemberBuilder._password` had a hardcoded default and `Password(...)` was called by nothing, so
that default was the only password every seeded member got. Fix keeps the builder fail-closed —
`_password` is `string?` with no default and `CreateAsync` throws before touching `UserManager`
when it is unset — and gives `SeedMembersAsync` an external source for it.

**Two separate externally configured passwords. The privileged Owner password is never reused for
ordinary members.**

| Path | `AdminUser:Password` | `SeedData:MemberPassword` |
| --- | --- | --- |
| Fresh DB, `isEmptyDb = true` | **required** | not required |
| Fresh DB, `isEmptyDb = false` | **required** | **required** |
| Existing populated DB | not required | not required |

Smallest implementation: the class-level `Member(userManager)` helper became a **local function**
inside `SeedMembersAsync` that applies the resolved member password to every builder. All 82 call
sites are untouched and no password literal was introduced anywhere.

**Validated before mutation.** With `isEmptyDb = false`, `SeedData:MemberPassword` is checked at
the top of `SeedMembersAsync`, *before* the Owner is created. A missing value therefore leaves
**zero** users behind — important because `InitializeAsync` returns as soon as any user exists, so
a half-seeded database would never be completed on a later start.

### Where local passwords go — User Secrets, not `appsettings.Development.json`
**`appsettings.Development.json` is NOT git-ignored.** `.gitignore` contains no rule for it
(verified on `dev`), so it is not a safe place for credentials and the README no longer says it is.
Local values for `AdminUser:Password` and `SeedData:MemberPassword` go in **.NET User Secrets**,
using the `UserSecretsId` already declared at `src/RTUB.Web/RTUB.csproj:5`. README documents the
two `dotnet user-secrets set` commands with `<placeholder>` arguments only — no literals.

### Known interaction — the full seed is undone on the next startup
On a **fresh** database with `isEmptyDb = false`, seeding is correct: the Owner is created from
`AdminUser:Password` and every member from `SeedData:MemberPassword`, kept separate, and the tests
prove it.

**But the next non-Production startup overwrites all of it.** `ResetDevDataAsync` runs before the
seeding early-return and rewrites **every** user's `PasswordHash` to one shared value, so after the
second run neither configured password authenticates anyone — every account shares the hardcoded
dev hash instead. That is pre-existing behavior, untouched by 016, and it is why the next unit
changed (see *Next unit*).

### Files changed (5)
- `src/RTUB.Application/Data/SeedData.Member.cs` — both fallbacks removed, `RequireSeedPassword` +
  placeholder set added, member password resolved once into a local-function builder factory,
  `isEmptyDb` return hoisted.
- `src/RTUB.Application/Data/SeedData.cs` — comment documenting the `isEmptyDb` manual switch.
  **No behavior change**; the boolean itself is untouched.
- `src/RTUB.Application/Data/Builders/MemberBuilder.cs` — default removed, fail-closed guard.
- `README.md` — documents `SeedData__MemberPassword` and when each password is required.
- `tests/RTUB.Application.Tests/Data/SeedDataBootstrapTests.cs` — **new**, the only new file.

### Not changed, by instruction
Identity's password policy (`RequiredLength = 4`, complexity off) is untouched — still its own
unit. No Azure configuration was changed. `ResetDevDataAsync` was reviewed and left alone — see the
finding in *Deferred*, which corrects how its guard was described earlier.

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
**`ResetDevDataAsync` hardening — promoted ahead of the SQLite race by unit 016.** Its guard is
only `if (environment.IsProduction()) return;`, so **Development *and* Staging** rewrite every
user's `PasswordHash` to one shared hardcoded value on every startup. Two reasons it goes first:
it silently undoes the full development seed 016 just made correct (see above), and an Azure DEV
App Service running as `Staging` would reset all user passwords on each start — that environment
is planned. Likely shape: tighten to `IsDevelopment()` and/or gate on an explicit opt-in setting,
and source the reset password from configuration rather than a hardcoded hash. Detail in
*Deferred*.

Then: **the `TestWebApplicationFactory` SQLite startup race** — serialize factory startup before
the hosted services run. It hits any integration class run in isolation at roughly 2 runs in 5,
and it was explicitly out of scope for 015. Detail under *Deferred* below.
Then: **Microsoft 10.0.11 -> 10.0.12 servicing train** across `src/` + tests, which also unblocks
`MockQueryable.Moq 10.0.12`.
Next *security* unit: **password policy** — Identity is currently `RequiredLength = 4` with every
complexity rule off (`AddIdentityServices`). The two `src/` password defaults it was going to
absorb were handled separately by unit 016 and are no longer part of it. Then security headers / CSP.

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
- **`ResetDevDataAsync` (`SeedData.cs`) runs in EVERY environment except Production.** The guard
  is exactly `if (environment.IsProduction()) return;`, so **Development *and* Staging** execute
  it — it clears push subscriptions and rewrites **every** user's `PasswordHash` to one shared
  value. Its own doc comment ("only execute in Development / local environments") understates
  this. Behavior deliberately **not changed** by 016; not required by the seeding tests.
  **Act on this before the planned Azure DEV environment**: an Azure DEV App Service running as
  `Staging` would reset every user password to the same known hash on each startup. Its own unit —
  tighten the guard to `IsDevelopment()`, or gate it on an explicit opt-in setting.
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

## Relevant files (unit 016)
- `src/RTUB.Application/Data/SeedData.Member.cs` — Owner bootstrap: no fallback password,
  `RequireBootstrapPassword` + `BootstrapPasswordPlaceholders`, `isEmptyDb` return hoisted.
- `src/RTUB.Application/Data/Builders/MemberBuilder.cs` — `_password` is `string?` with no
  default; `CreateAsync` throws before creating a user when it is unset.
- `tests/RTUB.Application.Tests/Data/SeedDataBootstrapTests.cs` — **new**.
- `STATE.md` — this file.

Unchanged and deliberately so: `SeedData.cs` (incl. `ResetDevDataAsync`), every `appsettings*.json`
(they contain no `AdminUser` section at all), `README.md` (its `your-admin-password` placeholder is
now one of the values the code rejects), migrations and the model snapshot, and CI.

## Tests (15 new, 0 removed, 0 changed)
All in `SeedDataBootstrapTests`. Every password comes from `TestSecret.NewPassword()` (unit 015) —
no password-shaped literal was introduced. The only credential-ish literals are the *placeholders*
the production code rejects, which are the assertion's subject, not secrets.
1. `EmptyDatabase_WithoutUsableAdminPassword_CreatesNoOwnerAndFailsClearly` — **6 theory cases**
   (absent, empty, whitespace, `your-admin-password`, `changeme`, `CHANGEME`): throws naming
   `AdminUser:Password` **and** zero users exist after.
2. `EmptyDatabase_WithSuppliedAdminPassword_CreatesOwnerWithBothRoles` — `isEmptyDb: true`, no
   member password configured: Owner created, password verified, `Owner` + `Admin` roles, exactly
   one user. Owner-only behavior preserved.
3. `BulkSeed_WithoutUsableMemberPassword_WritesNothingAndFailsClearly` — **4 theory cases**
   (absent, empty, whitespace, `changeme`) with a valid admin password and `isEmptyDb: false`:
   throws naming `SeedData:MemberPassword` and **zero users created** — proves validation precedes
   any mutation.
4. `BulkSeed_WithMemberPassword_SeedsOwnerAndMembersWithThatPassword` — `isEmptyDb: false`: Owner
   created and authenticates with the **admin** password, user count > 1, and the seeded member
   `nabo` authenticates with the **member** password and holds the `Member` role. Proves the two
   passwords stay separate and the full dataset really is created (82 members).
5. `PopulatedDatabase_DoesNotRequireBootstrapPassword` — Owner pre-seeded, neither setting
   configured: no throw, count unchanged.
6. `MemberBuilder_WithoutExplicitPassword_CreatesNoUserAndFailsClearly` — still fail-closed.
7. `MemberBuilder_WithExplicitPassword_CreatesTheUser` — the guard did not break the path.

Requirement "no known/default password exists in production source" is covered by the repo-wide
scan below, **not** by a test: a test asserting a specific literal is absent would have to contain
that literal, which is exactly what unit 015 removed.

## Latest validation (unit 016)
- Release build, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true` and
  `EnforceCodeStyleInBuild=true`.
- Focused first: `SeedDataBootstrapTests` alone — **10 total, 10 passed, 0 failed, 0 skipped.**
- Full suite, `dotnet test --no-build -c Release`: **4507 passed, 0 failed, 60 skipped**
  (total 4567). Baseline was 4497 / 0 / 60 — **+10, exactly the tests added.**
- `git diff --check`: clean. Both edited files re-normalised to CRLF, so the diff is minimal
  (36 insertions / 3 deletions and 13 / 3) rather than whole-file rewrites.
- Repo-wide scan for the two known defaults across `src/`, `tests/`, `docs/`, `README.md` and
  `.github/`: **no match** (working tree; git history was not rewritten, same as unit 015).
- Targeted credential scan of `src/` for `?? "<literal>"` password fallbacks and
  `password = "<literal>"` assignments: one hit, `Profile.razor`'s `OnChangePassword` event
  callback — markup, not a credential.
- `git status`: 2 modified source files, 1 new test file, 1 modified `STATE.md`. **Migrations and
  the model snapshot are untouched** — no schema change was needed or made.
- No frontend build, no Playwright, no Graphify rebuild — the change is two method bodies, not a
  structural change.
