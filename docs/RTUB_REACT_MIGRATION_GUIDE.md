# RTUB React track - roadmap and task contract

Where the React track stands, what comes next, and the shape every next task takes. Module rules (replacement,
testing, schema, wording) live in `docs/react-portal-pilot.md` → *Rules for every React module*; per-module detail in
`docs/react-*.md`. "Migration" in this file means moving a page from Blazor to React, never a database migration.

_Last updated: 2026-10-12 (038 Push v2)_

## Done: 001-025 and 030-037 (merged to `dev`); 038 Push v2 (open, not merged)

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
| 025 | Novidades (new feature: public posts wall) | `/news` | `react-news.md` |
| 030 | Member shell: visitor header with Login; members' rail / drawer menu (not a new route) | every React page | `react-portal-pilot.md` → *Member shell* |
| 031 | Pedidos (admin side of `/request`) and Perguntas aos Órgãos Sociais | `/requests`, `/questions` | `react-requests-questions.md` |
| 032 | Profile editor (into `/profile`), Mapa de membros, Hall of Fame | `/profile`, `/members/map`, `/hall-of-fame` | `react-member-area.md` |
| 033 | Naipes (instrument videos / images, comments) and their settings | `/naipes`, `/naipes/config` | `react-naipes.md` |
| 034 | Reuniões (meetings, participation, atas, meeting requests) | `/meetings` | `react-meetings.md` |
| 035 | Owner tools (Owner only; Admin lost Emails / Notificações): announcement emails, custom push, users and roles, audit log, database | `/emails`, `/notifications`, `/users`, `/owner/tracing`, `/owner/db` | `react-owner-operations.md` |
| 036 | Password recovery and email confirmation (public); `/Error` server-owned static page; `/share` removed | `/forgot-password`, `/reset-password`, `/confirm-email` | `react-identity.md` |
| 037 | Blazor host retired (`App.razor`, `MainLayout`, `/_blazor`, `blazor.web.js`, `RTUB.Shared`, Blazor CSS / JS); system announcement now a React listener (`GET /api/announcement`) | none new | `react-portal-pilot.md` → *System announcement* |
| 038 | Push v2: "Notificações push" in `/profile` (opt-in / opt-out per device, repair states), `/api/push` rebuilt (own subscriptions only, validated, antiforgery), old push client removed, worker push handlers cleaned | `/profile` (section) | `react-push-v2.md` |
| 041 | Messaging v2 on the preserved v1 tables: conversation list + chat, replies, fixed-set reactions, mute, unread / read, push without the text; text only; no schema change | `/messages` | `react-messages.md` |

Route ownership is code: `src/RTUB.Web/Program.cs` (React shell section). Retired Blazor URLs (`/member/events`,
`/member/gallery`, `/member/roles`, `/hierarchy`, `/members/manage`, `/finance…`, `/calotes`, `/mbway…`, `/nerba…`,
`/member/profile`, `/member/map`)
302 to their React page.

## 025 Novidades (merged)

The task was first named "Newsletter"; it is the Novidades public news feed. Built as `/news` with an approved schema
(`NewsPosts`, migration `AddNewsPosts`); see `docs/react-news.md`. Its follow-ups (images, comments, reactions,
per-post pages with share previews) are separate tasks. Next React task: pick from *Still Blazor* or *Cleanup* below.

## Still Blazor

Nothing. 036 left no Blazor page (5 before 036, 10 before 035, 11 before 034, 13 before 033, 16 before 032) and 037
retired the host: no Razor component is compiled, `/_blazor` and `blazor.web.js` are 404, `RTUB.Shared` (and its bUnit
tests) is gone, and the service worker precaches no Blazor asset. Every page is the React shell; `/Error` is a
server-owned static page on purpose; `/share` stays removed. Push v2 (React opt-in / opt-out in `/profile`, the old
push client removed) is task 038.

**Members' navigation (030).** Signed-in members get around with the React member shell (a rail on wide screens, a
drawer on phones and in the installed app), which links only React pages since 035. `/profile` is
the member's own profile editor (032), not the members' menu. The removed modules (games, bets, MyTuno, images,
labels) are not in it and are not to be brought back; "Mensagens" is Messaging v2 (041), not the old inbox. `/naipes/config` is reached from `/naipes` ("Configurar",
Admin / Owner), not from the menu.

## Future work, outside the migration

- **Games / Bets / MyTuno**: removed from the app by task 026 (no replacement; the old URLs 404). Their tables and
  `AspNetUsers.FidelisBalance` stay until a later contract task drops them: `docs/games-bets-mytuno-removal.md`.
- **Messages / Conversas**: the old inbox was removed by task 027 (`/messages/{id}` and `/hubs/messages` 404; push
  no longer leaves an inbox copy). **Messaging v2 (task 041)** is the React `/messages` on the same four tables, which
  are **preserved, never dropped** (`docs/react-messages.md`, `docs/messages-removal.md`).
- **Images (slideshows) and Labels admin**: `/images` and `/labels` removed by task 029A (no replacement; both 404). The
  Leaderboard story is temporary text in code until the owner decides. 029B drops the `Labels` and `Slideshows` tables
  (with a migration) and the services left behind.

## Cleanup and polish (after or between features)

Recorded in `STATE.md` → *Deferred*; the main ones:
- Dead code with no caller since 013-024 (Shared cards, unused services, CSS, JS): one cleanup task.
- Treasury report delete blocked by a Nerba order: 500 → 409.
- Leitões seeing members' email/phone in member details: decide.
- Real R2 checks on DEV for every storage-backed module (local runs use placeholder storage).
- Receipts and gallery files are public-read in R2 (private objects + pre-signed URLs is a storage change).
- (030, resolved) Sign-out is in the member shell. The old Blazor prompts (push, Play Store, login popup) went with
  `RTUB.Shared` in 037; push opt-in is back in `/profile` (038), the others only if wanted.
- (032, resolved in 038) Push opt-in / opt-out is the `/profile` section "Notificações push" (`react-push-v2.md`).
  Still to check on real devices (Android installed app, iPhone / iPad Home Screen app, DEV) before a production release.
- (035, resolved in 037) The system announcement is a React listener (`react-portal-pilot.md` → *System announcement*);
  the no-caller badges / cards left by 035 went with `RTUB.Shared`.
- (034) Reuniões kept the old visibility rules (Owner limits, the requests gate, the fiscal-year cut at midnight on
  31 August): decide (`docs/react-meetings.md` → *Deferred*).

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
