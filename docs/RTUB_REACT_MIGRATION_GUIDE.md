# RTUB React track - roadmap and task contract

Where the React track stands, what comes next, and the shape every next task takes. Module rules (replacement,
testing, schema, wording) live in `docs/react-portal-pilot.md` → *Rules for every React module*; per-module detail in
`docs/react-*.md`. "Migration" in this file means moving a page from Blazor to React, never a database migration.

_Last updated: 2026-10-05 (025 Novidades)_

## Done: 001-024 (merged to `dev`) and 025 (in review)

| Tasks | Module | React routes | Doc |
| --- | --- | --- | --- |
| 001-005, 010 | Public shell: home, privacy, profile entry, request | `/`, `/privacy`, `/profile`, `/request` | `react-portal-pilot.md` |
| 006 | Música | `/music`, `/music/albums/{id}` | `react-music.md` |
| 007 | Login | `/login` | `react-portal-pilot.md` |
| 008, 016 | Órgãos Sociais (+ RGI, management) | `/roles` | `react-governance.md` |
| 009, 015 | Galeria (+ upload, edit, delete) | `/gallery` | `react-gallery.md` |
| 011-013 | Atuações (agenda, event, enrollments, discussion, contacts) | `/events…` | `react-events.md` |
| 014 | Ensaios | `/rehearsals`, `/rehearsals/{id}` | `react-rehearsals.md` |
| 017, 018 | Membros (directory, hierarchy, member admin) | `/members`, `/members/hierarchy` | `react-members.md` |
| 019 | Classificação | `/leaderboard` | `react-leaderboard.md` |
| 020 | Instrumentos | `/inventory` | `react-inventory.md` |
| 021 | Loja | `/shop` | `react-shop.md` |
| 022 | Documentação | `/documentation` | `react-documentation.md` |
| 023 | Logística | `/logistics`, `/logistics/{id}` | `react-logistics.md` |
| 024 | Tesouraria | `/treasury`, `/treasury/reports/{id}`, `/treasury/calotes`, `/treasury/mbway`, `/treasury/nerba[/{id}]` | `react-treasury.md` |
| 025 | Novidades (new feature: public posts wall; branch open) | `/news` | `react-news.md` |

Route ownership is code: `src/RTUB.Web/Program.cs` (React shell section). Retired Blazor URLs (`/member/events`,
`/member/gallery`, `/member/roles`, `/hierarchy`, `/members/manage`, `/finance…`, `/calotes`, `/mbway…`, `/nerba…`)
302 to their React page.

## 025 Novidades (in review)

The task was first named "Newsletter"; it is the Novidades public news feed. Built as `/news` with an approved schema
(`NewsPosts`, migration `AddNewsPosts`); see `docs/react-news.md`. Its follow-ups (images, comments, reactions,
per-post pages with share previews) are separate tasks. Next React task: pick from *Still Blazor* or *Cleanup* below.

## Still Blazor (not yet scheduled)

`/member/profile` (profile editor), `/member/map`, `/hall-of-fame`, `/meetings`, `/naipes`, `/naipes/config`,
`/notifications`, `/requests`, `/questions`, `/labels`, `/users`, `/emails`, `/images`, `/share`, `/owner/*`, and the
Identity pages (`/forgot-password`, `/reset-password`, `/confirm-email`). Pick these up one module per task.

## Future work, outside the migration

- **Games / Bets / MyTuno**: removed from the app by task 026 (no replacement; the old URLs 404). Their tables and
  `AspNetUsers.FidelisBalance` stay until a later contract task drops them: `docs/games-bets-mytuno-removal.md`.
- **Messaging** (`/messages`, `MessagesHub`): to be replaced by a new WhatsApp-like system. That is new product design
  (conversations, groups, media, push), not a port of the Blazor inbox; it needs its own design task before code.

## Cleanup and polish (after or between features)

Recorded in `STATE.md` → *Deferred*; the main ones:
- Dead code with no caller since 013-024 (Shared cards, unused services, CSS, JS): one cleanup task.
- Treasury report delete blocked by a Nerba order: 500 → 409.
- Leitões seeing members' email/phone in member details: decide.
- Real R2 checks on DEV for every storage-backed module (local runs use placeholder storage).
- Receipts and gallery files are public-read in R2 (private objects + pre-signed URLs is a storage change).
- React top bar has no sign-out; old Blazor prompts (push, Play Store) have no React home.

## The "next prompt" contract

Every React-track task prompt states, and every task report answers:

1. **Start:** `git checkout dev && git pull origin dev`; confirm the previous task is merged (`git merge-base
   --is-ancestor <sha> dev`); stop if not.
2. **Branch:** `feat/<NNN>-<slug>` from `dev` (React sequence; next is `026`). Commit message `<NNN>: <what>`.
3. **Scope:** the module and routes it owns; what is explicitly out of scope.
4. **Never without an explicit request:** push, merge, PR, touch `master`, deploy PROD, change PROD DB / storage /
   Azure, send real email or push. No migration unless the task says so (stop and report first).
5. **Rules:** the module rules in `react-portal-pilot.md` (replacement, testing, schema, wording); rights enforced
   server-side; writes need antiforgery; DTOs carry no private fields; Owner inherits Admin unless stated.
6. **Validation:** `dotnet build`, the affected test projects (full `dotnet test` before merge), `npm run
   check:portal` + `npm run build:portal` (commit the bundle), browser check on a scratch DB copy at 375px with
   visitor / member / Leitão-Caloiro / Mod / Admin-Owner, `git diff --check`, secret scan.
7. **Docs:** a `docs/react-<module>.md` (audit, rules, API, follow-ups), a `STATE.md` line, the `CLAUDE.md` doc table,
   and this file's table.
8. **Report:** branch + base commit, files changed, rules before/after, tests and browser results, follow-ups, git
   status, commit hash, recommendation.
