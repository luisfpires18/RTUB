# STATE.md

Living execution state. **Read this first.** Overwrite stale entries — this is a status board, not a diary.

_Last updated: 2026-09-08_

## Phase
Modernization **Phase 1C.2 — `rtub-push` skill**.
**Implementation complete — awaiting owner review.** Not merged, not pushed.

Phase 1 tooling work is nearly complete: after this review, only the remaining optional custom skills
are left before Phase 1 can close.

## Branch
`chore/003/rtub-push-skill`, branched from `dev`. Uncommitted (commit not authorized).

## Last completed step
Phase 1C.2: created `.claude/skills/rtub-push/SKILL.md` — durable RTUB-specific Web Push
knowledge verified against current code: `WebPushOptions`/VAPID contract and the two distinct gates
(`WebPush:Enabled` for non-Owners vs `IsConfigured()`), silent-failure mode when VAPID keys are malformed,
dual server+client opt-out tracking (`ApplicationUser.PushNotificationsOptedOut` +
`localStorage['rtub-push-opted-out']`) and why self-healing must respect it, inbox-message fallback
semantics, 404/410 subscription cleanup, retry/backoff rules, per-method fan-out differences, base64url
key normalization, iOS/Android/TWA workarounds in the service worker, auth coupling, and the real limits
of the existing service tests. Includes a clearly-labelled "known debt — not the target design"
section pointing at Phase 6 items.

Review pass: removed brittle metadata from the skill (line-number anchors, call-site and background-service
counts, file-length and test-count figures) in favour of file/class/method/component references. Exact values
kept only where they govern runtime behavior (TTL 86400, `Urgency: high`, 3 retries, 2^n / 3^n backoff,
404/410 handling). Fixed the two directly-related stale docs: `docs/architecture/system-index.md` Push
section now routes to options/service/factory/repository/entity/controller/browser-manager/SW-handlers/opt-in
UI and no longer lists `EmailNotificationService.cs` as push (noted as a separate channel);
`docs/pwa-practices.md` checklist no longer says push is "if implemented".

No application code, tests, or configuration were modified. Graphify not rebuilt (structure unchanged).

## Current task
None active. Awaiting owner review of the `rtub-push` skill.

## Blockers
None.

## Deferred / owner decisions
- **Phase 2 implementation not selected yet** — deliberately deferred until Phase 1 tooling closes.
- **Phase 1C:** remaining custom RTUB skills (`rtub-pwa`, `rtub-testing`, `rtub-shipping`,
  `rtub-frontend`, `rtub-mytuno`) — deliberately not created yet.
- Work branches `chore/001/claude-workflow` and `chore/002/rtub-sqlite-skill` cleanup — delete when convenient.
- `playwright@claude-plugins-official` was already installed at project scope before Phase 1B. Not removed.
- `docs/cloudflare-account-migration-runbook.md` carries pre-existing uncommitted edits from earlier work.
  Preserved untouched.
- Pending feature work (logging usernames, inventory discard values, Direcao meetings, MBWAY transfers
  page, Nerba orders grid, leaderboard UI) — unchanged, not part of this phase.

## Relevant files
- `CLAUDE.md` — session routing rules.
- `docs/architecture/system-index.md` — repository routing map.
- `docs/architecture/adr/README.md` — ADR format; no ADRs recorded yet.
- `.claude/skills/rtub-sqlite/SKILL.md` — Phase 1C.1 output (merged).
- `.claude/skills/rtub-push/SKILL.md` — Phase 1C.2 output (under review).
- `.claude/skills/webapp-testing/UPSTREAM.md` — vendored-skill provenance and security review.

## Latest validation (Phase 1C.2)
Documentation/tooling only — no build, no test suite, no Graphify rebuild (none required; no code changed).
- YAML frontmatter parses; `name` + `description` only — PASS
- Skill discoverable — appears in the session skill list as `rtub-push` — PASS
- Trigger description narrow: names Web Push / VAPID / subscriptions / opt-in UI / delivery / SW push
  handlers / iOS-Android, and explicitly excludes SW caching, offline, manifest and generic frontend — PASS
- No brittle anchors left: no `file:line` references, no call-site / background-service / test / file-length
  counts. Behavioral constants (TTL, retries, backoff, 404/410) deliberately retained — PASS
- All referenced paths, classes, methods and component names resolve in the current tree — PASS
- Skill, `docs/architecture/system-index.md`, `docs/pwa-practices.md` and the implementation agree — PASS
- CURRENT behavior and Phase 6 debt/target architecture remain in separate labelled sections — PASS
- No generic Web Push/PWA tutorial content; no framework-doc duplication — PASS
- No secrets or secret-shaped strings; VAPID values referenced by name only — PASS
- `git diff --check` clean (exit 0) — PASS
- Unrelated `docs/cloudflare-account-migration-runbook.md` edits preserved untouched — PASS
