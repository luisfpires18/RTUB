# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-08_

## Phase
Modernization **Phase 2.2.2 (Newtonsoft.Json dependency investigation) — COMPLETE, unmerged.**
Phase 2.2.1 (Microsoft/.NET 10 servicing update) and the Cloudflare runbook removal are merged to
dev.

## Branch
`chore/007/remove-newtonsoft-json`, branched from `dev`. Uncommitted (no commit authorized).
`chore/001`–`chore/006` still present; delete when convenient.

## Last completed step
**Phase 2.2.2 — Newtonsoft.Json: `RTUB.Shared` direct reference REMOVED, `RTUB.Application`
reference RETAINED as a temporary security floor.** Central `PackageVersion 13.0.4` kept.

- **Source usage: none.** No `Newtonsoft`, `JsonConvert`, `JObject`, `JToken`, converter, settings
  or attribute reference in any `.cs` / `.razor` / `.cshtml` in `src/` or `tests/`. The only
  `JsonIgnore` / `JsonIgnoreCondition` hits (`DTOs/CombatResult.cs`, `Arena/BossMode/Stage.razor`)
  are System.Text.Json. `PushNotificationService` serializes with `System.Text.Json`. No
  Newtonsoft-specific serialization semantics anywhere.
- **Why Application keeps it:** `WebPush-NetCore 1.0.2` declares a permissive
  `Newtonsoft.Json >= 9.0.1` and ships the assembly as a **runtime** asset. Removing *both* direct
  references (first experiment) dropped `RTUB.Application` and `RTUB.Shared` to **9.0.1**,
  introducing **`GHSA-5crp-9r3c-p9vr` (High)**. `Microsoft.EntityFrameworkCore.Tools` is
  `PrivateAssets=all`, so its 13.0.4 does not backstop published output, and central transitive
  pinning is off — the `PackageVersion` alone cannot hold the floor. So the Application reference
  is a version floor, not a code dependency.
- **Why Shared no longer needs it (proved separately):** removing only
  `src/RTUB.Shared/RTUB.Shared.csproj`'s reference and re-restoring resolves `RTUB.Shared` to
  **13.0.4 transitively** through its `RTUB.Application` ProjectReference. Redundant; removed.
  No new advisory, no version change anywhere else.
- **Comments:** one explanation lives in `RTUB.Application.csproj`; `Directory.Packages.props`
  carries a one-line pointer. No comment left in Shared. No transitive pinning enabled, no
  package override added.

Phase 2.2.1: bumped all 11 Microsoft .NET 10 servicing packages `10.0.0` → **`10.0.11`**
(latest stable verified against NuGet at execution time; no .NET 11 previews). Families kept
aligned on one servicing release: ASP.NET Core, EF Core, Microsoft.Extensions, EF health checks,
SignalR client, MVC testing. Test/third-party packages deliberately untouched.

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
**Phase 2.3 — `WebPush-NetCore 1.0.2` replacement (Push modernization).** It is now the single
blocking dependency: sole cause of `NU1903` + `NETSDK1206`, and the sole reason the unused
`Newtonsoft.Json` floor pin must stay. Replacing it (e.g. `Lib.Net.Http.WebPush`, or VAPID +
`HttpClient` directly) retires the WebPush advisories **and** lets Phase 2.2.2's removal land for
free. Scope it against `PushNotificationService.cs` (`WebPushClient`, `VapidDetails`,
`PushSubscription`, `WebPushException` 404/410/429 handling) — see the `rtub-push` skill.
Test-stack modernization (xunit v3 family) remains a separate, later unit.

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
- Work-branch cleanup (`chore/001`–`chore/004`) — delete when convenient.
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
