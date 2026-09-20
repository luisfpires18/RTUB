# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-20_

## Phase
Modernization unit **009 (replace `WebPush-NetCore 1.0.2`) — implementation complete, uncommitted,
awaiting owner review.** `WebPush-NetCore` is gone, the Newtonsoft floor pin is gone, and both
`NU1903` / `NETSDK1206` suppressions are gone. Solution now has **zero vulnerable packages**.
Tooling unit 008 (Graphify) and modernization Phase 2.2.2 and earlier are merged to `dev`.

## Branch
`chore/009/replace-webpush-netcore`, branched from `dev`. Uncommitted (no commit authorized).
`chore/001`–`chore/008` still present; delete when convenient.

## Last completed step
**Unit 009 — `WebPush-NetCore 1.0.2` replaced by `WebPush 1.0.13`.**

**Replacement chosen:** `WebPush 1.0.13` (`web-push-libs/web-push-csharp`, published 2026-04-28).
**Reason:** `WebPush-NetCore` is a fork of this exact library, so the API surface RTUB uses is
identical — `WebPushClient`, `SetVapidDetails`, `PushSubscription(endpoint, p256dh, auth)`,
`SendNotificationAsync(sub, payload, Dictionary<string,object>)`, `WebPushException.StatusCode`.
**Zero source changes** in `src/` — the swap is package-reference only. Ships a native `net10.0`
TFM and one transitive dependency, `Portable.BouncyCastle 1.9.0` (not deprecated, no advisories).

**Rejected:** `ClosureOSS.WebPush 2.5.7` — also maintained and `net10.0`-native, but a v2 API
redesign (DI `IWebPushClient`, different options model) and three transitive dependencies
(`Microsoft.Extensions.Logging.Abstractions 10.0.11`, `Microsoft.IdentityModel.{JsonWebTokens,Tokens} 8.22.0`).
Larger migration for no requirement RTUB has. Revisit only if `WebPush 1.x` stalls again.

**Behavior preserved verbatim** — `IPushNotificationService`, VAPID config/behavior,
`Enabled` / `IsConfigured()` split, Owner bypass, inbox-before-push, retry/backoff (3 attempts,
2^n transient / 3^n on 429), `TTL 86400`, `Urgency: high`, 404/410 subscription deletion, all
caller contracts. `PushNotificationService` was not refactored.

**VAPID key formats verified empirically** against `WebPush 1.0.13` (throwaway probe, not committed):
`SetVapidDetails` accepts base64url, standard base64, and either with padding. RTUB's base64url
storage format (`NormalizeBase64Url`) is accepted — the swap cannot silently disable sending.

**Files changed (4):** `Directory.Packages.props`, `Directory.Build.props`,
`src/RTUB.Application/RTUB.Application.csproj`,
`tests/RTUB.Application.Tests/Services/PushNotificationServiceTests.cs`.

## SQLite vulnerability outcome
- **Before:** `SQLitePCLRaw.lib.e_sqlite3 2.1.11` — High, `GHSA-2m69-gcr7-jv3q`.
- **After:** transitive graph resolves `SQLitePCLRaw.* 2.1.12` (bundle/core/lib/provider).
  `GHSA-2m69-gcr7-jv3q` **gone** from every project. No direct SQLitePCLRaw override added —
  the normal dependency graph resolved it.

## Warning suppressions
**Both removed.** `NoWarn` no longer lists `NU1903` or `NETSDK1206`, and the explanatory comment
block in `Directory.Build.props` went with them. Verified, not assumed: forced restore + Release
build with both suppressions absent gives **0 warnings, 0 errors** under `TreatWarningsAsErrors`.
Their sole remaining cause — `Microsoft.NETCore.App 1.0.5` / `Microsoft.NETCore.Jit 1.0.7` /
`Libuv 1.9.1` via `WebPush-NetCore` — left the graph with the package.

## Newtonsoft outcome — floor pin removed
The temporary direct `Newtonsoft.Json 13.0.4` reference in `RTUB.Application` and its
`PackageVersion` in `Directory.Packages.props` are **both deleted**. They existed only because
`WebPush-NetCore 1.0.2` declared a permissive `Newtonsoft.Json >= 9.0.1`; `WebPush 1.0.13` declares
no Newtonsoft dependency at all.

Verified with `dotnet nuget why` after a forced restore, not assumed:
- `RTUB.Application` — "does not have a dependency on Newtonsoft.Json". Same for `RTUB.Core`,
  `RTUB.Shared`.
- `RTUB` (Web) — `13.0.4`, transitive via `Microsoft.EntityFrameworkCore.Tools` → `.Design`
  (build/design-time only).
- Test projects — `13.0.1`, transitive via `Microsoft.NET.Test.Sdk 17.12.0` →
  `Microsoft.TestPlatform.TestHost`. `13.0.1` is the fixed version for `GHSA-5crp-9r3c-p9vr`.

No RTUB project declares Newtonsoft directly any more, and no serialization code was touched
(the migration to `System.Text.Json` completed in Phase 2.2).

## Current task
None active.

## Last housekeeping unit
Obsolete `docs/cloudflare-account-migration-runbook.md` deleted (owner-confirmed), plus its two
routing references in `CLAUDE.md` and `docs/architecture/system-index.md`. The file's pre-existing
unstaged edits went with it — intended, the file itself is obsolete.
`docs/cloudflare-r2-and-database-backups.md` untouched. Docs-only: no build, no tests.

## Next unit
**Test-stack modernization — `xunit 2.9.2` family → `xunit.v3`** across all 5 test projects, with
`Microsoft.NET.Test.Sdk 17.12.0`, `xunit.runner.visualstudio 2.8.2`, `bunit 2.7.2`,
`MockQueryable.Moq 8.0.0`, `coverlet.collector 6.0.2`, `AngleSharp 1.5.2` in the same unit.
This is now the only remaining deprecated-package cluster in the solution.

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

`explorer.exe` (PID 9396, running since the 2026-09-19 boot) caches the pre-reorder environment
block, and Claude Desktop inherits it from explorer — so neither a new session nor an app restart
ever picked up the reordered PATH. Rather than reorder further, the Microsoft-Store Python
user-site install was upgraded in place: **graphifyy 0.9.55 -> 0.9.56 with the `mcp` extra**
(`mcp 2.2.0`), using its own interpreter
`C:\Users\Victus\AppData\Local\Microsoft\WindowsApps\python3.13.exe`
(`pip install --user --upgrade "graphifyy[mcp]==0.9.56"`).

Both installs are now 0.9.56 + MCP, so whichever wins PATH works. The venv was left untouched.
Verified in-process: `graphify --version` = 0.9.56; the winning Store `graphify-mcp.exe` imports
`mcp.server.stdio` and answers `initialize` over stdio.

Claude's MCP client does not reconnect mid-session, so the upgrade only took effect in a new
session. **Confirmed operational 2026-09-20** in a fresh Claude session, via the MCP tools
themselves (not the CLI, not a manual JSON-RPC probe):
- `graph_stats` → **20,354 nodes / 48,074 edges / 734 communities**, 88% EXTRACTED / 12% INFERRED
  / 0% AMBIGUOUS — matches the built graph exactly.
- `get_node IPushNotificationService` → `src/RTUB.Application/Interfaces/IPushNotificationService.cs`
  L8, degree 67.
- `shortest_path PushNotificationService → IPushNotificationService` → 1-hop `implements`
  [EXTRACTED].

The chain Claude → `.mcp.json` → `graphify-mcp` → `graphify-out/graph.json` works end-to-end.
The Microsoft-Store Python install is the one winning PATH; `graphify --version` = 0.9.56. Which
of the two installs wins does not matter — both are 0.9.56 + MCP.

## Blockers
None.

## Deferred / owner decisions
- **Remaining dependency findings (after 009):**
  - `xunit 2.9.2` family deprecated (Legacy → `xunit.v3`) across all 5 test projects — **the only
    deprecated packages left in the solution.**
  - Test stack still old: `Microsoft.NET.Test.Sdk 17.12.0`, `xunit.runner.visualstudio 2.8.2`,
    `MockQueryable.Moq 8.0.0`, `coverlet.collector 6.0.2`, `bunit 2.7.2`, `AngleSharp 1.5.2`.
  - `QuestPDF 2024.10.3`, AWS SDK, `Microsoft.Playwright 1.50.0` — independent version trains.
  - `Portable.BouncyCastle 1.9.0` — new transitive via `WebPush 1.0.13`. Not deprecated, no
    advisories, but it is the legacy package id (`BouncyCastle.Cryptography` is the modern one).
    Nothing to do; note it if a future advisory lands.
- **Push subsystem debt (unchanged, out of scope for 009):** `WebPushClient` is still newed up
  inside `PushNotificationService`, so it cannot be mocked and no test covers an actual send,
  a retry, or 404/410 cleanup. Also: two service-worker registration paths, unbounded
  `BroadcastAsync` fan-out, no `CancellationToken`, no delivery metrics. All deliberately untouched.
- **Phase 1C:** remaining optional custom skills — deliberately not created.
- Work-branch cleanup (`chore/001`–`chore/008`) — delete when convenient.
- Two `graphifyy 0.9.56 + MCP` installs (isolated venv, Microsoft-Store Python user site). The
  Store one wins PATH and works; both are compatible, so neither needs removing.
- Pending feature work — unchanged, not part of any phase.

## Relevant files
- `Directory.Packages.props` — `WebPush 1.0.13` replaces `WebPush-NetCore 1.0.2`; Newtonsoft
  `PackageVersion` removed (unit 009). 11 Microsoft packages → 10.0.11 (Phase 2.2.1).
- `src/RTUB.Application/RTUB.Application.csproj` — `WebPush` reference; Newtonsoft floor pin and
  its rationale comment removed (unit 009).
- `Directory.Build.props` — `NU1903` / `NETSDK1206` suppressions and the whole rationale comment
  block removed (unit 009).
- `tests/RTUB.Application.Tests/Services/PushNotificationServiceTests.cs` — one added test,
  `Constructor_AcceptsRealVapidKeys_WithoutLoggingInvalidConfigurationWarning`.
- `src/RTUB.Application/Services/PushNotificationService.cs` — **unchanged**; the `using WebPush;`
  and every API call were already correct for the replacement package.

## Latest validation (unit 009)
- Forced restore (`--force-evaluate`): OK, all 9 projects.
- Build Release: **0 warnings, 0 errors** — with `NU1903` / `NETSDK1206` no longer suppressed and
  `TreatWarningsAsErrors=true`.
- Full test suite: **4478 passed, 0 failed, 60 skipped**. Baseline 4477 + the one added test.
  (Core 791, Application 1968, Shared 759, Web 735, Integration 225.)
- Focused push tests (`FullyQualifiedName~Push`, Application): **82 passed, 0 failed**.
- `dotnet list package --vulnerable --include-transitive`: **zero vulnerable packages in all 9
  projects** — first time in the modernization.
- `dotnet list package --deprecated --include-transitive`: only the pre-existing `xunit 2.x`
  family. Every `Microsoft.NETCore.*` 1.x entry is gone.
- Resolved graph check: `WebPush-NetCore` **absent**; `Microsoft.NETCore.App 1.0.5`,
  `Microsoft.NETCore.Jit 1.0.7`, `Microsoft.NETCore.Runtime.CoreCLR 1.0.7` and `Libuv 1.9.1`
  **all absent**. `WebPush 1.0.13` + `Portable.BouncyCastle 1.9.0` resolve everywhere.
- Newtonsoft final state verified with `dotnet nuget why` — see the Newtonsoft section above.
- Migrations / `ApplicationDbContextModelSnapshot.cs`: **unchanged** (nothing under
  `src/RTUB.Web/Migrations/` touched).
- `git diff --check` clean; secret scan clean. File encodings (BOM + CRLF) verified byte-identical
  to `HEAD` so the diff carries no whitespace noise.
- Graphify not rebuilt — no application structure change (`src/` has zero source edits).
- Frontend/Playwright not run — no frontend file touched.

## Previous validation (Phase 2.2.2)
- Code search across `src/` + `tests/` for every Newtonsoft API surface: **0 hits.**
- Forced restore (`--force-evaluate`) after the Shared-only removal: OK.
- Resolved Newtonsoft graph — `RTUB.Application` 13.0.4 (direct), `RTUB.Shared` **13.0.4
  (transitive)**, `RTUB` (Web) 13.0.4, `RTUB.Application.Tests` / `RTUB.Web.Tests` /
  `RTUB.Shared.Tests` / `RTUB.Integration.Tests` 13.0.4, `RTUB.Core.Tests` 13.0.1 (pre-existing,
  unchanged). **Identical to the pre-change baseline** — nothing downgraded.
- `dotnet list package --vulnerable --include-transitive`: **no Newtonsoft advisory** in any
  project. Remaining advisories are the unchanged WebPush-NetCore 1.x runtime set.
- Build Release: **0 warnings, 0 errors**.
- `RTUB.Shared.Tests`: **759 passed, 0 failed, 2 skipped** — matches the Phase 2.2.1 Shared
  baseline. Full suite not run: resolution is byte-identical to baseline, so nothing warranted it.
- Rejected first experiment (both references removed) is documented above; it is not in the diff.
- `git diff --check` clean; secret scan clean. Migrations untouched. Graphify not rebuilt
  (no application structure change).

## Previous validation (Phase 2.2.1)
- Clean restore (`--force-evaluate`, http-cache cleared): OK.
- Build Release: **0 warnings, 0 errors**.
- Full test suite: **4477 passed, 0 failed, 60 skipped** — identical to Phase 2.1 baseline.
  (Core 791, Application 1967, Shared 759, Web 735, Integration 225.)
- Vulnerability scan (incl. transitive): SQLitePCLRaw advisory cleared; only WebPush-NetCore
  1.x runtime advisories remain.
- Deprecated scan: WebPush-NetCore 1.x runtime packages + xunit 2.x family. No new entries.
- Resolved Microsoft 10.0.x packages: all on 10.0.11, no version skew.
- Migrations / `ApplicationDbContextModelSnapshot.cs`: **unchanged** (no files touched under
  `src/RTUB.Web/Migrations/`).
- `git diff --check` clean; secret scan clean.
