# React Members (`/members`, React tracks 017 and 018)

`/members` is React (`portal/src/Members.tsx`, `MemberDialogs.tsx`, `MemberManage.tsx`, `MembersHierarchy.tsx`) over
`/api/members` (`Endpoints/MemberEndpoints.cs` → `IMemberDirectoryService` for reads, `IMemberAdminService` for the
Admin/Owner tools). It replaces the Blazor `/members` (directory, "Detalhes do Membro", "Gestão de Membros Ativos",
"Aniversários" in 017; the admin tools in 018) and the Blazor `/hierarchy`. DEV only; **no schema change**.

| Route | Owner after 018 |
| --- | --- |
| `/members` (`?q=&category=&subCategory=&instrument=&activeOnly=true&member=<id>`) | React, signed-in members (visitors: 302 to `/login?returnUrl=`); Admin/Owner tools in modals |
| `/members/hierarchy` | React, signed-in members |
| `/hierarchy` | `302` → `/members/hierarchy` (GET/HEAD only); Blazor page retired |
| `/members/manage` | `302` → `/members` (GET/HEAD only, 018); `Members.razor` retired |
| `/leaderboard` (Classificação) | React since 019: `docs/react-leaderboard.md` |
| `/hall-of-fame`, `/member/map`, `/member/profile` | Blazor, unchanged (not in these tasks) |

## Audit (old pages)

- **`/members`** (`Members.razor`, 3,550 lines, `[Authorize]`): a directory plus a full member-admin system.
  - Any member: search (first/last name, nickname, email, phone, city; multi-word), category (Leitão, Caloiro, Tuno,
    Tuno Honorário), Tuno sub-category (Tuno, Veterano, Tunossauro, Fundador), instrument (primary or secondary), "só
    ativos" (`IsRetired`); Tunos Honorários hidden unless filtered or searched. Members by nickname (30 per page),
    Leitões by activities then last activity (18 per page; greyed when expelled, "Não tem participado" with no
    activity this month). Cards: photo, Online/Offline (signed in within 1 h), nickname, name, Caloiro/Tuno/Honorário
    badges, the current fiscal year's position, "Sem Cargos", primary instrument.
  - Any member, "Detalhes do Membro": **email, phone, city, birth date + age, degree**, instruments (not for Tunos
    Honorários), Padrinho (not for Fundadores, Honorários, Leitões), "Percurso na Tuna" timeline, "Estado na Tuna"
    (active categories with activity: no-ativo/reformado, progress, encouragement, last rehearsal/event, activity list).
  - Any member: the active-members list (Caloiro/Tuno/Veterano/Tunossauro, ordered by retired, reactivation progress,
    recent activity) and the birthdays still to come this year.
  - Admin (`IsInRole("Admin")`, UI only): create (welcome email), edit (dates only for Owner or the current Magister),
    instruments, nickname for Leitões (email), expel/reactivate Leitões, make active, push reminder; Owner: delete
    regular members; Admin: delete Leitões. A birthday-email flow exists in the code but no button calls it.
- **`/hierarchy`** (`Hierarchy.razor`, `[Authorize]`): `IMemberHierarchyService` tree of effective members
  (Caloiro/Tuno/Veterano/Tunossauro, so Honorários too, never Leitões); only Tunos and above are padrinhos; roots by
  first/last name, afilhados by Caloiro year/month then name.
- **Data** (read-only scratch copy, aggregates): 108 accounts, 0 expelled, 75 retired, 64 with a padrinho, 64 with a
  birth date; no address, notes or emergency-contact fields exist (`City` only).
- **Classification** is `/leaderboard` ("Tabela de Classificação", `Activities/Leaderboard.razor`, 932 lines): XP
  ranking with levels, comments (writes), statistics and `RankingConfiguration`. A separate Activities module.

## Boundary decision

- Moved: directory, details, active-members list, birthdays, hierarchy - everything a member could do, read-only.
- **Bridge:** the admin tools are a member-admin system (account creation, Identity, emails, push), too large to
  rebuild safely here. `Members.razor` is unchanged except its route (`/members/manage`) and
  `[Authorize(Roles = "Admin,Owner")]`; React `/members` links there for Admin/Owner ("Gerir membros"). The page still
  shows its own grid, for admins only, until the member-admin task.
- **Deferred:** `/leaderboard` keeps its Blazor page (own task: scoring, comments, config). Hall of Fame and the member
  map unchanged.

## Rules (`MembersAuthorization`, server-side)

| Who | Directory, details (with contacts), active, birthdays, hierarchy | Admin tools (018) |
| --- | --- | --- |
| Visitor | `401` (pages: 302 to sign in) | `401` |
| Member, Mod | yes | `403` |
| Admin | yes | create, edit, instruments, Leitões' nickname / expel / reactivate, "Tornar ativo", push reminder, delete **Leitões** |
| Owner | yes | everything Admin does (Owner inherits Admin), plus delete **any** member |

Complete Leitão / Caloiro / Tuno date pairs (year and month) change only for Owner or an Admin who is this fiscal year's
Magister; anyone else's change to them is ignored, as before (`lockedDates` greys them in the form). An incomplete pair
can be completed by any Admin.

The cards never carry email, phone, city or birth date; the search still matches them. Details carry the contacts the
old modal showed any signed-in member. Birthdays send day/month and the age, not the birth date.

## API (all GET, `no-store`)

| Endpoint | Returns |
| --- | --- |
| `/api/members?q=&category=&subCategory=&instrument=&activeOnly=` | `{ members, leitoes, instruments, canManage }`; unknown filter → 400 |
| `/api/members/{id}` | the details; 404 when unknown |
| `/api/members/active?status=&q=` | the active-members list (`status`: active / retired) |
| `/api/members/birthdays?q=` | birthdays from today to 31 December |
| `/api/members/hierarchy` | the tree (`children` nested) |

## Changed on purpose

- Pagination is "Mostrar mais" (30 / 18 at a time) instead of numbered pages.
- The hierarchy is an indented list (any depth fits a phone) instead of the horizontal scrolling tree.
- `/members/manage` (the old page) is Admin/Owner only: its member-facing parts are React now.
- 29 February birthdays are skipped in common years (the old list threw).
- Copy is rewritten (portal originality rule); labels and badges keep the old wording.

## Member admin (018; was `/members/manage`)

Audit of the old page's tools (`Members.razor`, `IsInRole` checks in the UI only) and what React keeps:

- **Create** ("Adicionar membro"): first/last name, nome de tuna, contacto, email (required; 80/80/80/80 max, email
  format: the entity's own annotations), birth date, curso, cidade (100 max), categoria (Leitão / Caloiro / Tuno,
  required), Fundador / Tuno Honorário (Tuno only, one or the other), month/year dates, instruments, padrinho (Caloiro /
  Tuno only, Tunos and above, never themselves). Username = normalised nickname; a Leitão "sem alcunha" takes the
  email's local part as nickname and username. Taken username / email refused with the old messages. Generated
  password, `RequirePasswordChange`, email confirmed, role `Member`, then the **welcome email** (fake in tests).
- **Edit**: the same fields; the username never changes. Fundador = Tuno since 12/1991; Fundador and Tuno Honorário
  lose Leitão / Caloiro dates and padrinho. Categories are rebuilt from the form, as before.
- **Instruments** (edit: saved one by one): add (first = primary; duplicate refused), set primary, remove (removing the
  primary promotes the next).
- **Leitões**: "Definir alcunha" (nickname + normalised username; taken username refused; the member gets the
  username-changed email), **expel / reactivate** (`IsExpelled`; the server refuses them on non-Leitões).
- **Gestão de Membros Ativos**: "Tornar ativo" when retired or on the 3 months back (`ActivateMemberWithOverrideAsync`),
  and the **push reminder** only to a retired member 2/3 months back with nothing this month (`SendToUserAsync`, fake
  in tests). Both refused server-side outside those conditions.
- **Delete**: the old hard delete with related data (`IUserProfileService.DeleteMemberWithRelatedDataAsync`).
- **Not moved** (no caller in the old page): the birthday-email flow and the Owner-only "toggle retired".

| Endpoint (Admin/Owner; writes need `X-CSRF-TOKEN`) | Does |
| --- | --- |
| `GET /api/members/{id}/edit` | the form's values, instruments, `lockedDates`; no password, role or audit field |
| `GET /api/members/mentors?q=&exclude=` | padrinho candidates (Tuno and above), 20 at most |
| `POST /api/members` / `PUT /api/members/{id}` | create (201) / edit; 400 with per-field errors |
| `POST /api/members/{id}/instruments`, `PUT …/instruments/{iid}/primary`, `DELETE …/instruments/{iid}` | instruments; answer the new list |
| `PUT /api/members/{id}/nickname`, `POST /api/members/{id}/expel` / `reactivate` | Leitões only (403 otherwise) |
| `POST /api/members/{id}/activate` / `reminder` | under the old list's conditions (403 otherwise) |
| `DELETE /api/members/{id}` | 204; Admin: Leitões only |

Changed on purpose (018): the rules are server-side (the old page only hid buttons); Owner inherits Admin; a padrinho
must exist and be Tuno or above; impossible dates (outside 1990-now, month outside 1-12) are refused; a welcome or
username email that fails is logged instead of breaking the page (the account is saved either way).

## Follow-ups

- (019) Classification (`/leaderboard`) is React: `docs/react-leaderboard.md`.
- Owner can delete their own account from the tools (as before); consider refusing self-delete.
- `IMemberPositionService` and `IUserRoleQueryService` have no caller left since `Members.razor` went (only their DI
  registrations), nor does the shared `AvatarCard` component; not removed here.
- `wwwroot/js/familyTree.js` (loaded by `MainLayout`) and the `.family-tree-*` rules in `css/1-base/mobile.css` are
  unused since the Blazor hierarchy went; the birthday-email code in `Members.razor` has no caller.
