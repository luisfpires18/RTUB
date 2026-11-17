# React Members (`/members`, `/members/hierarchy`)

React: `portal/src/Members.tsx`, `MemberDialogs.tsx`, `MemberManage.tsx`, `MembersHierarchy.tsx` over `/api/members`.
Code: `Endpoints/MemberEndpoints.cs`, `Helpers/MembersAuthorization.cs`, `MemberInfoRules`, `IMemberDirectoryService`
(reads), `IMemberAdminService` (tools), `MemberHierarchyService`. Related: Classificação in
[react-leaderboard](react-leaderboard.md); profile, map and Hall of Fame in [react-member-area](react-member-area.md).

| Route | Behavior |
| --- | --- |
| `/members` (`?q=&category=&subCategory=&instrument=&activeOnly=true&member=<id>&info=<id>`) | signed-in members; visitors get 302 to `/login?returnUrl=`; admin tools in modals |
| `/members/hierarchy` | signed-in members |
| `/hierarchy`, `/members/manage` | `302` to `/members/hierarchy` and `/members` (GET/HEAD only) |

## Permissions

Enforced server-side by `MembersAuthorization`.

| Who | Directory, details (with contacts), active, birthdays, hierarchy | Tools |
| --- | --- | --- |
| Visitor | `401` | `401` |
| Member (Leitão included) | yes | `403` |
| Mod | yes | create (always a plain Member account), "Completar informação", instruments; the rest `403` |
| Admin | yes | create, edit, instruments, Leitões' nickname / expel / reactivate, push reminder, delete **Leitões** |
| Owner | yes | everything Admin does (Owner inherits Admin), plus delete **any** member and change complete date pairs |

Complete Leitão / Caloiro / Tuno date pairs (year and month) change only for Owner or an Admin who is this fiscal year's
Magister; anyone else's change is ignored in the full form (`lockedDates` greys them). An incomplete pair can be
completed by any Admin.

Cards never carry email, phone, city or birth date (search still matches them). Details carry the contacts, for any
signed-in member. Birthdays send day/month and age, not the birth date.

## Directory API (GET, `no-store`)

| Endpoint | Returns |
| --- | --- |
| `/api/members?q=&category=&subCategory=&instrument=&activeOnly=` | `{ members, leitoes, instruments, canManage }`; unknown filter is 400 |
| `/api/members/{id}` | details; 404 when unknown |
| `/api/members/active?status=&q=` | active-members list (`status`: active / retired) |
| `/api/members/birthdays?q=` | birthdays from today to 31 December (29 Feb skipped in common years) |
| `/api/members/hierarchy` | the tree (`children` nested) |

Directory rules: Tunos Honorários are hidden unless filtered or searched; "Mostrar mais" paging (30 members, 18
Leitões); Leitões are shown by activities then last activity.

## Hierarchy

`MembersHierarchy.tsx`: one WAI-ARIA tree (`role="tree"`, nested `treeitem` / `group`), CSS only, no graph library.

- Tree of effective members (Caloiro / Tuno / Veterano / Tunossauro, Honorários too, never Leitões); only Tunos and
  above are padrinhos; roots by name, afilhados by Caloiro year/month then name.
- A member recorded as their own padrinho is a root; a padrinho loop is cut at its first member by name, who becomes a
  root. The stored `MentorId` is untouched.
- Members with no padrinho and no afilhados are listed together under "Sem ligações de padrinho".
- Wide screens (>= 900 px): left to right, one card per family, all branches open; a deeper family scrolls inside its
  own card. Phones / tablets: same DOM indented, first two levels open.
- Interaction: the card links to `/members?member=`; a round button folds afilhados; "Expandir tudo" / "Recolher tudo";
  accent-insensitive search selects and focuses a member, highlights the path to the root; "Focar ramo" shows one
  branch. Keyboard: one tab stop, arrows, Home / End, Enter, type-ahead; scrolling respects `prefers-reduced-motion`.

Tests: `MemberHierarchyServiceTests`, `MembersHierarchySourceTests`, `MembersApiTests`.

## Member admin

Writes need `X-CSRF-TOKEN`. Rules are enforced by the API, not only by hidden buttons.

| Endpoint | Does |
| --- | --- |
| `GET /api/members/{id}/edit` | form values, instruments, `lockedDates`; no password, role or audit field |
| `GET /api/members/mentors?q=&exclude=` | padrinho candidates (Tuno and above), 20 at most |
| `POST /api/members` / `PUT /api/members/{id}` | create (201) / edit; 400 with per-field errors |
| `POST /api/members/{id}/instruments`, `PUT .../instruments/{iid}/primary`, `DELETE .../instruments/{iid}` | instruments; answer the new list |
| `PUT /api/members/{id}/nickname`, `POST .../expel`, `POST .../reactivate` | Leitões only (403 otherwise) |
| `POST /api/members/{id}/reminder` | push reminder, 403 unless the member is retired 2 of 3 months back with nothing this month |
| `DELETE /api/members/{id}` | 204; Admin: Leitões only |

- Create ("Adicionar membro"): first/last name, nome de tuna, contacto, email (required; 80 max each, entity
  annotations), birth date, curso, cidade (100 max), categoria (Leitão / Caloiro / Tuno), Fundador / Tuno Honorário (Tuno
  only, one or the other), month/year dates, instruments, padrinho (Caloiro / Tuno only; Tuno or above; never
  themselves). Username = normalized nickname; a Leitão "sem alcunha" takes the email's local part as nickname and
  username. Taken username / email refused. Generated password, `RequirePasswordChange`, email confirmed, role `Member`,
  then the welcome email (a failed email is logged, the account is saved anyway).
- Edit: same fields; the username never changes. Fundador = Tuno since 12/1991; Fundador and Tuno Honorário lose
  Leitão / Caloiro dates and padrinho. Categories are rebuilt from the form. Impossible dates (outside 1990-now, month
  outside 1-12) are refused.
- Instruments: first added is primary; duplicate refused; removing the primary promotes the next.
- "Definir alcunha" (Leitões): nickname + normalized username; the member gets the username-changed email.
- Expel / reactivate set `IsExpelled`; refused on non-Leitões.
- Activity state is decided only by the rules in [member-activity-status](member-activity-status.md); there is no
  manual "make active" (the `OverrideRetired` flag is dead). A new member starts active only as a Leitão.
- Delete is a hard delete with related data (`IUserProfileService.DeleteMemberWithRelatedDataAsync`).

### Mod: "Completar informação"

Mod (and Admin / Owner) completes approved members' information through `PUT /api/members/{id}/info`
(`MemberInfoInput`); the full edit stays Admin / Owner.

| Field | Mod may | Applies to (`MemberInfoRules`) |
| --- | --- | --- |
| Cidade, Contacto | edit | every member in scope |
| Instruments (add, remove, primary) | edit (the instrument endpoints allow Mod) | everyone but a Tuno Honorário |
| Entrada na Tuna (Leitão date) | fill when missing; a complete pair stays Owner, or the current Magister with Admin (a Caloiro Magister holds Mod, so not) | Leitão, Caloiro, Tuno; not Fundador / Honorário |
| Passagem a Caloiro | same | Caloiro, Tuno; not Fundador / Honorário |
| Passagem a Tuno | same | Tuno; not Fundador (always December 1991) |
| Padrinho | edit (Tuno or above; nobody their own) | Leitão, Caloiro, Tuno; not Fundador / Honorário |

- Allow-list by contract: `MemberInfoInput` has only those fields; anything else in the body (name, email, category,
  role, position, expelled / active status, password) is not bound. A field that does not apply must stay empty (`400`);
  changing a complete date pair without Owner / an Admin Magister is `400` ("Esta data está fechada: não a podes alterar
  aqui."; no system role in the text).
- Concurrency: the GET returns the member's `Stamp` (Identity concurrency stamp); a save with an older one is `409`
  ("Os dados deste membro mudaram entretanto") and writes nothing. Validation errors keep what was typed.
- Audit: same `ApplicationUser` update as the full form, audited with actor and fields.
- Scope: not expelled, with a Leitão / Caloiro / Tuno category (honorary-only is out); otherwise `404` for a Mod.
- Not given to Mod: delete, full edit (`GET /edit`, `PUT /{id}`), "Definir alcunha", expel / reactivate, push reminder,
  categories, roles, positions, activity status.
- UI: a Mod gets "Completar informação" in a member's details (Admin / Owner keep "Editar"); `/members?info={id}` opens
  it once (from "As minhas ações pendentes" → Membros).

## Follow-ups

- Owner can delete their own account from the tools; consider refusing self-delete.
