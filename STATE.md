# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-20_

## Phase
Modernization unit **010 (modernize the .NET test dependency stack) - implementation complete,
uncommitted, awaiting owner review.** Unit 009 (`WebPush`) is merged to `dev`. The whole test
stack is now current except xUnit itself, which is deliberately held back (see below).

## Branch
`chore/010/modernize-test-dependencies`, branched from `dev`. Uncommitted (no commit authorized).
`chore/001`-`chore/009` still present; delete when convenient.

## Last completed step
**Unit 010 - test dependency stack modernized. Package/config only; zero source edits.**

**Updated (7 packages):**

| Package | From | To | Note |
| --- | --- | --- | --- |
| `xunit` | 2.9.2 | 2.9.3 | final v2 patch; stays on v2 by decision |
| `xunit.runner.visualstudio` | 2.8.2 | 4.0.0 | still the VSTest adapter; runs v1/v2/v3 |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | 18.10.1 | still VSTest (`microsoft/vstest`, branch 18.10) |
| `coverlet.collector` | 6.0.2 | 10.0.1 | still a VSTest data collector |
| `bunit` | 2.7.2 | 2.11.3 | net10.0 TFM; needs AngleSharp >= 1.8.1 |
| `MockQueryable.Moq` | 8.0.0 | **10.0.8** | *not* 10.0.12 - see below |
| `FluentAssertions` | 8.8.0 | 8.11.0 | same major, same (Xceed) license terms |

`Moq 4.20.72` and `AutoFixture 4.18.1` were already the latest stable releases - no action.

**`MockQueryable.Moq` held at 10.0.8 deliberately.** 10.0.12 requires
`Microsoft.EntityFrameworkCore 10.0.12`, which would drag EF Core in the test projects above the
10.0.11 the application resolves - tests would then run against a different EF build than
production. 10.0.8 requires EF Core 10.0.8, so the repo's 10.0.11 still wins. Verified in
`project.assets.json`: **EF Core resolves 10.0.11 in every test project.** Revisit when the
Microsoft 10.0.11 -> 10.0.12 train is taken as its own unit.

**Removed (4 references, 3 package ids):**
- `AngleSharp` direct `PackageReference` from `RTUB.Shared.Tests` and `RTUB.Web.Tests`, plus its
  `PackageVersion`. **Zero `AngleSharp` occurrences in test source**; bUnit supplies it
  transitively (resolves **1.8.1**). Keeping the 1.5.2 pin would have been an NU1605 downgrade
  against bUnit 2.11.3 anyway.
- `AutoFixture` direct `PackageReference` from `RTUB.Web.Tests` - zero usages there. The
  `PackageVersion` stays: `RTUB.Shared.Tests` genuinely uses it (7 files).
- `Microsoft.Playwright` / `Microsoft.Playwright.Xunit` `PackageVersion` entries - **zero
  references anywhere in the repo**: no project, no source file, no workflow. Dead declarations.

**Retained on purpose (tooling, not source dependencies):** `Microsoft.NET.Test.Sdk`,
`xunit.runner.visualstudio` and `coverlet.collector` have no `using` in any test file and were
*not* treated as unused - they are the build / runner / coverage integration, and CI depends on
all three.

**Side effect, verified not assumed:** `Newtonsoft.Json` is now **absent from all 5 test
projects**. It was previously 13.0.1, transitive via `Microsoft.NET.Test.Sdk 17.12.0` ->
`Microsoft.TestPlatform.TestHost`; Test SDK 18.10.1 no longer pulls it. No RTUB project resolves
Newtonsoft anywhere now except `RTUB` (Web), where it is design-time only via EF Core Tools.

**CI unchanged and re-verified.** `.github/workflows/ci.yml` was not edited. Its exact flags
(`--collect:"XPlat Code Coverage" --logger "trx;..."`) were used as the validation command and
still produce `coverage.cobertura.xml` (5 files) and `test-results.trx`, so both upload globs
still match.

**Files changed (3):** `Directory.Packages.props`,
`tests/RTUB.Shared.Tests/RTUB.Shared.Tests.csproj`,
`tests/RTUB.Web.Tests/RTUB.Web.Tests.csproj`.
No `src/`, no test source, no migrations, no workflow.

## xUnit v3 decision - DEFERRED to unit 011
**Not migrated in 010. The blocker is the runner, not the test code.**

`xunit.v3 4.0.1` takes a hard dependency on `xunit.v3.mtp-v2` - it *requires*
Microsoft.Testing.Platform v2 and is no longer a VSTest framework. So v3 is not a package bump; it
replaces the entire run and coverage pipeline.

Measured source surface in this repo - **much smaller than the config surface**:

| Surface | Count | v3 impact |
| --- | --- | --- |
| `IAsyncLifetime` | **0** | none (the usual `Task`->`ValueTask` break does not apply here) |
| `ITestOutputHelper` | **0** | none |
| `MemberData` / `ClassData` / `TheoryData` | **0** | none |
| `Assert.Raises` / `Assert.PropertyChanged` | **0** | none |
| `IClassFixture` / `ICollectionFixture` / `CollectionDefinition` | 45 files | source-compatible |

Config / CI surface, which is the real cost: drop `Microsoft.NET.Test.Sdk` +
`xunit.runner.visualstudio` for the MTP runner; test projects become executables; rewrite
`tests/RTUB.Application.Tests/xunit.runner.json` to the v3 schema; replace `coverlet.collector`
(a VSTest data collector) with MTP coverage; and rewrite the CI test step's `--collect` /
`--logger` flags to their MTP equivalents. Across 5 test projects plus `ci.yml`.

Not blocked by bUnit: bUnit 2.x core is test-framework-agnostic (`bunit.xunit` is a dead
1.0.0-preview package and is not used here). MockQueryable and Moq are framework-agnostic too.

**Conclusion:** the test *code* would likely migrate almost untouched; the runner, coverage and CI
config would not. Per the split rule, 010 stays a routine package update and v3 gets a dedicated
unit where the CI pipeline change can be validated on its own.

## Current task
None active.

## Last housekeeping unit
Obsolete `docs/cloudflare-account-migration-runbook.md` deleted (owner-confirmed), plus its two
routing references in `CLAUDE.md` and `docs/architecture/system-index.md`. The file's pre-existing
unstaged edits went with it — intended, the file itself is obsolete.
`docs/cloudflare-r2-and-database-backups.md` untouched. Docs-only: no build, no tests.

## Next unit
**011 - xUnit v3 / Microsoft.Testing.Platform migration.** Scope as above. This is the last
deprecated-package cluster in the solution. Treat the CI test step and the coverage artifacts as
the risky part, not the test source.

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
- **Remaining dependency findings (after 010):**
  - `xunit 2.9.3` family still deprecated (Legacy -> `xunit.v3`) in all 5 test projects - **the
    only deprecated packages left in the solution.** Owned by unit 011.
  - `MockQueryable.Moq 10.0.12` - held at 10.0.8; gated on the EF Core 10.0.12 bump (above).
  - Microsoft 10.0.11 -> **10.0.12** train (ASP.NET Core, EF Core, Identity, Mvc.Testing) - one
    servicing bump across src + tests. Not started.
  - `AWSSDK.Core 4.0.3.8` -> 4.0.102.6 and `AWSSDK.S3 4.0.10` - independent train. Not started.
  - `QuestPDF 2024.10.3` -> 2026.9.0 - major train, likely breaking. Not started.
  - `AWSSDK.Core` is referenced directly by `RTUB.Integration.Tests`, `RTUB.Shared.Tests` and
    `RTUB.Web.Tests` but **used by none of them** (only `RTUB.Application.Tests` uses `Amazon.*`).
    Three dead direct references. Left alone in 010 - that is the AWS train, not the test stack.
  - `Portable.BouncyCastle 1.9.0` - transitive via `WebPush 1.0.13`. Not deprecated, no
    advisories, but it is the legacy package id (`BouncyCastle.Cryptography` is the modern one).
    Nothing to do; note it if a future advisory lands.
- **Test-suite hygiene, noted not acted on:** `RTUB.Web.Tests` skips **56 of 791** tests (mostly
  `*PageTests` modal / button cases). Pre-existing and unchanged by 010, but worth a look.
- **Push subsystem debt (unchanged):** `WebPushClient` is still newed up inside
  `PushNotificationService`, so it cannot be mocked and no test covers an actual send, a retry, or
  404/410 cleanup. Also: two service-worker registration paths, unbounded `BroadcastAsync`
  fan-out, no `CancellationToken`, no delivery metrics. All deliberately untouched.
- **Phase 1C:** remaining optional custom skills - deliberately not created.
- Work-branch cleanup (`chore/001`-`chore/009`) - delete when convenient.
- Two `graphifyy 0.9.56 + MCP` installs (isolated venv, Microsoft-Store Python user site). The
  Store one wins PATH and works; both are compatible, so neither needs removing.
- Pending feature work - unchanged, not part of any phase.

## Relevant files
- `Directory.Packages.props` - 7 test-package versions raised; `AngleSharp`,
  `Microsoft.Playwright` and `Microsoft.Playwright.Xunit` `PackageVersion` entries removed (010).
  Still carries `WebPush 1.0.13` (009) and the Microsoft 10.0.11 set (Phase 2.2.1).
- `tests/RTUB.Shared.Tests/RTUB.Shared.Tests.csproj` - `AngleSharp` reference removed (010).
- `tests/RTUB.Web.Tests/RTUB.Web.Tests.csproj` - `AngleSharp` + `AutoFixture` references removed (010).
- `.github/workflows/ci.yml` - **unchanged**; its flags were used as the validation command.
- `tests/RTUB.Application.Tests/xunit.runner.json` - **unchanged**; v2 schema, still valid. Will
  need rewriting in unit 011.
- `Directory.Build.props` - unchanged since 009 (`TreatWarningsAsErrors=true`, no suppressions).

## Latest validation (unit 010)
- Forced restore (`--force-evaluate`): OK, all 9 projects, **0 warnings**.
- Build Release: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true`. The `AngleSharp` and
  `AutoFixture` removals compiling cleanly is what proves those references were dead.
- Full suite, run with CI's exact flags - **4478 passed, 0 failed, 60 skipped. Exact baseline
  match**; no project count moved:

  | Project | Passed | Skipped | Baseline passed |
  | --- | --- | --- | --- |
  | `RTUB.Core.Tests` | 791 | 0 | 791 |
  | `RTUB.Application.Tests` | 1968 | 0 | 1968 |
  | `RTUB.Shared.Tests` | 759 | 2 | 759 |
  | `RTUB.Web.Tests` | 735 | 56 | 735 |
  | `RTUB.Integration.Tests` | 225 | 2 | 225 |
  | **Total** | **4478** | **60** | **4478** |

- Coverage / CI pipeline verified end-to-end on the new stack: 5 `coverage.cobertura.xml` files
  plus `test-results.trx` produced, matching both `ci.yml` upload globs.
- `dotnet list package --vulnerable --include-transitive`: **zero vulnerable packages in all 9
  projects.**
- `dotnet list package --deprecated --include-transitive`: **only** the `xunit 2.9.3` family.
  Nothing new introduced by Test SDK 18 / runner 4.0.0 / coverlet 10.
- `dotnet list package --outdated`: `RTUB.Core.Tests` is now **fully current**. Everything still
  listed under the other test projects is non-test-stack (AWS, the Microsoft 10.0.12 train, and
  the deliberately held `MockQueryable`).
- Resolved graph checked in `project.assets.json`: EF Core **10.0.11** in every test project (no
  skew), `AngleSharp 1.8.1` transitive from bUnit, `Newtonsoft.Json` **absent** from all test
  projects.
- `git diff --check` clean; secret scan of the diff clean. `git status` = exactly 3 modified
  files, no untracked files.
- Migrations / `ApplicationDbContextModelSnapshot.cs`: **unchanged**. `src/` has **zero** changes;
  no test `.cs` file changed.
- File encodings verified against `HEAD` (no BOM, CRLF working tree) so the diff carries no
  whitespace noise.
- Graphify not rebuilt - no application structure change. Frontend / Playwright not run - no
  frontend file touched.
- Timing note: with `--collect` the full suite takes ~25 min locally; without it the same projects
  finish in seconds.

## Previous validation (unit 009)
- Forced restore OK; Release build 0 warnings / 0 errors with `NU1903` / `NETSDK1206` no longer
  suppressed. Full suite **4478 passed, 0 failed, 60 skipped**.
- `--vulnerable --include-transitive`: zero vulnerable packages in all 9 projects - first time in
  the modernization. `WebPush-NetCore` and the `Microsoft.NETCore.*` 1.x set gone from the graph.
- Newtonsoft floor pin removed; no RTUB project declares it directly any more.
