# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-07_

## Phase
Modernization **Phase 1B — Claude Code workflow & essential tooling**.
**COMPLETE** — merged into `dev` and pushed to `origin/dev`.

## Branch
`dev` (contains Phase 1B commit e232614c). Work branch `chore/001/claude-workflow` not yet deleted.

## Last completed step
Phase 1B implementation + review correction:
- Root `CLAUDE.md` (routing rules) and this `STATE.md`.
- `docs/architecture/system-index.md` + `docs/architecture/adr/README.md`.
- `.claude/settings.json` permission cleanup (blanket `Bash(*)` removed; durable `npm ci`/`npm run build:*` promoted here).
- `.claude/settings.local.json` untracked from git (`git rm --cached`), added to `.gitignore` — stays on disk as a machine-local override, no longer committed.
- `security-guidance@claude-plugins-official` v2.0.7 installed at project scope.
- `webapp-testing` skill vendored to `.claude/skills/webapp-testing/` with `UPSTREAM.md` provenance.
- Stale AI guidance corrected in `.github/agents/` (test, backend, docs).
- Obsolete `.github/prompts/rtub-ask-opus.prompt.md` deleted.

## Current task
**Phase 1C.1** — Create `rtub-sqlite` skill (per Phase 1C plan).

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

## Latest validation (Phase 1B)
Commit e232614c verified before merge:
- Phase 1B files staged only; unrelated `docs/cloudflare-account-migration-runbook.md` excluded — PASS
- `.claude/settings.json` valid JSON — PASS
- `.claude/settings.local.json` untracked, ignored, on disk — PASS
- No accidental secrets in staged diff — PASS
- Upstream `SKILL.md` trailing whitespace preserved for provenance (noted in UPSTREAM.md) — PASS
- Merge to `dev` successful (fast-forward from master baseline) — PASS
- STATE.md updated to reflect Phase 1B complete — PASS
