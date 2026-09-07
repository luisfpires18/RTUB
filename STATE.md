# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-08_

## Phase
Modernization **Phase 1 (tooling) — COMPLETE.**
Phase 1C.2 (`rtub-push` skill) reviewed, accepted, merged into `dev` and pushed to `origin/dev`.

## Branch
`dev`. Work branches `chore/002/rtub-sqlite-skill` and `chore/003/rtub-push-skill` kept, not deleted.

## Last completed step
Phase 1C.2: `.claude/skills/rtub-push/SKILL.md` — durable RTUB Web Push knowledge (VAPID contract and its
two gates, malformed-key silent-failure mode, dual server+client opt-out, inbox-message fallback, 404/410
cleanup, retry/backoff and fan-out rules, base64url normalization, iOS/Android/TWA workarounds), with
current behavior and Phase 6 debt in separate sections. Also corrected the Push section of
`docs/architecture/system-index.md` (removed `EmailNotificationService`, added the real subsystem surface)
and the stale push line in `docs/pwa-practices.md`.

## Current task
None active.

## Next unit
**2.1 — legacy ASP.NET dependency cleanup.** Not started, not authorized yet.

## Blockers
None.

## Deferred / owner decisions
- **Phase 1C:** remaining optional custom skills (`rtub-pwa`, `rtub-testing`, `rtub-shipping`,
  `rtub-frontend`, `rtub-mytuno`) — deliberately not created.
- Work-branch cleanup (`chore/001`, `chore/002`, `chore/003`) — delete when convenient.
- `playwright@claude-plugins-official` was already installed at project scope before Phase 1B. Not removed.
- `docs/cloudflare-account-migration-runbook.md` carries pre-existing uncommitted edits from earlier work.
  Preserved untouched and unstaged.
- Pending feature work (logging usernames, inventory discard values, Direcao meetings, MBWAY transfers
  page, Nerba orders grid, leaderboard UI) — unchanged, not part of any phase yet.

## Relevant files
- `CLAUDE.md` — session routing rules.
- `docs/architecture/system-index.md` — repository routing map.
- `docs/architecture/adr/README.md` — ADR format; no ADRs recorded yet.
- `.claude/skills/rtub-sqlite/SKILL.md` — SQLite / EF Core knowledge.
- `.claude/skills/rtub-push/SKILL.md` — Web Push knowledge.
- `.claude/skills/webapp-testing/UPSTREAM.md` — vendored-skill provenance and security review.

## Latest validation (Phase 1C.2)
Documentation/tooling only — no build, no test suite, no Graphify rebuild (no code changed).
- Skill frontmatter valid; skill discoverable as `rtub-push` — PASS
- No `file:line` anchors or volatile inventory counts; behavioral constants (TTL, retries, backoff,
  404/410) deliberately retained — PASS
- All referenced paths and symbols resolve in the current tree — PASS
- Skill, `system-index.md`, `pwa-practices.md` and the implementation agree — PASS
- `git diff --check` clean, accidental-secret scan clean — PASS
- Reviewed and accepted by owner; merged to `dev` via `--no-ff`, no conflicts — PASS
- Unrelated `docs/cloudflare-account-migration-runbook.md` edits preserved, unstaged, uncommitted — PASS
