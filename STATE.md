# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-20_

## Phase
Modernization unit **013 (CSRF review of the remaining cookie-authenticated API endpoints) —
implementation complete, uncommitted, awaiting owner review.** Unit 012 is merged to `dev`.

## Branch
`fix/013/cookie-api-csrf`, branched from `dev` (clean, in sync with `origin/dev` at `1ac8abb9`).
Uncommitted — no commit authorized.
`chore/001`-`chore/011` and `fix/012` still present; delete when convenient.

## Last completed step
**Unit 013 — the five remaining cookie-authenticated mutations were classified against real
observed behavior, and only the one that was genuinely CSRF-reachable was addressed: it was
deleted, because nothing called it.** No antiforgery plumbing was added anywhere.

### Endpoints reviewed and CSRF classification

Every row was measured with a real authenticated `WebApplicationFactory` request, not reasoned
about. Anonymous callers hit the cookie challenge first (302 to `/login`, or 401 for `/api/push/*`).

| Endpoint | Auth | Form-POST result (authenticated) | Classification |
| --- | --- | --- | --- |
| `POST /api/admin/refresh-all` | cookie, `Roles = "Admin"` | **200 OK** with an arbitrary form body, and with no body at all | **exploitable form CSRF - and dead. Removed.** |
| `POST /api/push/subscribe` | cookie, `[Authorize]` | **415** for all three form enctypes | already protected by request shape |
| `POST /api/push/unsubscribe` | cookie, `[Authorize]` | **415** for all three form enctypes | already protected by request shape |
| `POST /api/push/broadcast` | cookie, `Roles = "Owner"` | **403** (role gate precedes binding); 415 for an Owner | already protected; **no browser caller** |
| `POST /api/push/send-to-selected` | cookie, `Roles = "Owner,Admin"` | **403**; 415 for an Owner/Admin | already protected; **no browser caller** |

`/auth/login` and `/auth/logout` already carry real antiforgery tokens (unit 012). The other three
controllers (`CdnProxy`, `DownloadMedia`, `Images`) expose `[HttpGet]` only — no mutations. That is
the complete set: a search for `MapPost|MapPut|MapDelete|MapPatch|MapMethods` and
`HttpPost|HttpPut|HttpDelete|HttpPatch` returns nothing else in the solution.

### The four mechanisms, kept distinct

They are **not** interchangeable, and each endpoint above is protected by a different one.

1. **Form CSRF** — a cross-site `<form>` can POST only `application/x-www-form-urlencoded`,
   `multipart/form-data` or `text/plain`. This is the attack `/api/admin/refresh-all` was open to:
   it binds nothing from the body, so *any* of those bodies reached the handler and returned 200.
2. **JSON API CSRF** — the `PushController` actions are `[ApiController]` + `[FromBody]`. Input
   formatter selection happens during model binding, **before the action runs**, and the JSON
   formatter reads only `application/json` / `text/json` / `application/*+json`. All three form
   enctypes were measured at **415 Unsupported Media Type**. This is a *server-side* gate and does
   not depend on any browser policy. Measured caveat: a **malformed** `multipart` body returns 400
   rather than 415 — the body is parsed before formatter selection. Both are pre-action
   rejections; the test uses well-formed browser-shaped bodies so it asserts the real 415.
3. **CORS** — there is **no CORS configuration in the solution at all**: no `AddCors`, no
   `UseCors`, no policy, no `AllowAnyOrigin`, no `AllowCredentials`. That is the safe default, not
   a gap. A cross-origin `fetch` sending `Content-Type: application/json` is non-simple, so it is
   preflighted; with no CORS middleware the `OPTIONS` never gets `Access-Control-Allow-Origin` and
   the browser never sends the real request. **Nothing unsafe was found, so nothing was changed.**
   Note the limit of this layer: a *simple* cross-origin POST (`text/plain`) is still **sent** —
   CORS only blocks reading the response — which is why mechanism 2 above, not CORS, is what
   actually protects the Push endpoints against that shape.
4. **SameSite** — the sign-in cookie is issued as
   `.AspNetCore.Identity.Application=...; path=/; samesite=lax; httponly` (observed on a real
   login response). `Lax` means the cookie is **not** attached to any cross-site POST, form or
   fetch, so the request arrives unauthenticated. Because the value is set *explicitly* by the
   framework, Chrome's 2-minute "Lax-allowing-unsafe" intervention — which applies only to cookies
   with no `SameSite` attribute — does not apply. This is a browser-side, defence-in-depth layer:
   it is porous to a same-site attacker (any sibling subdomain counts as same-site), which is why
   it was **not** treated as sufficient on its own for `/api/admin/refresh-all`.

### Why `/api/admin/refresh-all` was deleted rather than protected

It was CSRF-reachable (200 on an authenticated Admin form POST) **and provably unused**:

- A tracked-tree search found only its own definition and unit 012's STATE.md note. No `fetch`, no
  `HttpClient`, no `.http` / script / CI / doc reference anywhere.
- `git log --all -S"refresh-all"` returns exactly two commits: `213d0aa0` (which introduced it)
  and `bc70ceea` (012's STATE.md note). **It never had a caller in any commit on any branch.**
- Its service is alive and unaffected: `AdminRefreshService.TriggerRefreshAsync()` is called
  **in-process** from `AllCharacters.razor:520` after a game-data reset. The DI registration, the
  event, and both Razor subscribers are untouched.

So the endpoint was pure dead privileged attack surface. Deleting it removes the vulnerability
outright, with a 7-line diff and zero token plumbing — strictly smaller and safer than adding an
antiforgery mechanism to something nothing calls. The effect it triggered remains available from
the admin UI that actually uses it.

### Why the Push endpoints were left alone

Their protection is mechanism 2, which is server-side and already in force. Adding antiforgery
would have required inventing a way for `service-worker.js` to obtain a request token — it cannot
read a Razor DOM `<AntiforgeryToken />` — for no gain. **Push business logic, the WebPush service
architecture, notification behavior, roles, the auth cookie, service-worker registration and CORS
are all byte-identical.** `subscribe` / `unsubscribe` still work over `application/json`, proven by
test, so push subscription renewal is not at risk.

Recorded, not acted on: `broadcast` and `send-to-selected` have **no browser caller either** —
every Razor page calls `IPushNotificationService.BroadcastAsync` / `SendToSelectedUsersAsync`
in-process. They are candidates for the same deletion treatment, but unlike `refresh-all` they are
not CSRF-reachable, so removing them is cleanup, not security, and is out of this unit's scope.

### Changes (2 source files, 1 new test file)
- `src/RTUB.Web/Program.cs` — the `/api/admin/refresh-all` `MapPost` block deleted (7 lines).
  Nothing else in the file touched; it used fully-qualified names, so no `using` went stale.
- `src/RTUB.Web/Services/AdminRefreshService.cs` — two lines added to the class doc recording that
  the service is in-process only and deliberately has no HTTP endpoint, so the route is not
  re-added later.
- `tests/RTUB.Integration.Tests/Api/CookieApiCsrfTests.cs` — **new**, 4 tests.

### Tests (4 new)
1. `RefreshAllEndpoint_NoLongerExists` — signs in as the seeded **Admin**, so `404` proves the
   route is gone rather than access-denied.
2. `PushJsonEndpoint_RejectsEveryContentTypeACrossSiteFormCanPost` — posts all three real form
   enctypes (`FormUrlEncodedContent`, `MultipartFormDataContent`, `text/plain`) to
   `/api/push/unsubscribe` with a valid sign-in cookie; each must be **415**.
3. `PushJsonEndpoint_AcceptsApplicationJsonFromRealCaller` — the same endpoint with
   `application/json` returns **200**, the positive control proving (2) is a content-type gate and
   not a blanket block on the service worker.
4. `AuthenticationCookie_IsSameSiteLax` — asserts the real `Set-Cookie` contains `samesite=lax`,
   so a later change to `SameSite=None` cannot silently reopen CSRF across every cookie POST.

All four sign in through the **real rendered login form**, so unit 012's antiforgery is exercised
rather than bypassed. `AntiforgeryFormToken` is reused from `AuthAntiforgeryTests.cs`; that file is
not modified.

**Negative probe run and reverted.** The endpoint was temporarily re-added to `Program.cs`;
`RefreshAllEndpoint_NoLongerExists` then **failed**, and passed again once it was removed. The test
gates on the actual deletion.

## Current task
None active.

## Next unit
**Microsoft 10.0.11 -> 10.0.12 servicing train** across `src/` + tests, which also unblocks
`MockQueryable.Moq 10.0.12`.
Next *security* unit: **login rate limiting** — `/auth/login` has none, so password attempts are
unlimited per IP; Identity lockout is per-account only. Then password policy, then security
headers / CSP.

## Blockers
None.

## Deferred / owner decisions

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
  fire in the full suite (4489 / 0 / 60, clean). Worth its own unit: serialize factory startup
  before the hosted services run.
- Out of scope by instruction and untouched: login rate limiting, password policy, MFA, cookie
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

## Relevant files (unit 013)
- `src/RTUB.Web/Program.cs` — `/api/admin/refresh-all` removed.
- `src/RTUB.Web/Services/AdminRefreshService.cs` — doc note: in-process only, no HTTP endpoint.
- `tests/RTUB.Integration.Tests/Api/CookieApiCsrfTests.cs` — **new**; 4 tests.

## Latest validation (unit 013)
- Release build, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true` and
  `EnforceCodeStyleInBuild=true`.
- Full suite, `dotnet test --no-build -c Release`: **4489 passed, 0 failed, 60 skipped.**
  Baseline was 4485 / 0 / 60; **+4 = exactly the 4 new tests.** No test was added or removed for
  the deleted endpoint — it never had one — and no other count moved.
- New class run 3x in isolation: 4 / 0 / 0 each time.
- Negative probe run and reverted (see Tests above) — the removal test fails with the route present.
- `git diff --check` clean. Secret scan clean: the only credential-shaped string is
  `TestPassword123!`, which is the pre-existing seeded admin password already in
  `TestWebApplicationFactory.cs`. No new secret, no VAPID or token material.
- `git status` = 2 modified source files, 1 new untracked test file. **Migrations and
  `ApplicationDbContextModelSnapshot.cs` unchanged.** New file normalized to CRLF to match siblings.
- Graphify **not** rebuilt — deleting one endpoint lambda is not a material structural change.
  Playwright **not** run: every browser-relevant property (cookie `SameSite`, content-type
  rejection) was proven server-side by integration test instead.

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
