# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-08_

## Phase
Modernization **Phase 2.2.1 (Microsoft/.NET 10 servicing update) — COMPLETE, merged to dev.**

## Branch
`dev`. Work branch `chore/005/dotnet-servicing-update` merged via --no-ff at 98537769; can be
deleted when convenient.

## Last completed step
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

## Next unit
**Remove obsolete `docs/cloudflare-account-migration-runbook.md`.** Owner confirmed obsolete;
deletion deliberately excluded from Phase 2.2.1 — goes in its own atomic branch next. Not started.

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
  - `Newtonsoft.Json` still a direct ref despite System.Text.Json migration — not investigated.
- **Phase 1C:** remaining optional custom skills — deliberately not created.
- Work-branch cleanup (`chore/001`–`chore/004`) — delete when convenient.
- `docs/cloudflare-account-migration-runbook.md` — owner confirmed obsolete, may be deleted.
  Pre-existing unstaged edits preserved, untouched by Phase 2.2.1. Deletion is next unit, its
  own atomic branch — not folded into dependency work.
- Pending feature work — unchanged, not part of any phase.

## Relevant files
- `Directory.Packages.props` — 11 Microsoft packages → 10.0.11.
- `Directory.Build.props` — suppression comment updated (SQLitePCLRaw cause removed).

## Latest validation (Phase 2.2.1)
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
