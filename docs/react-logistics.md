# React Logística (`/logistics`, React track 023)

`/logistics` (boards) and `/logistics/{id}` (one board, a Kanban) are React (`portal/src/Logistics.tsx`,
`LogisticsBoard.tsx`, `LogisticsDialogs.tsx`) over `/api/logistics` (`Endpoints/LogisticsEndpoints.cs` →
`ILogisticsKanbanService`), replacing the Blazor `Pages/Management/Logistics.razor` and `LogisticsBoard.razor` (and
`wwwroot/js/kanban.js`). DEV only; **no schema change** (065 none either).

| Route | Owner after 023 |
| --- | --- |
| `/logistics`, `/logistics/{id}` | React, signed-in members, Leitões included since 065 (visitors: 302 to `/login?returnUrl=…`) |

Links: the React member menu, Gestão → Logística (`MemberMenuAccess.Logistics`, every member since 065; a Leitão's
Gestão shows Logística only), and the React `/profile` actions.

## Audit (old pages: boards 496 lines, board 1945 lines, both `[Authorize]`)

- **Tables**: `LogisticsBoards` (name ≤ 200, description ≤ 2000, optional event, completed + date), `LogisticsLists`
  (name ≤ 100, `Position`), `LogisticsCards` (title ≤ 200, description ≤ 2000, `Position`, status TODO / WIP / DONE,
  optional event, single `AssignedToUserId`, start / due / reminder dates, JSON columns `Labels` `[{Id,Text,Color}]`
  (oldest rows: comma text), `ChecklistJson` `[{Id,Task,Done}]`, `AttachmentsJson` = links to other cards
  `[{Id,Name,CardId}]`), `LogisticsCardAssignments` (members), `LogisticsCardReminders`. FKs cascade board → lists →
  cards → assignments / reminders. Local data: 3 boards, 11 lists, 63 cards; positions collide (see Move).
- **Boards page**: active, then completed, newest first, 4 per page each, search (name, description). Mod/Admin:
  create, edit (name, description, event by name search), finish / reopen, delete (lists and cards too). Leitões sent
  to `/`.
- **Board page**: lists by position, cards by position; label filter. Card face: status bar, dates, title,
  description, labels, event, assignee username, counts (members, checklist x/y, links, board files). Members only read
  (no click, no drag). Mod/Admin: add / rename / delete list (new lists last), add card (title, description, "Atribuir
  a"), click opens details (title, status, labels with colour, dates, details text, checklist, links, board files,
  members), delete card, drag a card between lists (`kanban.js` → `OnCardMoved`). **No Leitão check** on this page.
- **Reminders**: "Criar Lembrete" for every member: card, frequency (once / daily / weekly), first date, members.
  **Nothing in the app ever sends them** (no job reads `LogisticsCardReminders`).
- **Files** ("Anexos"): `docs/{env}/{current fiscal year}/Logistics/{board name, sanitised}/` - one folder **per
  board**, so every card of a board listed the same files; ≤ 10 MB, PDF / Word / Excel / TXT / PNG / JPG; download is
  a pre-signed URL. The same folder shows in Documentação. Deleting a board leaves its files.
- **Move**: wrote only the moved card's list and position, so positions collided.
- **Rules**: `LogisticsAuthorization.CanManage` = Admin or Mod (unit 030), re-checked in the circuit. Owner without
  Admin had no rights. **Notifications**: none. **Old tests**: four bUnit page tests and two page integration tests,
  retired with the pages (the `/emails` Mod-refusal check moved to `LogisticsApiTests`).

## Scope

Everything moved: boards (CRUD, finish / reopen, search), lists (CRUD, now reorderable), cards (CRUD, status, labels,
dates, checklist, links, members, "Atribuir a"), drag and drop, label filter, board files, reminders. No bridge.

## Rules now (`LogisticsAuthorization`, server-side; task 065, issue #246)

| Who | Boards, cards, card details (read), file download | Reminders | Tick checklist, change status | Any other change, files upload / delete |
| --- | --- | --- | --- | --- |
| Visitor | `401` (page: 302) | `401` | `401` | `401` |
| Member, Leitão included | yes | yes | only on cards assigned to them | `403` |
| Mod, Admin, Owner | yes | yes | yes | yes (Owner inherits Admin) |

"Assigned" = the card's "Atribuir a" member or one of its "Membros atribuídos", read from the database on every write
(removing the assignment refuses the next write). Private boards / board access are phase 3 (#248): not implemented,
every board is visible to every member. Detail: [permissions/logistics](permissions/logistics.md).

Validation as the entities: names / titles required and capped, description ≤ 2000, event and members must exist,
links only to other cards of the same board, at most 20 labels (≤ 50, `#rrggbb`), 100 checklist items (≤ 200).
Members see names and avatars only; a member id reaches managers only where they change an assignment (card details
send `userId: null` to non-managers).

## Kanban UX (compared with the Blazor board)

- Columns in a horizontal scroll with snap, edge to edge, fixed comfortable width (272-316 px); the board header and a
  **sticky toolbar** (search, TODO / WIP / DONE filter, label filter, list chips that jump to a column).
- Status as a coloured **left strip and chip** (TODO red, WIP yellow, DONE green); checklist as a **progress bar**;
  labels as chips with a colour dot (readable on any colour); long titles wrap by word.
- Card and list actions in a compact **⋯ menu** (status, "Mover…", delete; rename, move left / right, delete) instead
  of always-visible buttons; "Adicionar cartão" at the foot of each column, "Adicionar lista" as a last column.
- **Drag and drop** with a drop line (mouse); **"Mover…"** (list + position) for touch and keyboard.
- Card details in one dialog: status as three buttons, core fields saved together, the rest saved as you go.
- Task 065: **every card opens** (click or the title button). Non-managers get a read view (`CardView` in
  `LogisticsDialogs.tsx`): status, list, dates, a role note ("Só leitura" or "atribuído a si"), details with line
  breaks, checklist (read-only ticks), people (responsável first), labels, links and the board files with download.
  Assignees also get the three status buttons and live checkboxes; each change waits for the server and shows the
  server's card (an error stays next to the section; nothing shows as saved before the answer). Full screen on phones
  (`.lx-card-modal`, ≤ 599 px, safe areas). Cards assigned to the caller carry an "Atribuído a si" chip and a purple
  edge, and the toolbar offers "Só os meus" when there is at least one.
- Board files in one "Ficheiros" dialog on the board header (they were always per board), not repeated on each card.
- Mobile: no page overflow at 375 px; columns ~84 % of the screen, list chips to jump.

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/logistics?q=` | `{ active, completed, canManage }` |
| `POST /boards`, `PUT /boards/{id}`, `POST /boards/{id}/state`, `DELETE /boards/{id}` | board CRUD, `{ completed }` |
| `GET /events?q=`, `GET /members?q=` | event picker (managers), member picker (≤ 20, not expelled) |
| `GET /boards/{id}` | lists, card faces, label texts, board files, `canManage` |
| `POST /boards/{id}/lists`, `PUT /lists/{id}`, `POST /lists/{id}/move`, `DELETE /lists/{id}` | lists |
| `POST /lists/{id}/cards`, `GET`/`PUT`/`DELETE /cards/{id}` | cards (`GET` details: every member, with `canManage`, `canWork`) |
| `POST /cards/{id}/status`, `POST /cards/{id}/move` | `{ status }` (managers, assignees), `{ listId, position }` |
| `POST /cards/{id}/checklist/{index}` | `{ task, done }`: one item's tick (managers, assignees); `task` must match, else `400 items` |
| `PUT /cards/{id}/labels`, `/checklist`, `/links` | replace the set (managers) |
| `POST /cards/{id}/assignments`, `DELETE /cards/{id}/assignments/{userId}` | members |
| `POST /boards/{id}/reminders` | `{ cardId, frequency, nextReminderDate, userIds }` |
| `GET`/`POST`/`DELETE /boards/{id}/files` | `?name=` → `{ url }`; multipart `file`; `?name=` |

(all under `/api/logistics`). File names lose any path; downloads and deletes accept only names listed in the folder.

## Changed on purpose

- Leitões are refused on the board page too; Owner manages without the Admin role.
- Moving a card renumbers the positions of both lists (fixes collisions); lists can be reordered (the `Position`
  column already existed).
- The member pickers search non-expelled members by nickname or name (they listed every account).
- Saving links refreshes the copied card titles. Labels and checklist are capped (see Rules).
- Board pages: no numbered paging (active boards shown, completed in a collapsible section).

## Storage and notifications

Board files: the old folder and file types, through `IDocumentStorageService`; tests use `FakeDocumentStorage`.
Reminders are stored exactly as before and still never sent (unchanged). No email or push anywhere in the module.

## Follow-ups

- Reminder delivery (a job reading `LogisticsCardReminders`) never existed; decide whether to build or drop it.
- (042, resolved) `ILogistics{Board,List,Card}Service` and their repositories (no page caller) are removed; the board
  folder sanitizer moved unchanged into `LogisticsKanbanService`. `BoardCard` and `kanban.css` went with 037.
- (065, resolved) Members open card details read-only; assignees tick the checklist and change the status. Comments,
  ownership and history are phase 2 (#247); private boards and board access phase 3 (#248); reminder delivery and
  "As minhas tarefas" phase 4 (#249).
- Board files follow the current fiscal year: after 1 September a board shows an empty folder (unchanged).
