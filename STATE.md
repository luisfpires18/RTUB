# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-20_

## Phase
Modernization unit **012 (antiforgery on the browser authentication POSTs) — implementation
complete, uncommitted, awaiting owner review.** Unit 011 is merged to `dev`.

## Branch
`fix/012/auth-antiforgery`, branched from `dev` (clean, in sync with `origin/dev` at `0c918bc4`).
Uncommitted — no commit authorized.
`chore/001`–`chore/011` still present; delete when convenient.

## Last completed step
**Unit 012 — `POST /auth/login` and `POST /auth/logout` now require a valid ASP.NET Core
antiforgery token.** Authentication logic itself is untouched.

### Antiforgery status

| Endpoint | Before | After |
| --- | --- | --- |
| `POST /auth/login` | `.DisableAntiforgery()` | token required, 400 before handler runs |
| `POST /auth/logout` | `.DisableAntiforgery()` | token required, 400 before handler runs |

### Framework finding (verified against .NET 10 docs **and** ASP.NET Core `release/10.0` source)

Three assumptions were checked, and two of the obvious approaches would have silently failed.

1. **Plain `<form>` does not emit a token.** The `AntiforgeryToken` component is added
   automatically **only to `EditForm`**. For an HTML `<form>` element the docs say to add
   `<AntiforgeryToken />` manually. Both RTUB auth forms are plain `<form>` elements, so both
   needed it. Failed validation ⇒ **400 Bad Request**.
2. **`.RequireAntiforgery()` does not exist.** The comment left in `Program.cs` pointed at an API
   that is not in the framework: `src/Http/Routing/src/PublicAPI.Shipped.txt` lists only
   `DisableAntiforgery`, and `Microsoft.AspNetCore.Routing.dll` in the 10.0.4 ref pack contains no
   `RequireAntiforgery` member (only `RequireAntiforgeryTokenAttribute`, in the Antiforgery
   assembly, which is metadata-only).
3. **Removing `.DisableAntiforgery()` alone would have protected nothing.**
   `AntiforgeryMiddleware.Invoke` validates only when the endpoint carries
   `IAntiforgeryMetadata { RequiresValidation: true }`, and it **never short-circuits** — it only
   sets `IAntiforgeryValidationFeature`. The 400 is produced inside
   `RequestDelegateFactory.TryReadFormAsync`, which runs **only for form-bound parameters**.
   `RequestDelegateFactory.InferAntiforgeryMetadata` likewise fires only when
   `factoryContext.ReadForm` is true. A handler that calls `HttpContext.Request.ReadFormAsync()`
   by hand gets neither the metadata nor the gate.

**Mechanism chosen:** bind the form through the handler signature (`IFormCollection`), which is
the framework's own documented default — "Minimal APIs that accept form data require antiforgery
token validation and fail before running application code". No custom CSRF code, no filter, no
manual `IAntiforgery` call.

### Changes (3 source files, 2 test files)
- `Program.cs` — login handler parameter `HttpContext http` → `IFormCollection form`; the
  `await http.Request.ReadFormAsync()` line deleted (the bound collection is the same
  `IFormCollection`). Logout handler gains an `IFormCollection form` parameter. Both
  `.DisableAntiforgery()` calls and the misleading `.RequireAntiforgery()` comment removed.
  Both endpoints carry a comment saying the parameter *is* the protection, so it is not deleted
  later as dead code.
- `Login.razor`, `MainLayout.razor` — `<AntiforgeryToken />` added inside each form.
  `Microsoft.AspNetCore.Components.Forms` is already in `src/RTUB.Web/_Imports.razor`.

**Nothing else changed.** Credential validation, email-confirmation check, lockout,
`AccessFailedAsync` / `ResetAccessFailedCountAsync`, expelled-user handling, `LastLoginDate`,
audit context, the duplicate-log memory cache, `RememberMe`, `SignInAsync`, the
`UrlHelper.IsLocalUrl` return-URL guard and every redirect are byte-identical.
`app.UseAntiforgery()` and `AddAntiforgery(o => o.HeaderName = ...)` untouched. No migrations.

### Why the forms render a token at all
`App.razor` has no `@rendermode`, so `Router` → `AuthorizeRouteView` → `MainLayout` all render
**static SSR**; only pages marked `@rendermode InteractiveServer` are interactive islands inside
that static shell. Static SSR is exactly where `<AntiforgeryToken />` works, and the logout form
therefore gets a fresh token on every full page load. Confirmed by test, not by reasoning:
the logout test reads the token out of a real `GET /` response.

### Tests
New `tests/RTUB.Integration.Tests/AuthAntiforgeryTests.cs` — **7 tests**, all driving the real
browser flow (GET the page, read `__RequestVerificationToken` out of the rendered HTML, POST it
with cookies). No token is ever manufactured and antiforgery is not weakened for tests.

1. `LoginPage_RendersAntiforgeryToken`
2. `LoginPost_WithoutAntiforgeryToken_IsRejected` — 400, no sign-in cookie
3. `LoginPost_WithTamperedAntiforgeryToken_IsRejected` — real cookie, corrupted token → 400
4. `LoginPost_WithTokenFromRenderedForm_SignsUserIn` — 302 → `/`, sign-in cookie issued
5. `LogoutPost_WithoutAntiforgeryToken_IsRejected` — 400
6. `LogoutPost_WhileAuthenticated_WithoutAntiforgeryToken_KeepsSessionSignedIn` — 400 and the
   session survives
7. `LogoutPost_WithTokenFromRenderedForm_SignsUserOut` — 302 → `/`, sign-in cookie cleared

Changed: `AuthenticationTests.CookieValidation_UpdatesLastLoginDate` posted straight to
`/auth/login`; it now fetches `/login` and submits the rendered token. Only the login step
changed — its `LastLoginDate` assertions are untouched.

**Both test traps were found and closed:**
- **Negative probe.** `.DisableAntiforgery()` was temporarily restored on both endpoints; all four
  rejection tests then **failed**, and passed again once it was removed. The tests gate on real
  enforcement, not on an unrelated 400.
- **First draft passed for the wrong reason.** The logout rejection tests posted an *empty* form
  body, which `RequestDelegateFactory` rejects with 400 as "request without body" — so they passed
  even with antiforgery disabled. They now post a non-empty tokenless body, with a comment
  recording why.

## Current task
None active.

## Next unit
**013 — Microsoft 10.0.11 → 10.0.12 servicing train** across `src/` + tests, which also unblocks
`MockQueryable.Moq 10.0.12`.
Next *security* unit, if security is the preferred track: **CSRF coverage for the remaining
cookie-authenticated POST endpoints** (see Deferred), then login rate limiting.

## Blockers
None.

## Deferred / owner decisions

### Security, recorded by 012 — explicitly out of scope, nothing changed
- **Other cookie-authenticated POST endpoints have no antiforgery**: `/api/admin/refresh-all`
  (Admin-role authorized) and the four `PushController` actions (`subscribe`, `unsubscribe`,
  `broadcast`, `send-to-selected`). They take JSON rather than a form enctype, which limits
  classic form-based CSRF, but they were not reviewed or hardened in this unit.
- **`AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN")`** is configured but no endpoint or client
  currently sends a header token. Harmless; either use it or drop it.
- **PWA note, no action needed.** `service-worker.js` serves HTML network-first, cache-fallback,
  so a cached `/login` page is only returned when the network is down — and login needs the
  network anyway. No stale-token path introduced.
- Out of scope by instruction and untouched: login rate limiting, password policy, MFA, cookie
  validation / SQLite pressure, security headers / CSP, authorization redesign, `Program.cs`
  refactor.
- Known and accepted framework limitation: antiforgery tokens are bound to the user identity, so
  multiple tabs signed in as different users (or one anonymous, one signed in) are unsupported.
  This is documented ASP.NET Core behavior, not an RTUB defect.

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

## Relevant files (unit 012)
- `src/RTUB.Web/Program.cs` — both auth endpoints; `IFormCollection` parameters, no
  `DisableAntiforgery`.
- `src/RTUB.Web/Pages/Identity/Login.razor` — `<AntiforgeryToken />` in the login form.
- `src/RTUB.Web/Shared/MainLayout.razor` — `<AntiforgeryToken />` in the logout form.
- `tests/RTUB.Integration.Tests/AuthAntiforgeryTests.cs` — **new**; 7 tests + the HTML token
  reader shared with `AuthenticationTests`.
- `tests/RTUB.Integration.Tests/AuthenticationTests.cs` — login step now uses the rendered form.

## Latest validation (unit 012)
- Release build, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true` and
  `EnforceCodeStyleInBuild=true`.
- Full suite, `dotnet test --no-build -c Release`: **4485 passed, 0 failed, 60 skipped.**
  Baseline was 4478 / 0 / 60; **+7 = exactly the 7 new antiforgery tests**, no other count moved.
- `RTUB.Integration.Tests` alone: 232 passed / 0 failed / 2 skipped (was 225 / 0 / 2).
- Negative probe run and reverted (see Tests above) — 4 rejection tests fail without the fix.
- `git diff --check` clean. Secret scan clean: only synthetic test passwords (`CsrfTest123!`),
  matching the existing `CookieTest123!` convention. No real token value is ever asserted or
  logged — the framework logs antiforgery failures at Debug with the parameter name only.
- `git status` = 3 modified source files, 1 modified test file, 1 new test file. **Migrations and
  `ApplicationDbContextModelSnapshot.cs` unchanged.**
- Graphify **not** rebuilt — no application structure change (two handler signatures and two
  markup lines). Frontend build / Playwright not run; not required.

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
