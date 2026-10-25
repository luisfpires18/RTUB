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
| `/logistics` | - | R + reminders | blocked | W D | W D | W D | [logistics](logistics.md) |
| `/requests` | - | R | blocked | R | R + approve, reject, delete | same as Admin | [requests-questions](requests-questions.md) |
| `/questions` | - | ask + own threads | yes | as Member | as Member | as Member | [requests-questions](requests-questions.md) |
| `/members` | - | R | R | R | W D (Leitões) | W D (anyone) | [members](members.md) |
| `/roles` | public mandate | R (RGI) | public | W | W | W | [governance](governance.md) |
| `/music` | public | R + videos | R | W (no delete) | W D | W D + exclusive | [music](music.md) |
| `/events` | public agenda | R + enrol self | R | + contacts | W D | W D | [events](events.md) |
| `/rehearsals` | - | R + own presença | R | as Member | W D | W D | [rehearsals](rehearsals.md) |
| `/gallery` | public items | R + upload | R | own items | W D all | W D all | [gallery](gallery.md) |
| `/news` | read published | read | read | read | W D + drafts | W D + drafts | [news](news.md) |
| `/leaderboard` | - | R + comment | R | as Member | + delete comments | + delete comments | [leaderboard](leaderboard.md) |
| `/inventory` | - | R | W D | W D | W D | W D | [inventory](inventory.md) |
| `/shop` | public catalogue (R) | R + reserve | R | + add products, promotions | W D + reservations | W D + reservations | [shop](shop.md) |
| `/naipes` | - | R + add own | yes | as Member | + manage all, settings | + manage all, settings | [naipes](naipes.md) |
| `/profile`, `/members/map`, `/hall-of-fame` | sign-in | own profile, R | yes | same | same | same | [member-area](member-area.md) |
| `/emails`, `/notifications`, `/users`, `/owner/*` | - | - | - | - | - | all | [owner-tools](owner-tools.md) |

Leitões also manage `/inventory` (checked by category, not role): keeping the instruments up to date is their job.

The menu groups (Gestão, Logística e Tesouraria, Pedidos, Owner) only decide which *links* show
(`MemberMenuAccess`); every page and API still enforces its own rule.
