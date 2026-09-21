# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **018 (fix the `TestWebApplicationFactory` SQLite startup race) - implementation
complete, uncommitted, awaiting owner review.** Unit 017 is merged to `dev` at `37a19e8a`.

## Branch
`fix/018/integration-sqlite-startup-race`, branched from `dev` (clean, in sync with `origin/dev` at
`37a19e8a`). Uncommitted - no commit authorized.
`chore/001`-`chore/011`, `fix/012`-`fix/014`, `chore/015`, `fix/016` and `fix/017` still present;
delete when convenient.

## Last completed step
**Unit 018 - the integration-test host no longer shares one `SqliteConnection`, and its database is
seeded before any hosted service starts.** The intermittent
`InvalidOperationException: Operations that change non-concurrent collections must have exclusive
access` is gone. Measured in the same harness, back to back: **old factory 12 failures in 40
isolated runs, all 12 the race; new factory 0 in 40.**

### Exact cause - proven, not inferred
Captured stack, abridged, from a reproduction run against the pre-018 factory:

```
System.InvalidOperationException : Operations that change non-concurrent collections must have
exclusive access. ...
  at System.Collections.Generic.Dictionary`2.set_Item(TKey key, TValue value)
  at Microsoft.Data.Sqlite.SqliteConnection.CreateAggregateCore[...](...)
  at Microsoft.EntityFrameworkCore.Sqlite.Storage.Internal.SqliteRelationalConnection
       .InitializeDbConnection(DbConnection connection)
  at Microsoft.EntityFrameworkCore.Sqlite.Storage.Internal.SqliteRelationalConnection..ctor(...)
  ... at RTUB.Application.Repositories.GameRepository.GetByKeyAsync(...)
  ... at RTUB.Application.Data.SeedData.InitializeAsync(...)
  ... at RTUB.Integration.Tests.TestWebApplicationFactory.CreateHost(...)
```

`SqliteRelationalConnection`'s **constructor** calls `InitializeDbConnection`, which registers EF
Core's own SQL functions, collations and aggregates (`ef_mod`, `ef_add`, ...) on the
`SqliteConnection` object. Those registrations are `Dictionary<,>.set_Item` on plain, unsynchronised
dictionary fields of `SqliteConnection`. **A `SqliteRelationalConnection` is constructed once per
`ApplicationDbContext`**, so with one shared connection instance *every* context construction
rewrote those dictionaries. Two constructions at once corrupted them.

The two racing paths were:
1. **main test thread** - `CreateHost` -> `db.Database.EnsureCreated()` -> `SeedData.InitializeAsync`
   -> `GameService.SeedDefaultGamesAsync` -> new `ApplicationDbContext`;
2. **thread pool** - `MemberStatusUpdateBackgroundService.ExecuteAsync`, the **only** hosted service
   with no startup delay (`await RunUpdateAsync()` on its first line; the other nine open with
   `await Task.Delay(5-25 s)`) -> `MemberStatusService.UpdateAllMemberStatusesAsync` ->
   `IDbContextFactory.CreateDbContextAsync` -> new `ApplicationDbContext`.

Both are pure DI/startup work, which is why the exception was unrelated to whichever test ran, and
why the full suite mostly hid it: a warm process loses the overlap window that a cold, isolated
class run has.

**Second, latent bug found by the same trace:** `base.CreateHost(builder)` **starts** the host, so
every hosted service ran *before* `EnsureCreated()` and the seed. `MemberStatusUpdateBackgroundService`
was therefore querying `AspNetUsers` against an empty database on every single factory startup and
logging `SQLite Error 1: 'no such table: AspNetUsers'`, swallowed by its own `catch`. Now fixed too:
a full integration run logs **zero** SQLite errors where it previously logged twelve.

### Previous test-database architecture
- one `SqliteConnection` field on the factory, `DataSource=:memory:`;
- opened inside `ConfigureWebHost`'s `ConfigureServices` delegate;
- `AddDbContext<ApplicationDbContext>(o => o.UseSqlite(_connection))` - the **connection instance**;
- `CreateHost` = `base.CreateHost` (which builds **and starts**), then `EnsureCreated()`, then seed;
- `Dispose` closed the connection *before* `base.Dispose`.

A bare `:memory:` database is private to its one connection, so sharing the instance was the only
way the old design could keep one database - and sharing the instance was the defect.

### New test-database architecture
- `Data Source=rtub-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared` - **a name generated per
  factory instance**, so each factory (and so each test class, which run in parallel) stays isolated,
  exactly as before;
- one `_keepAlive` connection opened in the **factory constructor** and closed in `Dispose`. A named
  in-memory database is dropped when its last connection closes, so this keeps it alive for the
  factory lifetime. Nothing queries through it. Opening it in the constructor rather than in
  `ConfigureServices` also means it cannot be created twice or leaked if that delegate re-runs;
- `AddDbContext` is configured from the **connection string**, so every `ApplicationDbContext` -
  scoped, or built by `IDbContextFactory` - opens and owns its own `SqliteConnection`. No two
  contexts can touch one connection's dictionaries again, under any amount of concurrency;
- `Dispose` now calls `base.Dispose` **first**, then closes `_keepAlive`, so the database outlives
  anything still querying it.

### Startup order - fixed at the lifecycle, not with sleeps or retries
`WebApplicationFactory`'s host is **deferred**: `builder.Build()` does not materialise services, and
merely reading `host.Services` is what starts the app. Build-seed-then-start is therefore impossible
- attempting it throws `ObjectDisposedException: IServiceProvider`, which was verified, not assumed.

So the seed moved into the host's own lifecycle: a private
`TestWebApplicationFactory.DatabaseInitializer : IHostedLifecycleService` does `EnsureCreatedAsync`
plus `SeedData.InitializeAsync` in **`StartingAsync`**. `Host.StartAsync` runs *every* hosted
service's `StartingAsync` before *any* `StartAsync`, so the database is complete before the first
background service runs, and registration order is irrelevant. No `Task.Delay`, no retry, no lock,
no serialisation of the test suite.

### Hosted services - none removed, none disabled
`MemberStatusUpdateBackgroundService` and the other nine still start in the test host exactly as in
production. The fix is ordering plus connection ownership, so nothing had to be stubbed out. **One
hosted service was added, and it exists only in the test project**: `DatabaseInitializer`, above.

### Seeding semantics - unchanged
Same `SeedData.InitializeAsync(services, configuration)`, same scope, same configuration, same
generated `AdminPassword`. Only *when* it runs moved. No production file was touched: the diff is
two files, both under `tests/`.

### Files changed (2)
- `tests/RTUB.Integration.Tests/TestWebApplicationFactory.cs` - connection string plus keep-alive
  connection, `CreateHost` override removed, `DatabaseInitializer` added, `Dispose` order reversed.
- `tests/RTUB.Integration.Tests/TestWebApplicationFactoryTests.cs` - **new**, the only new file.

### Not changed, by instruction
Application SQLite performance, production DB architecture, cookie-validation DB pressure, global
test parallelisation policy, `xUnit1051`, CI/CD, Azure, PWA, `SeedData`, hosted-service
architecture. No migration and no model-snapshot change - none was needed. **Nothing in `src/`.**

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
**Microsoft 10.0.11 -> 10.0.12 servicing train** across `src/` + tests, which also unblocks
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
- ~~**Pre-existing integration-test flake, not caused by 013**~~ — **fixed by unit 018.** The
  shared `SqliteConnection` is gone and the seed now runs in `IHostedLifecycleService.StartingAsync`.
  Measured before/after in the same harness: **12 failures in 40 isolated runs → 0 in 40.** See
  *Last completed step*.
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

## Relevant files (unit 018)
- `tests/RTUB.Integration.Tests/TestWebApplicationFactory.cs` - per-factory named shared-cache
  in-memory database, keep-alive connection, `DatabaseInitializer` hosted lifecycle service.
- `tests/RTUB.Integration.Tests/TestWebApplicationFactoryTests.cs` - **new**.
- `STATE.md` - this file.

Unchanged and deliberately so: every file under `src/`, `IntegrationTestBase.cs`,
`RemoteIpTestStartupFilter.cs`, every existing integration test class, migrations, the model
snapshot, and CI.

## Tests (3 new, 0 removed, 0 changed)
All in `TestWebApplicationFactoryTests`. They pin the test host's own lifecycle, not application
behaviour.
1. `EveryDbContextOpensItsOwnConnection` - two contexts from two scopes plus one from
   `IDbContextFactory<ApplicationDbContext>`; all three `DbConnection` instances must be distinct,
   and all three must still see the seeded users. This is the **deterministic** assertion of the new
   architecture: it fails outright on the old shared-connection design.
2. `SeedDataIsInPlaceBeforeHostedServicesStart` - a derived factory registers one extra probe
   `IHostedService` that records, in `StartAsync`, whether the seeded `testadmin` is already
   present. Deterministic: `StartingAsync` always precedes every `StartAsync`.
3. `ConcurrentDbContextCreationDoesNotCorruptTheDatabase` - 32 parallel scope-and-context creations,
   each running a query. This is the direct regression for the reported exception; it is paired with
   test 1 so the suite never rests on a probabilistic check alone.

## Latest validation (unit 018)
- Release build, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true` and
  `EnforceCodeStyleInBuild=true`.
- Focused first: `TestWebApplicationFactoryTests` alone - **3 total, 3 passed**; then the whole
  `RTUB.Integration.Tests` project - **249 total, 247 passed, 2 skipped, 0 failed**, with **zero**
  `SQLite Error` lines in the log (previously twelve per run, from the unseeded background query).
- **Stress, before/after in the same harness**, each class run **in isolation** as its own
  `dotnet test --filter-class` process:

  | Class | Runs | Old factory | New factory |
  | --- | --- | --- | --- |
  | `AuthAntiforgeryTests` | 20 | **5 failed**, all 5 the race | **0 failed** |
  | `LoginRateLimitTests` | 20 | **7 failed**, all 7 the race | **0 failed** |
  | `TestWebApplicationFactoryTests` | 20 | n/a | **0 failed** |

  60 isolated new-factory runs, **zero** occurrences of `non-concurrent collections` anywhere in the
  captured output.
- Full suite, `dotnet test --no-build -c Release`: **4529 passed, 0 failed, 60 skipped**
  (total 4589). Baseline was 4526 / 0 / 60 - **+3, exactly the tests added.**
- `git diff --check`: clean.
- Diff credential scan for `password = "<literal>"`, `secret = "<literal>"`, `token`/`key` literals
  and `?? "<literal>"` across the changed files: **no match**.
- `git status`: 1 modified test file, 1 new test file, 1 modified `STATE.md`. **No file under
  `src/` is touched; migrations and the model snapshot are untouched.**
- No frontend build, no Playwright, no Graphify rebuild - test infrastructure only, no application
  structure change.
