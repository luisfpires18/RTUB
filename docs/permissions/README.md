# Permissions by page

Who can do what on each React page. Each file mirrors the server-side rules (the `*Authorization` helpers in
`src/RTUB.Application/Helpers/`, enforced by the services), so a hidden button is never the protection. When a rule
changes, change the helper, its tests, the page's `docs/react-*.md` and **the file here** in the same task.

## Two things decide access

| | What it is | Values |
| --- | --- | --- |
| **System role** | ASP.NET Identity role, one per account | Owner, Admin, Mod, Member |
| **Member type** | what the person is in the tuna | Leitão, Caloiro, Tuno (+ Tuno Honorário, Fundador) are *categories*; **Veterano / Tunossauro** come from `CurrentRole` (computed from the Tuno year); **Magister** and other cargos are *positions* |

A person has one role **and** a type, so an Admin can also be a Veterano or a Leitão. Both are checked, usually the
type first (a Leitão is blocked from several pages whatever the role).

- **Owner inherits Admin** on the React track (the helpers check Owner wherever they check Admin). Exceptions are
  written down in each page file (Owner-only actions).
- **Mod** only gets what a page lists; on most pages Mod has no extra rights.
- Status codes: visitor `401`, signed in but not allowed `403`, a folder/item the caller may not see `404`.

## Overview

`R` read, `W` create/edit, `D` delete. Detail and exceptions are in the linked file.

| Page | Visitor | Member | Leitão | Mod | Admin | Owner | File |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `/documentation` | - | R + upload | blocked | R + upload + manage | R + upload + manage | all | [documentation](documentation.md) |
| `/meetings` | - | by meeting type | blocked | as Member | as Member (+ add participants) | all | [meetings](meetings.md) |
| `/treasury` | - | R (not Caloiro) | own calotes | R + records | R + records + reports | all | [treasury](treasury.md) |
| `/logistics` | - | R (members-visible boards + private ones they are invited to) + "As minhas tarefas"; own cards: edit, checklist, move, comment, attach, own reminders | as Member | W D + moderation, all boards | W D + moderation, all boards | W D + moderation, all boards | [logistics](logistics.md) |
| `/requests` | - | R | blocked | R | R + approve, reject, delete | same as Admin | [requests-questions](requests-questions.md) |
| `/questions` | - | ask + own threads | yes | as Member | as Member | as Member | [requests-questions](requests-questions.md) |
| `/members` | - | R | R | R | W D (Leitões) | W D (anyone) | [members](members.md) |
| `/roles` | public mandate | R (RGI) | public | W | W | W | [governance](governance.md) |
| `/music` | public | R + videos | R | W (no delete) | W D | W D + exclusive | [music](music.md) |
| `/events` | public agenda | R + enrol self | R | + contacts, create / edit | W D | W D | [events](events.md) |
| `/rehearsals` | - | R + own presença | R | + create / edit | W D | W D | [rehearsals](rehearsals.md) |
| `/gallery` | public items | R + upload | R | own items | W D all | W D all | [gallery](gallery.md) |
| `/news` | read published | read | read | W + drafts (no delete) | W D + drafts | W D + drafts | [news](news.md) |
| `/leaderboard` | - | R + comment | R | as Member | + delete comments | + delete comments | [leaderboard](leaderboard.md) |
| `/inventory` | - | R | W D | W D | W D | W D | [inventory](inventory.md) |
| `/shop` | public catalogue (R) | R + reserve | R | + add products, promotions, reservations | W D + reservations | W D + reservations | [shop](shop.md) |
| `/naipes` | - | R + add own | yes | as Member | + manage all, settings | + manage all, settings | [naipes](naipes.md) |
| `/pending-actions` | sign-in | own actions (active: answer Atuações, Ensaios; past = self-report, rehearsals pending approval; everyone: own Logística cards, own open Questões; 1.º/2.º Tesoureiro: Tesouraria; Secretário: Secretaria — by current position, not role) | yes | as member type | as member type | as member type | [pending-actions](pending-actions.md) |
| `/profile`, `/members/map`, `/hall-of-fame` | sign-in | own profile, R | yes | same | same | same | [member-area](member-area.md) |
| `/emails`, `/notifications`, `/users`, `/owner/*` | - | - | - | - | - | all | [owner-tools](owner-tools.md) |

Leitões also manage `/inventory` (checked by category, not role): keeping the instruments up to date is their job.

Logística also has a third axis besides role and type: the card **assignment** - owner and collaborators (tasks
065 / 066, see [logistics](logistics.md)) - and a fourth, **board visibility** (067): private boards are seen only by
their access list and Mod, Admin, Owner; elsewhere a private board is `404`.

The menu flags (`management`, `logistics`, `treasury`, `admin`, `owner` in `MemberMenuAccess`) only decide which
*links* show; every page and API still enforces its own rule. Since 065 `logistics` is its own flag (true for every
member), so a Leitão's Gestão shows Logística only.
