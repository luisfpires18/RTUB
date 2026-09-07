# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-07_

## Phase
Modernization **Phase 1C.1 — `rtub-sqlite` skill**.
**COMPLETE** — reviewed, accepted, merged into `dev` and pushed to `origin/dev`.

## Branch
`dev` (contains merge commit 223cd792). Work branch `chore/002/rtub-sqlite-skill` kept, not deleted yet.

## Last completed step
Phase 1C.1: created `.claude/skills/rtub-sqlite/SKILL.md` (121 lines) — durable RTUB-specific
SQLite/EF Core knowledge (`IDbContextFactory` per-operation contract, WAL/busy-timeout PRAGMAs,
migrations assembly `"RTUB"` quirk, the two test-database setups, backup/restore via R2).
Also corrected `docs/backend-practices.md:155` (missing `--project src/RTUB.Web`) to match
`.github/copilot-instructions.md` and the skill. Merged to `dev`, no conflicts.

## Current task
None active. Next planned unit: **1C.2 `rtub-push` skill** — not started, not authorized yet.

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
Documentation/tooling only — RTUB test suite not run, Graphify not rebuilt (not needed).
- `SKILL.md` frontmatter valid, all referenced paths exist, no secrets — PASS
- `docs/backend-practices.md` migration command now agrees with `.github/copilot-instructions.md`
  and the skill — PASS
- `git diff --check` clean, accidental-secret scan clean — PASS
- Reviewed and accepted by owner; merged to `dev`, pushed to `origin/dev` — PASS
- Unrelated `docs/cloudflare-account-migration-runbook.md` edits preserved untouched throughout — PASS