# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-07_

## Phase
Modernization **Phase 1C.1 — `rtub-sqlite` skill**.
**Implementation complete — awaiting owner review.** Not committed.

## Branch
`chore/002/rtub-sqlite-skill` (branched from `dev`). Phase 1B lives in `dev`.

## Last completed step
Phase 1C.1: created `.claude/skills/rtub-sqlite/SKILL.md` (121 lines).
Durable RTUB-specific SQLite/EF Core knowledge, verified against current code:
- `IDbContextFactory` one-context-per-operation; `Repository<T>` saves internally, no `SaveChangesAsync` on `IRepository<T>`; `UpdateAsync` is fetch-then-`SetValues` (scalars only) and throws when the row is missing.
- WAL/busy-timeout PRAGMAs via `SqliteConnectionInterceptor` on every connection open; `DefaultTimeout=30`; `Cache=Shared` deliberately absent.
- Migrations assembly is `"RTUB"` (`src/RTUB.Web/RTUB.csproj`); EF CLI needs `--project src/RTUB.Web`; `PendingModelChangesWarning` suppressed; startup migrate skipped when `EnvironmentName == "Test"`.
- Two distinct test databases: EF InMemory (`DatabaseFixture`, enforces `[Required]`, `CleanDatabase` skips `Users` and is a hand-maintained list) vs real SQLite shared `:memory:` via `EnsureCreated()` (`TestWebApplicationFactory`) — migrations untested there.
- Backup uses `SqliteConnection.BackupDatabase()` because WAL makes file copies invalid; restore must delete stale `-wal`/`-shm`; schedulers are per-instance, hosted services need `IServiceScopeFactory`.
No application code or tests modified.

Follow-up: review found `docs/backend-practices.md:155` missing `--project src/RTUB.Web`
(fails from repo root). Fixed to match `.github/copilot-instructions.md:20` and the skill.
All three now agree.


## Current task
Owner review of Phase 1C.1. Next planned unit: **1C.2 `rtub-push`** — not started, not authorized yet.

## Blockers
None.

## Deferred / owner decisions
- **Phase 1C:** remaining custom RTUB skills (`rtub-push`, `rtub-pwa`, `rtub-testing`, `rtub-shipping`, `rtub-frontend`, `rtub-mytuno`) — deliberately not created yet.
- Work branch `chore/001/claude-workflow` cleanup — delete when convenient.
- `playwright@claude-plugins-official` was already installed at project scope before Phase 1B. Not removed.
- `docs/cloudflare-account-migration-runbook.md` carries pre-existing uncommitted edits from earlier work. Preserved untouched.
- Pending feature work (logging usernames, inventory discard values, Direcao meetings, MBWAY transfers page, Nerba orders grid, leaderboard UI) — unchanged, not part of this phase.

## Relevant files
- `CLAUDE.md` — session routing rules.
- `docs/architecture/system-index.md` — repository routing map.
- `docs/architecture/adr/README.md` — ADR format; no ADRs recorded yet.
- `.claude/skills/webapp-testing/UPSTREAM.md` — vendored-skill provenance and security review.

## Latest validation (Phase 1C.1)
Documentation/tooling only — RTUB test suite deliberately not run, Graphify not rebuilt.
- `SKILL.md` frontmatter parses; `name`/`description` present; name is a valid slug — PASS
- All 17 file/directory paths referenced in the skill exist — PASS
- Every claim re-verified against current source before writing — PASS
- No secrets or credential-shaped strings — PASS
- `git diff --check` clean — PASS
- Body 117 lines, within the 80–150 target; points to `docs/cloudflare-r2-and-database-backups.md` and `docs/backend-practices.md` instead of duplicating them — PASS
- Unrelated `docs/cloudflare-account-migration-runbook.md` edits preserved untouched — PASS