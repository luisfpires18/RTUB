# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-08_

## Phase
Modernization **Phase 2.1 (legacy ASP.NET dependency cleanup) — implementation complete, awaiting owner review.**
Phase 1 (tooling) complete and merged into `dev`.

## Branch
`chore/004/remove-legacy-http-abstractions`, branched from `dev`. Not committed, not pushed — no git
write authorized in this unit. Earlier work branches `chore/001`–`chore/003` still kept.

## Last completed step
Phase 2.1. Removed the obsolete `Microsoft.AspNetCore.Http.Abstractions 2.2.0` dependency and corrected
the warning-suppression rationale.

**Removed**
- `src/RTUB.Application/RTUB.Application.csproj` — direct `Microsoft.AspNetCore.Http.Abstractions`
  `PackageReference`.
- `Directory.Packages.props` — the now-unused `PackageVersion` (2.2.0).

**Added**
- `src/RTUB.Application/RTUB.Application.csproj` — explicit
  `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. RTUB.Application uses only
  `HttpContext` and `IHttpContextAccessor` (28 + 17 uses), both in the .NET 10 shared framework.
  The framework was already arriving transitively via `Microsoft.AspNetCore.Identity.UI 10.0.0`;
  declaring it directly removes that hidden coupling. No package added, no output change.

**Suppressions reassessed — both RETAINED, comment rewritten**
The old `Directory.Build.props` comment blamed Http.Abstractions 2.2.0. That was wrong. Verified by
restoring/building with `NoWarn` cleared, before and after removal — the warning set is identical.
- `NU1903` — retained. Real causes: `Microsoft.NETCore.App 1.0.5` and `Microsoft.NETCore.Jit 1.0.7`,
  pulled in by **`WebPush-NetCore 1.0.2`**; plus `SQLitePCLRaw.lib.e_sqlite3 2.1.11` from
  `Microsoft.EntityFrameworkCore.Sqlite 10.0.0`. Neither is in scope for 2.1.
- `NETSDK1206` — retained. Real cause: `Libuv 1.9.1` via `Microsoft.NETCore.App 1.0.5` ->
  `WebPush-NetCore 1.0.2`. Same fix path.
Comment now names the verified causes and warns against adding suppressions to force a green build.

## Current task
None active. Awaiting owner review of Phase 2.1.

## Next unit
**2.2 — dependency/security follow-up.** Not started, not selected, not authorized.

## Blockers
None for 2.1.

## Deferred / owner decisions
- **Phase 2.2 dependency findings (all pre-existing, none introduced or resolved by 2.1):**
  - `WebPush-NetCore 1.0.2` is the sole reason `NU1903` and `NETSDK1206` are suppressed. Replacing it
    clears both. Belongs to the Push modernization phase — deliberately untouched here.
  - `SQLitePCLRaw.lib.e_sqlite3 2.1.11` — high severity (GHSA-2m69-gcr7-jv3q) and flagged
    `CriticalBugs`; transitive through EF Core Sqlite 10.0.0. Needs an EF Core bump, not a pin.
  - Whole 10.0.0 Microsoft stack (ASP.NET Core, EF Core, Identity, Extensions) is one patch band
    behind at 10.0.11.
  - Test stack: `xunit 2.9.2` deprecated in favour of xunit.v3; `xunit.runner.visualstudio` 2.8.2 ->
    4.0.0; `Microsoft.NET.Test.Sdk` 17.12.0 -> 18.9.0; `MockQueryable.Moq` 8.0.0 -> 10.0.8;
    `coverlet.collector` 6.0.2 -> 10.0.1; `bunit` 2.7.2 -> 2.9.0; `AngleSharp` 1.5.2 -> 1.8.0.
  - Other: `QuestPDF` 2024.10.3 -> 2026.8.0 (check licence terms), AWS SDK patch drift,
    `FluentAssertions` 8.8.0 -> 8.10.0.
  - `Newtonsoft.Json` is still a direct reference of RTUB.Application even though Phase B migrated
    serialization to System.Text.Json. Worth checking whether it is still needed. Not investigated.
- **Phase 1C:** remaining optional custom skills (`rtub-pwa`, `rtub-testing`, `rtub-shipping`,
  `rtub-frontend`, `rtub-mytuno`) — deliberately not created.
- Work-branch cleanup (`chore/001`, `chore/002`, `chore/003`) — delete when convenient.
- `playwright@claude-plugins-official` was already installed at project scope before Phase 1B. Not removed.
- `docs/cloudflare-account-migration-runbook.md` carries pre-existing uncommitted edits from earlier work.
  Preserved untouched and unstaged.
- Pending feature work (logging usernames, inventory discard values, Direcao meetings, MBWAY transfers
  page, Nerba orders grid, leaderboard UI) — unchanged, not part of any phase yet.

## Relevant files
- `Directory.Build.props` — warning suppressions and their verified causes.
- `Directory.Packages.props` — central package versions.
- `src/RTUB.Application/RTUB.Application.csproj` — framework reference and package references.
- `CLAUDE.md` — session routing rules.
- `docs/architecture/system-index.md` — repository routing map.
- `.claude/skills/rtub-sqlite/SKILL.md`, `.claude/skills/rtub-push/SKILL.md`.

## Latest validation (Phase 2.1)
Package references changed, so the whole .NET solution was validated. No frontend/Pixi build, no
Playwright, no Graphify rebuild — application structure unchanged.
- `dotnet clean` + forced `dotnet restore` — PASS, no restore warnings
- `dotnet build RTUB.sln -c Release` — PASS, **0 warnings, 0 errors** (`TreatWarningsAsErrors` on)
- `dotnet test RTUB.sln -c Release` — PASS, **4477 passed, 0 failed, 60 skipped** across all 5 projects
  (Core 791, Application 1967, Shared 759, Web 735, Integration 225; skips pre-existing)
- Diagnostic restore/build with `NoWarn` cleared, before and after the change — identical warning set,
  proving Http.Abstractions 2.2.0 caused neither `NU1903` nor `NETSDK1206` — PASS
- `dotnet list package --vulnerable --include-transitive` — 3 findings, all pre-existing, none from
  the removed package — PASS (recorded above for 2.2)
- No `Microsoft.AspNetCore.Http.Abstractions/2.2.0` or `Http.Features/2.2.0` in any `project.assets.json`;
  `Http.*` reference assemblies in build output resolve to shared framework 10.0.4 — PASS
- `git diff --check` clean; accidental-secret scan of the diff clean — PASS
- Unrelated `docs/cloudflare-account-migration-runbook.md` edits preserved, unstaged, uncommitted — PASS
