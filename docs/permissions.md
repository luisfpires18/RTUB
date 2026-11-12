# Permissions by page

Overview of who can do what on each page. Detail and exceptions live in each page's `docs/react-*.md` under
`## Permissions`. Those mirror the server-side rules (the `*Authorization` helpers in `src/RTUB.Application/Helpers/`,
enforced by the services), so a hidden button is never the protection. When a rule changes, change the helper, its
tests, the page doc and this table in the same task.

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
| `/documentation` | - | R + upload | blocked | R + upload + manage | R + upload + manage | all | [documentation](react-documentation.md#permissions) |
| `/meetings` | - | by meeting type; each body's current president: its meetings and atas | blocked | as Member | as Member (+ add participants; no atas) | all types, all atas | [meetings](react-meetings.md#permissions) |
| `/treasury` | - | R (not Caloiro) | own calotes | R + records | R + records + reports | all | [treasury](react-treasury.md#permissions) |
| `/logistics` | - | R (members-visible boards + private ones they are invited to) + "As minhas tarefas"; own cards: edit, checklist, move, comment, attach, own reminders | as Member | W D + moderation, all boards | W D + moderation, all boards | W D + moderation, all boards | [logistics](react-logistics.md#permissions) |
| `/requests` | - | R | blocked | R | R + approve, reject, delete | same as Admin | [requests-questions](react-requests-questions.md#permissions) |
| `/questions` | - | ask + own threads | yes | as Member | as Member | as Member | [requests-questions](react-requests-questions.md#permissions) |
| `/members` | - | R | R | R + complete member info | W D (Leitões) | W D (anyone) | [members](react-members.md#permissions) |
| `/roles` | public mandate | R (RGI) | public | W | W | W | [governance](react-governance.md#permissions) |
| `/music` | public | R + videos | R | W (no delete) | W D | W D + exclusive | [music](react-music.md#permissions) |
| `/events` | public agenda | R + enrol self | R | + contacts, create / edit | W D | W D | [events](react-events.md#permissions) |
| `/rehearsals` | - | R + own presença | R | + create / edit | W D | W D | [rehearsals](react-rehearsals.md#permissions) |
| `/gallery` | public items | R + upload | R | own items | W D all | W D all | [gallery](react-gallery.md#permissions) |
| `/news` | read published | read | read | W + drafts (no delete) | W D + drafts | W D + drafts | [news](react-news.md#permissions) |
| `/leaderboard` | - | R + comment | R | as Member | + delete comments | + delete comments | [leaderboard](react-leaderboard.md#permissions) |
| `/inventory` | - | R | W D | W D | W D | W D | [inventory](react-inventory.md#permissions) |
| `/shop` | public catalogue (R) | R + reserve | R | + add products, promotions, reservations | W D + reservations | W D + reservations | [shop](react-shop.md#permissions) |
| `/naipes` | - | R + add own | yes | as Member | + manage all, settings | + manage all, settings | [naipes](react-naipes.md#permissions) |
| `/pending-actions` | sign-in | own actions (active: answer Atuações, Ensaios; past = self-report, rehearsals pending approval; everyone: own Logística cards, own open Questões; 1.º/2.º Tesoureiro: Tesouraria; Secretário: Secretaria; presidents: Reuniões; Ensaiador / Magister: presenças, repertório — by current position, not role; Mod / Admin / Owner: Membros — by role) | yes | as member type | as member type | as member type | [pending-actions](react-pending-actions.md#permissions) |
| `/profile`, `/members/map`, `/hall-of-fame` | sign-in | own profile, R | yes | same | same | same | [member-area](react-member-area.md#permissions) |
| `/emails`, `/notifications`, `/users`, `/owner/*` | - | - | - | - | - | all | [owner-tools](react-owner-operations.md#permissions) |

Leitões also manage `/inventory` (checked by category, not role): keeping the instruments up to date is their job.

Logística also has a third axis besides role and type: the card **assignment** (owner and collaborators, see
[logistics](react-logistics.md#permissions)), and a fourth, **board visibility**: private boards are seen only by their
access list and Mod, Admin, Owner; for anyone else a private board is `404`.

The menu flags (`management`, `logistics`, `treasury`, `admin`, `owner` in `MemberMenuAccess`) only decide which
*links* show; every page and API still enforces its own rule. `logistics` is true for every member, so a Leitão's
Gestão shows Logística only.
