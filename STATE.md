# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-20_

## Phase
Tooling unit **008 (make Graphify operational) — implementation complete, uncommitted, awaiting
owner review.** CLI, Claude skill and MCP are all **operational**: the MCP server is connected and
verified from inside Claude, the conditional Graphify usage policy is established and the generated
graph is gitignored. Nothing outstanding.
Modernization Phase 2.2.2 and everything before it are merged to `dev`.

## Branch
`chore/008/enable-graphify`, branched from `dev`. Uncommitted (no commit authorized).
`chore/001`–`chore/007` still present; delete when convenient.

## Last completed step
**Unit 008 — Graphify is operational.**

- **CLI:** `graphifyy 0.9.56` (PyPI, `Graphify-Labs/graphify`, Apache-2.0) in an isolated venv at
  `C:\Users\Victus\.local\graphify-venv`. **Not** an RTUB dependency — no csproj, package.json or
  lockfile touched. Its `Scripts/` dir is first on the *user* PATH.
- **Claude integration:** project-scoped. `.claude/skills/graphify/` (skill + 8 references),
  `.claude/CLAUDE.md` (skill registration), `.mcp.json` (project MCP server `graphify-mcp`).
  The installer's `PreToolUse` hooks were **removed** from `.claude/settings.json` — they fire on
  every Bash/Grep/Read/Glob and mandate `graphify query`, which the effectiveness test showed is the
  weakest entry point. `.claude/settings.json` is back to byte-identical with `dev`.
  Root `CLAUDE.md` keeps our own concise wording; the installer's 10-line block was reverted.
- **Scope:** `.graphifyignore` (44 lines) on top of `.gitignore`. Excludes `graphify-out/`,
  `.git/`, the 21 MB / 321-file generated `src/RTUB.Web/Migrations/`, vendored `wwwroot/lib/`,
  minified/map files, binary media/fonts, and local DB + credential patterns. No secret or local
  database content is indexed; tracked `appsettings*.json` were checked and are placeholder-only.
- **Graph:** build `graphify extract . --code-only` — 1325 code files, no LLM, no API key, 73 s ->
  **19,928 nodes / 47,685 edges / 699 communities**. After one `graphify update .`:
  **20,354 / 48,074 / 734**, 88% EXTRACTED / 12% INFERRED / 0% AMBIGUOUS.
  Incremental update re-extracted **95 of 1325 files (7%)** — the SHA256 cache works; the 81 s wall
  time is clustering + HTML, not extraction.
- **MCP:** `.mcp.json` declares one stdio server, `command: "graphify-mcp"`, arg
  `graphify-out/graph.json`, empty `env`. No secret, no absolute path, no venv path — portable and
  committable as-is; left unchanged.
  Verified end-to-end by JSON-RPC probe against the venv `graphify-mcp.exe`: `initialize` returns
  `graphify 0.9.56`, `tools/list` returns 10 tools, `graph_stats` returns 20,354 / 48,074 / 734,
  `get_neighbors IPushNotificationService` returns forward *and* reverse (`<--`) edges with exact
  `file:line`, `shortest_path PushNotificationService -> IPushNotificationService` returns the
  1-hop `implements` edge. Three spot-checks against source matched exactly.
- **Ponytail:** installed as a global plugin (`ponytail@ponytail` 4.10.0, user scope), loads at
  SessionStart at level `full`. Not an RTUB dependency — nothing in the repo references it, and
  `~/.claude/skills/` is empty (no duplicate manual skill).

## SQLite vulnerability outcome
- **Before:** `SQLitePCLRaw.lib.e_sqlite3 2.1.11` — High, `GHSA-2m69-gcr7-jv3q`.
- **After:** transitive graph resolves `SQLitePCLRaw.* 2.1.12` (bundle/core/lib/provider).
  `GHSA-2m69-gcr7-jv3q` **gone** from every project. No direct SQLitePCLRaw override added —
  the normal dependency graph resolved it.

## Warning suppressions
Both retained, re-verified diagnostically by rebuilding with `NU1903` and `NETSDK1206`
removed from `NoWarn`:
- `NU1903` — now caused **only** by `Microsoft.NETCore.App 1.0.5` + `Microsoft.NETCore.Jit 1.0.7`
  via `WebPush-NetCore 1.0.2`. SQLitePCLRaw no longer contributes.
- `NETSDK1206` — Libuv RIDs via the same `WebPush-NetCore` chain. Unchanged.
Comment block in `Directory.Build.props` updated to record the new cause set. Nothing removed,
nothing added.

## Current task
None active.

## Last housekeeping unit
Obsolete `docs/cloudflare-account-migration-runbook.md` deleted (owner-confirmed), plus its two
routing references in `CLAUDE.md` and `docs/architecture/system-index.md`. The file's pre-existing
unstaged edits went with it — intended, the file itself is obsolete.
`docs/cloudflare-r2-and-database-backups.md` untouched. Docs-only: no build, no tests.

## Next unit
**Phase 2.3 — replace `WebPush-NetCore 1.0.2`.** Sole cause of `NU1903` + `NETSDK1206`, and the only
reason the Newtonsoft floor pin still exists. Test-stack modernization (xunit v3) is a separate,
later unit.

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
- **Remaining dependency findings (unchanged by 2.2.1):**
  - `WebPush-NetCore 1.0.2` → Push modernization phase. Sole remaining cause of `NU1903`
    (`GHSA-7mfr-774f-w5r9`, `GHSA-8884-xcr4-r68p`, `GHSA-xcvr-qv8h-m7xw`) and `NETSDK1206`.
    Also deprecated: `Microsoft.NETCore.App 1.0.5`, `Microsoft.NETCore.Runtime.CoreCLR 1.0.7`.
  - `xunit 2.9.2` family deprecated (Legacy → `xunit.v3`) across all 5 test projects.
  - Test stack still old: `Microsoft.NET.Test.Sdk 17.12.0`, `xunit.runner.visualstudio 2.8.2`,
    `MockQueryable.Moq 8.0.0`, `coverlet.collector 6.0.2`, `bunit 2.7.2`, `AngleSharp 1.5.2`.
  - `QuestPDF 2024.10.3`, AWS SDK, `Microsoft.Playwright 1.50.0` — independent version trains.
- **Newtonsoft.Json final removal** — blocked on `WebPush-NetCore`. The `RTUB.Application` floor
  pin plus its `PackageVersion` are the only Newtonsoft declarations left; delete both together
  with the WebPush replacement.
- **Phase 1C:** remaining optional custom skills — deliberately not created.
- Work-branch cleanup (`chore/001`–`chore/007`) — delete when convenient.
- Two `graphifyy 0.9.56 + MCP` installs (isolated venv, Microsoft-Store Python user site). The
  Store one wins PATH and works; both are compatible, so neither needs removing.
- Pending feature work — unchanged, not part of any phase.

## Relevant files
- `Directory.Packages.props` — Newtonsoft floor-pin rationale comment (Phase 2.2.2); 11 Microsoft
  packages → 10.0.11 (Phase 2.2.1).
- `src/RTUB.Application/RTUB.Application.csproj`, `src/RTUB.Shared/RTUB.Shared.csproj` — Newtonsoft
  floor-pin rationale comments.
- `Directory.Build.props` — suppression comment updated (SQLitePCLRaw cause removed).

## Latest validation (Phase 2.2.2)
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
