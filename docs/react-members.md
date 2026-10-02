# React Members (`/members`, React track 017)

`/members` is React (`portal/src/Members.tsx`, `MemberDialogs.tsx`, `MembersHierarchy.tsx`) over read-only
`/api/members` (`Endpoints/MemberEndpoints.cs` → `IMemberDirectoryService`). It replaces what every signed-in member
used on the Blazor `/members` (directory, "Detalhes do Membro", "Gestão de Membros Ativos" list, "Aniversários") and
the Blazor `/hierarchy`. DEV only; **no schema change**.

| Route | Owner after 017 |
| --- | --- |
| `/members` (`?q=&category=&subCategory=&instrument=&activeOnly=true&member=<id>`) | React, signed-in members (visitors: 302 to `/login?returnUrl=`) |
| `/members/hierarchy` | React, signed-in members |
| `/hierarchy` | `302` → `/members/hierarchy` (GET/HEAD only); Blazor page retired |
| `/members/manage` | **Blazor bridge**, Admin and Owner only: the old page's management tools |
| `/leaderboard` (Classificação), `/hall-of-fame`, `/member/map`, `/member/profile` | Blazor, unchanged (not in this task) |

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

| Who | Directory, details (with contacts), active, birthdays, hierarchy | `/members/manage` |
| --- | --- | --- |
| Visitor | `401` (pages: 302 to sign in) | 302 to sign in |
| Member, Mod | yes | refused (302) |
| Admin, Owner | yes, plus the "Gerir membros" link | yes (per-action checks unchanged) |

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

## Follow-ups

- Member-admin task: create/edit/delete, instruments, nicknames, expel/reactivate, make active, push reminder; then
  `/members/manage` goes.
- Classification (`/leaderboard`) in React.
- `wwwroot/js/familyTree.js` (loaded by `MainLayout`) and the `.family-tree-*` rules in `css/1-base/mobile.css` are
  unused since the Blazor hierarchy went; the birthday-email code in `Members.razor` has no caller.
