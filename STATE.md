# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-07_

## Phase
Modernization **Phase 1B — Claude Code workflow & essential tooling**.
Implementation complete, **awaiting owner review**.

## Branch
`chore/001/claude-workflow` (branched from `master`).
`dev` created locally from the `master` baseline. Nothing committed, pushed, or merged yet — this phase is authorized for local branch creation only.

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
Owner review of Phase 1B. No further changes until reviewed.

## Blockers
None.

## Deferred / owner decisions
- **Phase 1C:** custom RTUB skills (`rtub-sqlite`, `rtub-push`, `rtub-pwa`, `rtub-testing`, `rtub-shipping`, `rtub-frontend`, `rtub-mytuno`) — deliberately not created yet.
- Committing / pushing / merging `chore/001/claude-workflow` into `dev` — needs explicit authorization.
- `playwright@claude-plugins-official` was already installed at project scope before Phase 1B. Not removed.
- `docs/cloudflare-account-migration-runbook.md` carries pre-existing uncommitted edits from earlier work. Preserved untouched.
- Pending feature work (logging usernames, inventory discard values, Direcao meetings, MBWAY transfers page, Nerba orders grid, leaderboard UI) — unchanged, not part of this phase.

## Relevant files
- `CLAUDE.md` — session routing rules.
- `docs/architecture/system-index.md` — repository routing map.
- `docs/architecture/adr/README.md` — ADR format; no ADRs recorded yet.
- `.claude/skills/webapp-testing/UPSTREAM.md` — vendored-skill provenance and security review.

## Latest validation
Phase 1B changes no application behavior, so the full test suite was not run.
- `.claude/settings.json` parses as valid JSON — PASS
- `.claude/settings.local.json` untracked, still present on disk, matched by `.gitignore` — PASS
- `security-guidance` v2.0.7 active for RTUB (project scope, 4 hooks, 0 model-context cost) — PASS
- `webapp-testing` available project-locally — PASS
- `dotnet build src/RTUB.Web/RTUB.csproj` — PASS (0 warnings, 0 errors)
- Browser smoke test via the skill's `with_server.py` against `https://localhost:58869/login`:
  HTTP 200, login form rendered, `window.Blazor` initialized, 0 console errors, 0 page errors — PASS
- `git diff --check` clean; secret scan clean; no stray artifacts — PASS
- Build/smoke test not rerun for the review-correction pass (untracking a settings file cannot affect app behavior).
- Graphify not rebuilt (no application-structure change) — correct.
