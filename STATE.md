# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-21_

## Phase
Modernization unit **015 (remove committed synthetic credential literals from tests) —
implementation complete, uncommitted, awaiting owner review.** Unit 014 is merged to `dev`.

## Branch
`chore/015/test-secret-hygiene`, branched from `dev` (clean, in sync with `origin/dev` at
`2f731581`). Uncommitted — no commit authorized.
`chore/001`-`chore/011` and `fix/012`-`fix/014` still present; delete when convenient.

## Last completed step
**Unit 015 — every committed credential-shaped literal is gone from the test tree.** Test
passwords are generated at runtime; nothing about what any test does changed.

### Why
GitGuardian raised a "Generic Password" incident after unit 012 committed a synthetic test
password. A scanner cannot tell a synthetic password from a real one, so the noise is structural:
any future test that types a password-shaped literal re-raises it.

### Strategy
One helper, `tests/Shared/TestSecret.cs`, source-linked into all five test projects by
`tests/Directory.Build.props` (`<Compile Include>` plus `<Using Include="RTUB.Tests" />`, so no
per-file using). `tests/Shared` sits outside every project cone, so it does not collide with the
SDK's default compile glob. No new test project: it is one static class, and a project would drag
in the whole package and reference chain for nothing.

`TestSecret.NewPassword() => Guid.NewGuid().ToString("N") + "Aa1!"` — fresh per call. The GUID
gives length and uniqueness; the four-character suffix is complexity padding so the value stays
valid if Identity's password rules are ever tightened (today: `RequiredLength = 4`, every
complexity rule off). A test that needs the same value twice holds it in a local and passes it to
both calls; nothing is written down.

`TestWebApplicationFactory` now generates its seeded-admin password per factory instance and
exposes it as `AdminPassword`, because `CookieApiCsrfTests` signs that admin in and the create and
the sign-in must agree. Its `SmtpPassword` is generated too — nothing sends mail under the test
host, but a credential-shaped literal keyed `SmtpPassword` is exactly what a scanner flags.

Two small simplifications fell out and were taken because they *shorten* the diff, not as
refactoring: `AuthAntiforgeryTests.LoginThroughRenderedFormAsync` lost its `password` parameter
(it now generates the value it already used for both create and login, and no caller wanted it),
and `LoginRateLimitTests`'s private `NewSecret()` was deleted in favour of the shared helper.

### Literals removed
| Value | Sites |
| --- | --- |
| the unit 012 CSRF password | `AuthAntiforgeryTests` x6 |
| the shared `TestPassword…` admin/user password | `TestWebApplicationFactory`, `CookieApiCsrfTests`, 4 `Workflows/*` files |
| the cookie-test password | `AuthenticationTests` x2 |
| the audit-log user password | `LoginAuditLogTests` x2 |
| the question-repo hash input | `QuestionRepositoryTests` |
| the welcome-email model passwords | `EmailTemplateTests` x3 (incl. one assertion, now compared to the local) |
| the SMTP password | `TestWebApplicationFactory` |
| the README `appsettings.Development.json` example password | `README.md` |

The removed values are deliberately **not quoted anywhere in this file or in a code comment** — a
comment is scanned like any other line, so re-typing a removed literal to explain it would undo
the unit.

### Deliberately left alone — classified harmless, not secrets
Low-entropy dictionary or sentinel values that no scanner classifies as a credential, and which
the brief explicitly warns against replacing blindly:
- `SmtpPassword = "pass"` / `"password"` (`EmailNotificationServiceTests`, `SmtpClientFactoryTests`,
  `EmailSenderTests`) — generic words.
- `"realpassword"` / `"secretpassword"` (`EmailConfigurationProviderTests`, `EmailSenderTests`) —
  **load-bearing**: `IsSmtpConfigured` returns false for a placeholder and true for a
  non-placeholder, so these two values are the test's subject, not decoration.
- `"YOUR_APP_PASSWORD_HERE"` — the placeholder the production code matches on. Removing it would
  delete the test.
- `PasswordHash = "oldhash"` / `"newhash"` — audit-log change tracking, not credentials.
- `IDrive:AccessKey`/`SecretKey` and `Cloudflare:R2:*` = `test-…` — already the non-secret
  sentinels the brief asks for; hyphenated English, zero entropy.
- `VapidPublicKey`/`VapidPrivateKey` = `test-public-key` / `test-private-key`, and
  `P256dh = "BLBsY9NpGt2-M2i3...p256dh_key"` (truncated with a literal ellipsis) — sentinels.

### Out of scope, recorded not fixed
The repo-wide scan found two **production** password defaults in `src/`. Both are real
security questions and both belong to the already-planned password-policy unit; this phase was
forbidden to touch `src/`:
- `src/RTUB.Application/Data/Builders/MemberBuilder.cs:27` — a hardcoded default member password.
- `src/RTUB.Application/Data/SeedData.Member.cs:26` — the admin seed falls back to a hardcoded
  password when `AdminUser:Password` is unset. **This is the one worth acting on**: an unset
  config value silently creates a known-password admin in any environment.

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
**The `TestWebApplicationFactory` SQLite startup race** — serialize factory startup before the
hosted services run. It is now the most annoying thing in the suite: it hits any integration class
run in isolation at roughly 2 runs in 5, and it was explicitly out of scope for 015. Detail under
*Deferred* below.
Then: **Microsoft 10.0.11 -> 10.0.12 servicing train** across `src/` + tests, which also unblocks
`MockQueryable.Moq 10.0.12`.
Next *security* unit: **password policy** — Identity is currently `RequiredLength = 4` with every
complexity rule off (`AddIdentityServices`) — which should also absorb the two `src/` password
defaults unit 015 found (see *Out of scope, recorded not fixed* above). Then security headers / CSP.

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
- **Two hardcoded production password defaults in `src/`** (`MemberBuilder.cs:27`,
  `SeedData.Member.cs:26`). Out of scope by instruction; folded into the password-policy unit.
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

## Relevant files (unit 015)
- `tests/Shared/TestSecret.cs` — **new**, the only new file. One static class, one method.
- `tests/Directory.Build.props` — source-links `tests/Shared/*.cs` into all five test projects and
  adds the `RTUB.Tests` global using.
- `tests/RTUB.Integration.Tests/TestWebApplicationFactory.cs` — new `AdminPassword` property;
  `AdminUser:Password` and `EmailSettings:SmtpPassword` now generated.
- `tests/RTUB.Integration.Tests/AuthAntiforgeryTests.cs` — 6 literals removed; helper lost a
  parameter.
- `tests/RTUB.Integration.Tests/Api/CookieApiCsrfTests.cs` — signs the admin in with
  `Factory.AdminPassword`; `SignInAsync` is no longer `static` because it now reads `Factory`.
- `tests/RTUB.Integration.Tests/AuthenticationTests.cs`, `LoginRateLimitTests.cs`, and 4
  `Workflows/*.cs` files.
- `tests/RTUB.Application.Tests/Data/LoginAuditLogTests.cs`,
  `tests/RTUB.Application.Tests/Repositories/QuestionRepositoryTests.cs`.
- `tests/RTUB.Web.Tests/Services/EmailTemplateTests.cs`.
- `README.md` — one example value in the `appsettings.Development.json` block.

## Tests (0 new)
**No test was added, removed, renamed or re-asserted.** This unit changes only where a test's
password comes from. The one assertion that compared against a password literal
(`EmailTemplateTests.WelcomeEmailModel_ShouldHaveCorrectProperties`) now compares against the
local that was used to set it, so it still proves the round-trip. No randomness was introduced
into any assertion unrelated to credentials.

## Latest validation (unit 015)
- Release build, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true` and
  `EnforceCodeStyleInBuild=true`. The source-link and the global using resolve in all five test
  projects.
- Focused first: `RTUB.Integration.Tests` alone — **246 total, 244 passed, 0 failed, 2 skipped.**
- Full suite, `dotnet test --no-build -c Release`: **4497 passed, 0 failed, 60 skipped** —
  **identical to the baseline.** No count moved in either direction, which is the point.
- **Repository-wide credential scan, not just the diff.** Two passes over `git ls-files` output
  (so tracked files only, `src/` included):
  1. a password-shaped-literal regex (mixed case + digit, 8-64 chars) across `.cs`, `.razor`,
     `.json`, `.yml`, `.props`, `.ts`, `.js`, `.md`;
  2. a credential-keyed-assignment regex (`password|secret|accesskey|apikey|clientsecret|…` on the
     left of `=` or `:`) across `tests/`.
  Residue after the fix is the classified-harmless list above plus enum/nickname/filename false
  positives. The scan also caught two things a diff review would have missed: the README example
  password, and a first draft of `TestSecret.cs` whose own doc comment quoted the removed literal
  back — both fixed.
- Tracked `appsettings.json` / `appsettings.Production.json` contain **no** password, secret, key,
  token or connection-string entries. No real secret was found anywhere; nothing needed escalation.
- `git diff --check` clean. **Migrations and `ApplicationDbContextModelSnapshot.cs` unchanged.**
  **No `src/` file changed.**
- `git status` = 14 modified (13 test files + `README.md`), 1 new untracked directory
  (`tests/Shared/`). Two workflow files briefly showed as modified from a `sed -i` line-ending
  rewrite with no content change; restored to CRLF and they dropped out.
- Graphify **not** rebuilt — no application structure changed.

## Previous validation (unit 014 — merged)
`POST /auth/login` carries a per-client-IP rate limit: a named `login` policy, partitioned fixed
window, **10 permits / 5 minutes**, `QueueLimit = 0`, 429 with `Retry-After` from lease metadata,
configured by `LoginRateLimit:*` in `appsettings.json`. `UseRateLimiter()` sits immediately after
`UseRouting()`, so a throttled client is answered before any credential or antiforgery work runs.
Partitioned by `RemoteIpAddress` and not by username: a caller-controlled key would allow an
unbounded-partition memory DoS, and Identity's 5-failure lockout already covers the per-account
case. 8 tests in `tests/RTUB.Integration.Tests/LoginRateLimitTests.cs`, using a test-only
`IStartupFilter` to give each test a distinct client IP. Suite 4497 / 0 / 60. The Azure
forwarded-headers deployment requirement above is still open. Detail is in the
`fix/014/login-rate-limiting` history.

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
