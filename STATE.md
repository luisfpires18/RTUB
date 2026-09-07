# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-08_

## Phase
Modernization **Phase 2.1 (legacy ASP.NET dependency cleanup) — COMPLETE, merged to dev.**

## Branch
`dev`. Work branch `chore/004/remove-legacy-http-abstractions` merged via --no-ff; can be deleted when convenient.

## Last completed step
Phase 2.1: Removed `Microsoft.AspNetCore.Http.Abstractions 2.2.0` (unused under .NET 10), corrected
warning suppressions (both retained but recommented with verified causes: WebPush-NetCore dragging
legacy runtime packages, EF Core SQLitePCLRaw). Explicit `<FrameworkReference>` added to RTUB.Application.
Full validation: 0 warnings, 4477 tests pass. Merged to dev at 9b75cbec.

## Current task
None active.

## Next unit
**2.2.1 — Microsoft/.NET 10 servicing update.** Not started, not authorized yet.

## Blockers
None.

## Deferred / owner decisions
- **Phase 2.2 pre-existing dependency findings:**
  - `WebPush-NetCore 1.0.2` → Push modernization phase (clears NU1903 + NETSDK1206).
  - `SQLitePCLRaw.lib.e_sqlite3 2.1.11` (high vuln) → EF Core bump.
  - Microsoft stack 10.0.0 → 10.0.11; test stack updates (xunit, NET.Test.Sdk, runner, MockQueryable, coverlet, bunit, AngleSharp); QuestPDF, AWS SDK, FluentAssertions.
  - `Newtonsoft.Json` still direct ref despite System.Text.Json migration — not investigated.
- **Phase 1C:** remaining optional custom skills — deliberately not created.
- Work-branch cleanup (`chore/001`–`chore/003`) — delete when convenient.
- `docs/cloudflare-account-migration-runbook.md` pre-existing edits — preserved, unstaged.
- Pending feature work — unchanged, not part of any phase.

## Relevant files
- `Directory.Build.props` — corrected suppression comments.
- `Directory.Packages.props` — Http.Abstractions version removed.
- `src/RTUB.Application/RTUB.Application.csproj` — framework reference added.
- `.claude/skills/rtub-sqlite/SKILL.md`, `.claude/skills/rtub-push/SKILL.md`.

## Latest validation (Phase 2.1)
- Build Release: 0 warnings, 0 errors.
- Full test suite: 4477 passed, 0 failed, 60 skipped.
- Dep graph: no Http.Abstractions 2.2.0; reference assemblies from framework 10.0.4.
- Suppressions verified unchanged by Http.Abstractions removal.
