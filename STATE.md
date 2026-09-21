# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-20_

## Phase
Modernization unit **014 (rate limiting on `POST /auth/login`) — implementation complete,
uncommitted, awaiting owner review.** Unit 013 is merged to `dev`.

## Branch
`fix/014/login-rate-limiting`, branched from `dev` (clean, in sync with `origin/dev` at `f30657b9`).
Uncommitted — no commit authorized.
`chore/001`-`chore/011`, `fix/012` and `fix/013` still present; delete when convenient.

## Last completed step
**Unit 014 — `POST /auth/login` now carries a per-client-IP rate limit, as a named policy applied
to that one endpoint.** No global limiter, no custom limiter type, no forwarded-header parsing.

### Policy

| | |
| --- | --- |
| Policy name | `login` (`ServiceCollectionExtensions.LoginRateLimitPolicy`) |
| Algorithm | partitioned **fixed window**, `RateLimitPartition.GetFixedWindowLimiter` |
| Threshold / window | **10 permits per 5 minutes**, per partition |
| Partition key | `HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"` |
| Queue | `QueueLimit = 0` — rejected immediately, never held open |
| Rejection | **429** (`RejectionStatusCode` + set again in `OnRejected` before the body is written) |
| `Retry-After` | emitted from `context.Lease.TryGetMetadata(MetadataName.RetryAfter, ...)`, seconds, invariant |
| Configuration | `LoginRateLimit:PermitLimit` / `LoginRateLimit:WindowMinutes` in `appsettings.json`; the two constants in `ServiceCollectionExtensions` are the fallback |
| Scope | `.RequireRateLimiting(...)` on the login `MapPost` **only** |

### Why these numbers
Identity locks an account after **5** failures for **5 minutes**
(`AddIdentityServices`). 10 / 5 min sits just above that and reuses the same window, so:
- a real user fumbling a password is locked out by Identity before the IP limit is reached, and is
  never throttled for a typo;
- a client walking a list of accounts — credential stuffing, which per-account lockout never sees —
  is capped at 10 accounts per 5 min per IP, ~120/hour, far below a useful stuffing rate;
- two people behind one NAT can each still fail 5 times before either is throttled.

A single fixed window was enough; no chained limiter was added. Fixed window's known weakness is a
burst of up to 2x `PermitLimit` straddling a window boundary — 20 attempts, still bounded, and
still far under a brute-force rate.

### Why not partition by username/email
It is caller-controlled, so each forged value would allocate and cache its own limiter — an
unbounded-partition memory DoS — and it would add nothing, because Identity lockout already covers
the per-account case. A null/unknown `RemoteIpAddress` deliberately collapses into a single shared
`"unknown"` bucket rather than creating a partition, so the partition count is bounded by the
number of real peers.

### Middleware order
`app.UseRateLimiter()` is placed **immediately after `app.UseRouting()`**, which is what the
current docs require for endpoint-specific policies (the endpoint's `RequireRateLimiting` metadata
must already be resolved). That also puts it **before** `UseAuthentication` / `UseAuthorization` /
`UseAntiforgery`, so a throttled client is answered 429 before any credential or token work runs.
Consequence, asserted by test: every login POST spends a permit **whatever its outcome**, including
one rejected by antiforgery.

## Deployment requirement — Azure forwarded headers (BLOCKING for production effect)

**Production `rtub` is Azure App Service on Linux.** `curl -I https://rtub.azurewebsites.net/health`
returns `Server: Kestrel` — no IIS layer, so there is no `UseIISIntegration` auto-wiring of
forwarded headers. `Program.cs` already assumes this: it skips `UseHttpsRedirection` outside
Development with the comment "HTTPS is handled at the load balancer level", which is the workaround
used exactly when the scheme is *not* being forwarded.

Therefore, with no forwarded-headers handling anywhere in the repo today,
`Connection.RemoteIpAddress` on App Service is the **platform's front-end address, not the client's**.
Every request would fall into one shared partition and the limiter would throttle all users
together instead of per client.

**Required App Service setting (Configuration -> Application settings):**

```
ASPNETCORE_FORWARDEDHEADERS_ENABLED = true
```

This is Microsoft's documented switch for App Service Linux / containers. The host wires
`ForwardedHeadersMiddleware` itself, ahead of the app pipeline, with cloud-appropriate settings.
Nothing is added to RTUB's own code for it — deliberately: no `UseForwardedHeaders` call, no
`ForwardedHeadersOptions`, no clearing of `KnownProxies`/`KnownNetworks`, and no manual
`X-Forwarded-For` parsing.

**Not verified:** whether this setting is already present on the `rtub` App Service. It is portal
configuration and is not in the repo (`.github/workflows/ci.yml` only publishes and deploys; it
sets no app settings). **Confirm it in the portal before relying on per-IP behavior in production.**
The same setting is needed on the future Azure dev environment.

Also note: enabling it makes `Request.IsHttps` true behind the proxy, which is what the
`UseHttpsRedirection` skip at `Program.cs:325` was working around. Revisiting that skip is a
separate decision, deliberately not made here.

What `RemoteIpAddress` is, per environment:
- **Local / `dotnet run`** — the real client address; correct with no extra configuration.
- **Integration tests (`TestServer`)** — `null` for every request (no transport), so all callers
  share the `"unknown"` partition. Tests work around this explicitly; see Tests below.
- **Azure App Service (current prod, Linux/Kestrel)** — the platform front end until
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is set.
- **Future Azure dev environment** — same, same setting.


## Current task
None active.

## Next unit
**Microsoft 10.0.11 -> 10.0.12 servicing train** across `src/` + tests, which also unblocks
`MockQueryable.Moq 10.0.12`.
Next *security* unit: **password policy** — Identity is currently `RequiredLength = 4` with every
complexity rule off (`AddIdentityServices`). Then security headers / CSP.

## Blockers
**One, deployment-side, not code:** `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` must be confirmed or
set on the `rtub` App Service, or the new limiter partitions on the platform front-end address
instead of the client's and throttles all users as one. See *Deployment requirement* above. The
code is correct and safe either way; only the per-client granularity depends on it.


## Deferred / owner decisions

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
- **`UseHttpsRedirection` is skipped outside Development** (`Program.cs:325`). Turning on
  `ASPNETCORE_FORWARDEDHEADERS_ENABLED` makes `Request.IsHttps` true behind the proxy, which is
  what that skip works around. Separate decision, not made here.

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

## Relevant files (unit 014)
- `src/RTUB.Web/Extensions/ServiceCollectionExtensions.cs` — new `AddLoginRateLimiting`, plus the
  policy name and the two default constants.
- `src/RTUB.Web/Program.cs` — `AddLoginRateLimiting` registration, `UseRateLimiter` after
  `UseRouting`, `.RequireRateLimiting(...)` on the login `MapPost`.
- `src/RTUB.Web/appsettings.json` — new `LoginRateLimit` section (10 / 5).
- `tests/RTUB.Integration.Tests/LoginRateLimitTests.cs` — **new**, 8 tests.
- `tests/RTUB.Integration.Tests/RemoteIpTestStartupFilter.cs` — **new**, test-host only.
- `tests/RTUB.Integration.Tests/TestWebApplicationFactory.cs` — registers that filter (2 lines).

## Tests (8 new)
`TestServer` has no transport, so `Connection.RemoteIpAddress` is `null` for every request and every
caller shares one partition — which makes an IP-partitioned policy untestable and makes tests in one
class interfere. `RemoteIpTestStartupFilter` is an `IStartupFilter` registered **only** by the test
factory; it runs ahead of the whole app pipeline and sets the same `Connection.RemoteIpAddress` the
transport sets in production, from an `X-Test-Remote-Ip` header. It is a no-op unless a request opts
in, so it cannot affect any other test. It is **not** a forwarded-headers implementation and adds no
production code path. Each test uses its own IP, so the class is order-independent and **no test
sleeps or waits for a window to roll over**.

1. `LoginPost_UnderLimit_SignsUserInNormally` — real rendered-form login still 302 to `/` with the
   Identity cookie.
2. `LoginPost_AtTheLimit_IsStillAccepted_ThenRejectedWith429` — requests 1..10 all reach antiforgery
   (400); request 11 is **429**. Pins both the threshold and the limiter-before-antiforgery order.
3. `LoginPost_WhenRejected_SendsRetryAfterFromLeaseMetadata` — 429 carries a positive `Retry-After`.
4. `LoginPost_WhenRejected_DoesNotSignAnyoneIn` — valid credentials **and** a valid token, budget
   spent: 429 and no `Set-Cookie`.
5. `LoginRateLimit_IsPartitionedByClientIp` — one IP exhausted and 429; a second IP's next request is
   400, not 429.
6. `LoginPost_WithWrongPassword_UnderLimit_StillCountsTowardAccountLockout` — 302 to
   `/login?error=Invalid` **and** `AccessFailedCount == 1`, so lockout is intact.
7. `LoginPost_UnderLimit_StillRequiresAntiforgeryToken` — tokenless POST is still 400, no cookie.
8. `RateLimiting_IsScopedToLoginOnly` — `/health`, `GET /login` and `POST /auth/logout` each driven
   12 times (over the limit) and never throttled, proving there is no global limiter.

**No new credential-shaped literal was committed.** Test passwords come from
`NewSecret() => Guid.NewGuid().ToString("N")`, generated per call; Identity's `RequiredLength = 4`
with no complexity rules accepts it. No GitGuardian ignore comment was added. The pre-existing
`TestPassword123!` in `TestWebApplicationFactory.cs` is untouched.

**Negative probe run and reverted.** `.RequireRateLimiting(...)` was temporarily detached from the
endpoint: tests 2, 3, 4 and 5 failed and the four behavior-preservation tests (1, 6, 7, 8) still
passed — exactly the intended split. Restored, all 8 green.

## Latest validation (unit 014)
- Release build, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true` and
  `EnforceCodeStyleInBuild=true`.
- Full suite, `dotnet test --no-build -c Release`: **4497 passed, 0 failed, 60 skipped.**
  Baseline was 4489 / 0 / 60; **+8 = exactly the 8 new tests.** No other count moved, and no
  existing test needed changing.
- New class run in isolation, 8 runs: **6 clean at 8 / 0 / 0, 2 hit the pre-existing
  `TestWebApplicationFactory` startup race** (unit 013's documented flake:
  `System.InvalidOperationException: Operations that change non-concurrent collections must have
  exclusive access` from `SqliteConnection.CreateCollation`). **Not caused by 014** — the whole
  stack is DI/EF connection construction, nothing rate-limiting is on it, and the unmodified
  `AuthAntiforgeryTests` flakes the same way on this branch (2 failures in 5 isolated runs). It
  never fires in the full suite, which is clean.
- `git diff --check` clean. Secret scan clean — the only matches on added lines are the words
  "credential stuffing" in a comment and the `OnRejected` identifier.
- `git status` = 4 modified files, 2 new untracked test files. **Migrations and
  `ApplicationDbContextModelSnapshot.cs` unchanged.** New files written CRLF to match siblings.
- Graphify **not** rebuilt — one DI extension method plus two pipeline lines is not a material
  structural change. No frontend build, no Playwright.


## Previous validation (unit 013 — merged)
The five remaining cookie-authenticated mutations were classified against measured behavior;
`POST /api/admin/refresh-all` was CSRF-reachable and provably uncalled, so it was deleted. The Push
endpoints were left alone — `[FromBody]` JSON binding returns 415 for every form enctype. 4 tests in
`tests/RTUB.Integration.Tests/Api/CookieApiCsrfTests.cs`. Suite 4489 / 0 / 60. Detail is in the
`fix/013/cookie-api-csrf` history.

## Previous validation (unit 012 — merged)
`POST /auth/login` and `POST /auth/logout` require real antiforgery tokens, via `IFormCollection`
handler parameters plus `<AntiforgeryToken />` in `Login.razor` and `MainLayout.razor`. 7 tests in
`tests/RTUB.Integration.Tests/AuthAntiforgeryTests.cs`, all driving the real rendered form. Suite
4485 / 0 / 60. Detail is in the `fix/012/auth-antiforgery` history.

## Previous validation (unit 011 — merged)
All 5 test projects migrated from xUnit v2/VSTest to **xUnit v3 4.0.1 on
Microsoft.Testing.Platform v2**, with zero test-source and zero `src/` edits. New `global.json`
(`test.runner`) and `tests/Directory.Build.props` (suppresses `xUnit1051`, 1634 sites). CI switched
to `--report-xunit-trx` + `--coverage --coverage-output-format cobertura`. Suite **4478 / 0 / 60**,
zero vulnerable and zero deprecated packages across all 9 projects. Runtime fell from ~25 min to
~25 s. Detail is in the `chore/011/migrate-xunit-v3` history.

## Graphify effectiveness (measured, unit 008)
Three real cross-layer questions, Graphify first, then minimal source verification.

| Question | Graphify result | Verdict |
| --- | --- | --- |
| Web Push: scheduler -> browser | `affected IPushNotificationService` returned all 6 background senders + `PushController` at exact `file:line`. **Missed** the browser leg — `service-worker.js` is a degree-2 island. | Accurate; incomplete at the HTTP boundary |
| SQLite backup + R2 | `affected IDatabaseBackupStorageService` found the `DatabaseBackupBackgroundService` orchestrator (`RunBackupAsync` L144, `RotateAsync` L211) and the `BaseStorageService` hierarchy. `query` returned false positives (`RestoreHp`, `ObjectPool`). | Accurate via `affected`; `query` noisy |
| MyTuno Blazor/Application/PixiJS | Correctly linked `MyTunoHome.razor` -> `IInventoryService`. **No path** Razor -> PixiJS: `JSRuntime.InvokeVoidAsync("myTunoGame.startBattle", ...)` is string dispatch, invisible to AST. | Real blind spot; grep found it in one call |

Conclusions: `explain` / `affected` are the value — precise, verifiable `file:line`, reverse traversal
that grep cannot cheaply reproduce. `graphify query` is lexical BFS and misleads. Cross-language and
string-dispatch boundaries are a structural blind spot. MCP beats reading the 147 KB `GRAPH_REPORT.md`
(same engine as the CLI, structured access); the report itself is not worth reading in full.
Maintaining the graph is worthwhile at ~73 s rebuild / 7% incremental cost.

## Graph artifact policy
`graphify-out/` is **gitignored** — 84 MB total, `graph.json` alone 38 MB, fully reproducible from
source. Committed instead: `.claude/skills/graphify/`, `.claude/CLAUDE.md`, `.mcp.json`,
`.graphifyignore`, the `.gitignore` rule, and the `CLAUDE.md` routing rule.

## MCP gate — executable conflict resolved
The PATH fight is over; it was solved by making the *winning* install compatible instead.

`explorer.exe` caches the pre-reorder environment block and Claude Desktop inherits it, so neither
a new session nor an app restart ever picked up a reordered PATH. Instead, the Microsoft-Store
Python user-site install was upgraded in place: **graphifyy 0.9.55 -> 0.9.56 with the `mcp` extra**
(`mcp 2.2.0`). Both installs are now 0.9.56 + MCP, so whichever wins PATH works.

**Confirmed operational 2026-09-20** in a fresh Claude session, via the MCP tools themselves:
`graph_stats` → **20,354 nodes / 48,074 edges / 734 communities** (88% EXTRACTED / 12% INFERRED);
`get_node IPushNotificationService` → `src/RTUB.Application/Interfaces/IPushNotificationService.cs`
L8, degree 67; `shortest_path PushNotificationService → IPushNotificationService` → 1-hop
`implements` [EXTRACTED]. The chain Claude → `.mcp.json` → `graphify-mcp` →
`graphify-out/graph.json` works end-to-end.
