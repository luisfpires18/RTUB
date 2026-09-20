# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-20_

## Phase
Modernization unit **011 (xUnit v2 → v3 / Microsoft.Testing.Platform migration) — implementation
complete, uncommitted, awaiting owner review.** Unit 010 is merged to `dev`.

**The solution now has zero deprecated packages and zero vulnerable packages.** This was the last
deprecated cluster.

## Branch
`chore/011/migrate-xunit-v3`, branched from `dev`. Uncommitted (no commit authorized).
`chore/001`–`chore/010` still present; delete when convenient.

## Last completed step
**Unit 011 — all 5 test projects migrated to xUnit v3 4.0.1 on Microsoft.Testing.Platform v2.
Zero test-source edits, zero `src/` edits.**

### Package shape (decided against current official xUnit docs, then verified in the resolved graph)

| Package | Change |
| --- | --- |
| `xunit.v3` | **added, 4.0.1** |
| `Microsoft.Testing.Extensions.CodeCoverage` | **added, 18.11.2** |
| `xunit` 2.9.3 | **removed** |
| `xunit.runner.visualstudio` 4.0.0 | **removed** |
| `Microsoft.NET.Test.Sdk` 18.10.1 | **removed** |
| `coverlet.collector` 10.0.1 | **removed** |

**`xunit.v3`, not `xunit.v3.mtp-v2`.** The `.mtp-vN` suffix packages exist to pin the MTP major
when there is a choice. In 4.x there is none: MTP v1 support was removed and `xunit.v3 4.0.1`
takes an exact-pinned dependency on `xunit.v3.mtp-v2 (= 4.0.1)`. Verified in `project.assets.json`
— the plain reference already resolves `xunit.v3.mtp-v2/4.0.1` → `xunit.v3.core.mtp-v2/4.0.1` →
**`Microsoft.Testing.Platform/2.4.0`**. An explicit `.mtp-v2` reference would resolve identically,
so it is redundant, and `xunit.v3` is what the official docs call the default for 4.0.0+.

**`xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` removed, not kept.** The xUnit docs
suggest keeping them for backward compatibility, but they are *only* the VSTest-mode adapter. MTP
supplies VS Test Explorer natively (VS 2022 ≥ 17.14.16 has the MTP Test Explorer on by default),
and CI no longer uses VSTest. Keeping them would leave dead VSTest-only packages behind.

**`coverlet.collector` removed.** It is a VSTest data collector; the xUnit MTP docs state the
standard Coverlet experience is not supported. Replaced by
`Microsoft.Testing.Extensions.CodeCoverage`, which still emits Cobertura.

`Microsoft.Testing.Extensions.Telemetry/2.4.0` now arrives transitively via `xunit.v3` — see
Deferred.

### Test project changes (identical pattern in all 5)
- `<OutputType>Exe</OutputType>` — v3 test projects are standalone executables.
- `xunit` + `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` + `coverlet.collector`
  references → `xunit.v3` + `Microsoft.Testing.Extensions.CodeCoverage`.
- Nothing else. `<Using Include="Xunit" />` stays (v3 keeps the `Xunit` namespace),
  `IsTestProject` on `RTUB.Integration.Tests` stays, project references untouched.

`UseMicrosoftTestingPlatformRunner` and `TestingPlatformDotnetTestSupport` were **not** set:
the first only swaps the CLI when running a test `.exe` directly, and the second is the SDK 8/9
path. Neither is needed on SDK 10 with the `global.json` runner setting.

### `global.json` — new file
```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```
Current official syntax for .NET SDK 10+. **No `sdk` section** — the SDK is deliberately not
pinned or downgraded. Local `dotnet --version` = 10.0.200; CI's `dotnet-version: '10.0.x'` is
unchanged and still wins. Caveat: this key needs SDK ≥ 10.0.100, so an SDK 9-only machine can no
longer run the suite.

### `tests/Directory.Build.props` — new file
The one real surprise of the migration. The v3 analyzer **`xUnit1051`** (pass
`TestContext.Current.CancellationToken` to anything accepting a `CancellationToken`) fires
**1634 times** across the existing suite, and repo-wide `TreatWarningsAsErrors=true` turns every
one into a build error. It was the *only* new diagnostic — no `CS` errors, no other `xUnit` rule,
which confirms the source is otherwise fully v3-compatible.

Suppressed centrally in one file scoped to `tests/` (`<NoWarn>$(NoWarn);xUnit1051</NoWarn>`)
rather than duplicated across 5 csproj files or "fixed" by editing 1634 call sites — that would be
a test-source refactor, not a runner migration. The file chains to the root
`Directory.Build.props` via `GetPathOfFileAbove` so test projects keep `TreatWarningsAsErrors`,
`Nullable` and `LangVersion`. Adopting the rule is deferred.

### `xunit.runner.json` — **unchanged, and proven still live**
All 7 keys in `tests/RTUB.Application.Tests/xunit.runner.json` are supported by v3, and the
`$schema` URL `.../schema/current/...` already resolves to the current 4.0 schema. In 4.0
`parallelizeTestCollections: true` is auto-mapped to the new `parallelMode: "collections"`, which
is also the default — no behavior change. No migration to `testconfig.json`: that file is
MTP-only, and `xunit.runner.json` is the form that works in every runner.

Not assumed — measured. Flipping `methodDisplay` to `method` in the build output changed the TRX
`testName` values from class-qualified to method-only (`GetCharacterAsync WithEmptyUserId
ShouldThrowException(...)`), proving the runner reads the file under MTP. Probe reverted.

### CI (`.github/workflows/ci.yml`) — test step and coverage glob only
```
dotnet test --no-build --configuration Release \
  --results-directory ./coverage \
  --report-xunit-trx \
  --coverage --coverage-output-format cobertura
```
- `--collect:"XPlat Code Coverage"` → `--coverage --coverage-output-format cobertura`.
- `--logger "trx;LogFileName=test-results.trx"` → `--report-xunit-trx` (xUnit's own built-in TRX
  reporter, not the Microsoft extension).
- **`--report-xunit-trx-filename` deliberately omitted.** Under MTP all 5 projects write into the
  single `--results-directory`, so a fixed filename would have them overwrite each other. Left
  unset, MTP auto-assigns unique names.
- **Coverage artifact glob changed**, `coverage/**/coverage.cobertura.xml` →
  `coverage/**/*.cobertura.xml`. MTP names coverage files by GUID, not `coverage.cobertura.xml`.
  The format is still Cobertura; only the filename pattern moved. The `.trx` glob
  (`coverage/**/*.trx`) needed no change.
- Release build, all 5 projects, failure behavior and both upload steps otherwise untouched. No
  unrelated CI modernization; OIDC/Azure remains future scope.

**Files changed (7 modified, 2 new):** `.github/workflows/ci.yml`, `Directory.Packages.props`, all
5 `tests/*/*.csproj`; new `global.json` and `tests/Directory.Build.props`.
No `src/`, no test `.cs`, no migrations.

## Current task
None active.

## Next unit
**012 — Microsoft 10.0.11 → 10.0.12 servicing train** (ASP.NET Core, EF Core, Identity,
Mvc.Testing) across `src/` + tests, which also unblocks `MockQueryable.Moq` 10.0.12. Alternatively
adopt `xUnit1051` as its own unit — see Deferred.

## Blockers
None.

## Deferred / owner decisions
- **`xUnit1051` suppressed, not adopted** (1634 sites). Its own unit if wanted: mechanical, but it
  touches nearly every test file, so it must not ride along with anything else.
- **`Microsoft.Testing.Extensions.Telemetry/2.4.0`** is pulled in transitively by `xunit.v3` and
  reports usage metrics to Microsoft. New outbound data flow introduced by this migration, left as
  shipped rather than silently changed. Opt out with `TESTINGPLATFORM_TELEMETRY_OPTOUT=1` (env
  var) if the owner wants it off in CI and/or locally.
- **`global.json` raises the SDK floor to 10.0.100** for running tests. No pin added.
- Running a test `.exe` directly gives xUnit's native console runner, not the MTP CLI, because
  `UseMicrosoftTestingPlatformRunner` was not set. `dotnet test` is unaffected. Set it only if
  direct-executable runs need the MTP CLI.
- **Remaining dependency findings (after 011):**
  - Microsoft 10.0.11 → **10.0.12** train — one servicing bump across src + tests. Not started.
  - `MockQueryable.Moq 10.0.12` — held at 10.0.8; gated on the EF Core 10.0.12 bump above.
  - `AWSSDK.Core 4.0.3.8` → 4.0.102.6 and `AWSSDK.S3 4.0.10` → 4.0.103.3 — independent train.
  - `QuestPDF 2024.10.3` → 2026.9.0 — major train, likely breaking. Not started.
  - `AWSSDK.Core` is referenced directly by `RTUB.Integration.Tests`, `RTUB.Shared.Tests` and
    `RTUB.Web.Tests` but **used by none of them** (only `RTUB.Application.Tests` uses `Amazon.*`).
    Three dead direct references — belongs to the AWS train.
  - `Portable.BouncyCastle 1.9.0` — transitive via `WebPush 1.0.13`. Legacy package id, not
    deprecated, no advisories. Nothing to do; note it if an advisory lands.
- **Test-suite hygiene, noted not acted on:** `RTUB.Web.Tests` skips **56 of 791** tests (mostly
  `*PageTests` modal / button cases). Pre-existing, unchanged by 011.
- **Push subsystem debt (unchanged):** `WebPushClient` is still newed up inside
  `PushNotificationService`, so it cannot be mocked and no test covers an actual send, a retry, or
  404/410 cleanup. Also: two service-worker registration paths, unbounded `BroadcastAsync`
  fan-out, no `CancellationToken`, no delivery metrics.
- **Phase 1C:** remaining optional custom skills — deliberately not created.
- Work-branch cleanup (`chore/001`–`chore/010`) — delete when convenient.
- Two `graphifyy 0.9.56 + MCP` installs (isolated venv, Microsoft-Store Python user site). The
  Store one wins PATH and works; both are compatible, so neither needs removing.
- Pending feature work — unchanged, not part of any phase.

## Relevant files
- `global.json` — **new**; MTP runner selection only, no SDK pin.
- `tests/Directory.Build.props` — **new**; chains to root props, suppresses `xUnit1051`.
- `Directory.Packages.props` — `xunit.v3` + `Microsoft.Testing.Extensions.CodeCoverage` in;
  `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `coverlet.collector` out.
- `tests/*/*.csproj` (all 5) — `OutputType=Exe`, v3 + MTP coverage package references.
- `.github/workflows/ci.yml` — test step rewritten for MTP; coverage artifact glob widened.
- `tests/RTUB.Application.Tests/xunit.runner.json` — **unchanged**; v3-valid, verified live.
- `Directory.Build.props` (root) — unchanged since 009.

## Latest validation (unit 011)
- Forced restore (`--force-evaluate`): OK, all 9 projects.
- Build Release, whole solution: **0 warnings, 0 errors** under `TreatWarningsAsErrors=true`.
- Proof-first migration: `RTUB.Core.Tests` migrated alone, restored, built and run under MTP
  (791/791, TRX + Cobertura produced) **before** the pattern was applied to the other 4.
- Full suite run with **CI's exact new command** — **4478 passed, 0 failed, 60 skipped. Exact
  baseline match**, and every per-project count matches, read back out of the TRX files:

  | Project | Passed | Skipped | Baseline passed |
  | --- | --- | --- | --- |
  | `RTUB.Core.Tests` | 791 | 0 | 791 |
  | `RTUB.Application.Tests` | 1968 | 0 | 1968 |
  | `RTUB.Shared.Tests` | 759 | 2 | 759 |
  | `RTUB.Web.Tests` | 735 | 56 | 735 |
  | `RTUB.Integration.Tests` | 225 | 2 | 225 |
  | **Total** | **4478** | **60** | **4478** |

- Artifacts: **5 `*.cobertura.xml`** (parsed — valid `<coverage>` root, real `line-rate`, non-empty
  `<package>` sets) and **5 `*.trx`**. Both `ci.yml` upload globs match.
- **Failure behavior proven, not assumed:** a temporary deliberately-failing test made
  `dotnet test` exit **2**; the passing run exits **0**. Probe deleted, project rebuilt clean.
- `dotnet list package --vulnerable --include-transitive`: **zero in all 9 projects.**
- `dotnet list package --deprecated --include-transitive`: **zero in all 9 projects** — the
  `xunit 2.x` cluster is gone and MTP introduced nothing deprecated.
- `dotnet list package --outdated`: **nothing test-stack remains.** `xunit.v3 4.0.1` and
  `Microsoft.Testing.Extensions.CodeCoverage 18.11.2` are current. Everything still listed is a
  deferred train (AWS, Microsoft 10.0.12, QuestPDF, the held `MockQueryable`).
- Resolved graph swept for VSTest remnants across all 5 test projects: **no `xunit` 2.x, no
  `Microsoft.NET.Test.Sdk`, no `Microsoft.TestPlatform.*`, no `coverlet`, no
  `xunit.runner.visualstudio`.**
- `git diff --check` clean; secret scan of the diff + both new files clean (only hit was the word
  "CancellationToken" inside a new comment).
- `git status` = exactly 7 modified + 2 new. **Zero `src/` changes, zero test `.cs` changes,
  migrations and `ApplicationDbContextModelSnapshot.cs` unchanged.**
- Graphify not rebuilt — no application structure change. Frontend / Playwright not run.
- **Timing note:** the full suite with coverage now runs in **~25 s**, against ~25 min under
  `coverlet.collector` + VSTest. Same 4478 tests.

## Previous validation (unit 010)
Test dependency stack modernized (7 packages raised, 4 references / 3 package ids removed);
package-and-config only, zero source edits. Full suite **4478 passed / 0 failed / 60 skipped**,
zero vulnerable packages, `xunit 2.9.3` left as the only deprecated cluster — which 011 removed.
`MockQueryable.Moq` held at 10.0.8 so EF Core stays at 10.0.11 in test projects (still true).

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
