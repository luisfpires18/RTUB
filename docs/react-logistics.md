# React Logística (`/logistics`)

Boards and Kanban in React: `portal/src/Logistics.tsx`, `LogisticsBoard.tsx`, `LogisticsDialogs.tsx`,
`LogisticsViews.tsx`, `LogisticsTasks.tsx` over `/api/logistics`.
Code: `Endpoints/LogisticsEndpoints.cs` -> `ILogisticsKanbanService` (`LogisticsKanbanService.*.cs`),
`LogisticsAuthorization`, `LogisticsWorkflow`, `LogisticsTemplates`, `LogisticsNotifier`, `LogisticsReminderDelivery`,
`LogisticsReminderBackgroundService`. Tests: `LogisticsApiTests`.

| Route | Who |
| --- | --- |
| `/logistics`, `/logistics/{id}` | signed-in members, Leitões included (visitors: 302 to `/login?returnUrl=...`, query kept) |
| `/logistics/{id}?card={cardId}` | the board with that card open (links, pushes); a card or board the member can't see shows "não existe ou não tem acesso" |
| `/logistics/tasks` | "As minhas tarefas" |
| `/logistics/{id}?view=list\|calendar&...` | list or calendar view; filters, order and month live in the address |

Menu: Gestão -> Logística (`MemberMenuAccess.Logistics`, true for every signed-in member) and `/profile`. A Leitão's
Gestão shows Logística only (`management` and `treasury` stay false; Reuniões, Documentação, Questões, Tesouraria and
Pedidos keep their own rules).

## Permissions

Server-side, in `LogisticsAuthorization` (manager), `LogisticsKanbanService.CanSeeBoardAsync` (board rule) and
`AccessAsync` (card rule). Every read and write under a board goes through them. Four separate things decide:

| Axis | Effect |
| --- | --- |
| System role | Mod, Admin, Owner **manage** (Owner inherits Admin) and see every board, private ones included |
| Tuna category | None: a Leitão is a member like any other (expelled accounts never get a session) |
| Board visibility | **Todos os membros** (default) or **Privado** (access list only): who sees the board and everything under it |
| Assignment | The card's **owner** ("Responsável") and **collaborators** work on that card only; it never grants access to a private board |

The board's responsible member (`OwnerUserId`) is accountability only: no staff rights, no access (on a private board
they must already be on the list), no right to manage access.

Board access:
- A board the caller may not see is not disclosed: `404` on the board, its files, cards, card details, comments,
  attachments, reminders and the board-scoped member picker (`/members?boardId=`), writes included; it is absent from
  `/api/logistics`. Documentação hides a private board's old folder the same way.
- Access is read from the database on every request; a removal applies to the next call. The page drops what it showed
  on `404`.
- Only members who see the board can be its responsible member, owner or collaborator of its cards, mentioned in its
  comments or targeted by its reminders (`400` "não tem acesso" otherwise; pickers only offer them).
- Only managers create, edit (visibility, access list), duplicate, finish, archive, restore and delete boards; they may
  invite anyone not expelled, Leitões included.

Matrix. `-` = refused (`403`; visitors `401`; unseen board `404`). Viewer = sees the board; Worker = owner or
collaborator of that card (and a viewer). Archived boards/cards: every write `409` except restore.

| Action | Viewer | Worker | Mod / Admin / Owner |
| --- | --- | --- | --- |
| Boards list, board, columns, card faces and details, discussion, history, views | yes | yes | yes (every board) |
| Download board files, card files, open links | yes | yes | yes |
| "As minhas tarefas": own cards on boards they see | yes | yes | yes, plus open cards without a responsible member |
| Reminders on the card: create, edit, delete | - | for themselves only (personal; another target `403`); edit/delete own while still a worker | for any board viewers; edit/delete any |
| See the card's reminders | own | own | all |
| Edit title, details, priority, start, deadline (version-checked, stale `409`) | - | yes | yes |
| Change status (moves to the first column with that state) | - | yes | yes |
| Move card to another column/position | - | own cards | yes |
| Checklist add, rename, remove, tick (one item at a time; text must match) | - | yes | yes |
| Comment, with @mentions of board viewers | - | yes | yes |
| Edit a comment | - | own, while a worker | own |
| Delete a comment | own | own | any (moderation, one history line) |
| Attach a file or link | - | yes | yes |
| Delete an attachment | - | own upload, while a worker | any |
| Change owner, add/remove collaborators | - | - (another owner id `403`) | yes (board viewers only) |
| Labels, links to other cards, duplicate, archive/restore, delete the card | - | - | yes |
| Columns: create, name and state, reorder, delete | - | - | yes |
| Boards: create (blank or template), edit, access, duplicate, finish, archive/restore, delete; board files upload/delete | - | - | yes |

- Nobody edits another author's comment; a manager only deletes it. A deleted account leaves its comments,
  attachments and history lines with no author ("Membro removido") and a board without a responsible member.
- Member ids reach managers only (card details send `userId: null` to non-managers); others get names and avatars.
- Attachment and file ids are checked against the card/board in the URL; a file id is never a storage key.
- Pushes follow the board rule: owner/collaborator/mention pushes go only to members who see the board, never to the
  member who made the change. A reminder re-checks its recipients when sent.

## Data

All additive (no table rebuilt).
- `LogisticsBoards`: `OwnerUserId` (plain indexed id, no FK because adding one rebuilds the SQLite table; a deleted
  account reads as none), `Visibility` (0 Members / 1 Private), `ArchivedAt`, `LegacyFolder`.
- `LogisticsLists.State` (TODO / WIP / BLOQUEADO / DONE); `LogisticsCards.ArchivedAt`, `Priority` (Low 0 / Normal 1 /
  High 2 / Urgent 3; old rows Normal via column default, model has none so an explicit Low is kept), `Version`;
  `CardStatus.Blocked` = 3.
- New tables: `LogisticsBoardMembers` (access list, unique, cascades), `LogisticsCardComments` (body <= 2000,
  `MentionsJson`, `EditedAt`), `LogisticsCardActivities` (history, not a `BaseEntity`, so not in the global audit log),
  `LogisticsCardAttachments` (file or link). All cascade with the card; a deleted account nulls the author.
- Reminders: `LogisticsCardReminders.CreatedByUserId`, `Personal`, `ActivatedAt`, `ClaimedUntil`, `Attempts`.
- Migrations: `AddLogisticsCardWorkspace`, `AddLogisticsBoardAccess`, `AddLogisticsReminderDelivery`.
- Card people: the old "Atribuir a" member is the **owner** (optional, "Sem responsável"); `LogisticsCardAssignments`
  rows are the **collaborators** (an owner with a row counts once). Only managers change either.
- Validation: names/titles required and capped (board name <= 200, list <= 100), description <= 2000, event and members
  must exist, card links only to cards of the same board, <= 20 labels (text <= 50, `#rrggbb`), <= 100 checklist items
  (<= 200 chars).
- Rollback: an older app ignores the new columns/tables. Across the board-access migration it shows private boards to
  every member again and ignores column states and archive; restore the pre-migration snapshot if that matters. Across
  the reminder migration it never sends; saved reminders stay stored.

## Workflow, archive, copies

- Each column has a state and is authoritative: a card's status is its column's state. Moving a card (managers; workers
  their own) sets the status; a status change moves the card to the end of the first column with that state (none:
  `400`). Changing a column's state changes its cards' status in the same save (history line each). Deleting a column
  with cards needs `?moveTo=`; the cards take that column's state. New boards start with Por fazer / Em curso /
  Bloqueado / Feito.
- `LogisticsWorkflow.MapLegacyAsync` runs once at startup, idempotent, logged: a column without a state takes the status
  most of its cards have (ties: TODO, WIP, BLOQUEADO, DONE); cards with other statuses move to new columns "Name
  (STATE)" right after it; an empty column is DONE/WIP by name ("feito", "done", "concluído" / "curso", "wip",
  "progress"), else TODO. No card changes status or is lost. Moving a card renumbers both lists' positions.
- Archive: boards Ativos / Concluídos (still editable) / Arquivados (read-only, writes `409` "closed"). Cards archive too
  (out of the columns, in "Cartões arquivados", read-only). Managers archive/restore; content untouched.
- Duplicate a card (managers): title "(cópia)", details, labels, priority, dates, checklist unticked. Never owner,
  collaborators, comments, history, attachments, reminders or card links.
- Duplicate a board (managers): columns and active cards (as above), visibility, access list and responsible member (a
  copy is never wider than its source). No board files, no archived cards.
- Templates **Organizar FITAB** and **Preparar deslocação/atuação** (`LogisticsTemplates`): pick one, anchor it to an
  event's date or any date, edit every task (include, title, deadline, priority, checklist), set visibility and access,
  then "Criar quadro" (`POST /boards/compose`). Previewed with real dates; nothing is assigned or notified.
- Boards page: tabs Ativos / Concluídos / Arquivados with counts, search, "Com tarefas minhas"; each board shows
  Privado, event, responsible member, progress and overdue/without-owner/own-task counts; managers' menu (edit and
  access, duplicate, finish/reopen, archive/restore, delete); empty states offer a template or a blank board.

## Card workspace

- Card fields (title, details, priority, start, deadline, owner for managers) are a draft saved together by "Guardar
  alterações"; closing with unsaved changes asks first. The save carries `version`: stale -> `409`
  (`logistics:conflict`) and the page offers "Ver a versão atual (descartar as minhas)" or "Guardar as minhas por cima".
  Status, checklist items, comments and attachments save at once, item by item (a checklist edit names the item's text,
  so two people never overwrite each other), and show only what the server answered. The whole-checklist replace is
  gone on purpose: it could overwrite another person's ticks.
- Discussion: plain text (never HTML; line breaks kept), @mentions picked from members. History: one pt-PT line per
  meaningful change, with actor and Portugal time, newest first, last 100, folded by default.
- Attachments: files (board file types, <= 10 MB) stored in the board's Documentação folder (see *Files*), plus
  http(s) links. They show on their card, not in the board's own file list; links to other cards are "Cartões ligados".
  Deleting an attachment, card or list deletes the stored files (best effort, after the rows); deleting a board does
  not.
- Deadline wording by Portuguese calendar day: "Faltam N dias" / "Falta 1 dia" / "Termina hoje" / "Atrasado N dias".
- Every card opens for every member. Non-managers get a read view (`CardView` in `LogisticsDialogs.tsx`); workers also
  get the status buttons and live checkboxes.
- Kanban: horizontal scroll with snap, sticky toolbar (search, status filter, label filter, "Só os meus" when any),
  drag and drop for mouse and "Mover..." (list + position) for touch/keyboard, cards assigned to the caller carry an
  "Atribuído a si" chip. Full screen card dialog on phones (`.lx-card-modal`, <= 599 px).

## Files (in Documentação)

Every Logística file is a Documentação document: one object, two views. Code: `LogisticsKanbanService.Documents.cs`.

- Board files and card attachments go to `docs/{env}/{fiscal year of the upload}/Logistics/{board folder}/{name}`.
  Types: PDF/Word/Excel/TXT/PNG/JPG, <= 10 MB, pre-signed download URL. A name already in the folder becomes
  `name (2).ext`; nothing is overwritten (board and card files share the folder).
- Board folder (`LogisticsBoard.LegacyFolder`, the column keeps its old name): the board's sanitized name, unique among
  boards and among Logistics folders no board owns (a deleted board's files, a name two old boards shared); otherwise
  `Name 2`, `Name 3`. Set when the board is created, copied or composed.
- Renaming a board moves its folder, in every fiscal year, to the new name; card rows follow their files.
- Delete: on the board or card, it leaves Documentação too. In Documentação (Owner), deleting a card's file also removes
  the card's attachment row, with a history line "Anexo removido em Documentação"; deleting the folder does the same for
  every card file in it. Deleting a board leaves its folder.
- Startup (`MoveFilesIntoDocumentationAsync`, idempotent, best effort per file): files kept outside Documentação before
  this (`logistics/{env}/boards/{id}/files/`, `logistics/{env}/cards/...`) and folders under an older board name move
  into the board's folder.
- The board lists its folder in every fiscal year, newest first, without its cards' files. Ids are opaque: `f:name` this
  fiscal year, `l:year/name` an older one; `?name=` still works. Names lose any path; downloads and deletes accept only
  listed names. Documentação hides a private board's folder from members off its access list.
- Board file upload/delete is managers only; download is any viewer.

## Views: Quadro, Lista, Calendário

Three views over the same cards (the active cards of `GET /boards/{id}`, only what the caller may see; archived cards
count nowhere), after the same filters. They all open the card workspace and write nothing themselves: no extra
endpoint, so API limits are the Kanban's.
- Filters (one bar): search in title and details, state, responsible member ("Os meus cartões", "Sem responsável", a
  name), priority, deadline ("Atrasados", "Próximos 7 dias", "Sem prazo"), label.
- Address: `view`, `q`, `status`, `label`, `who` (`mine`, `none`, `person`), `prio`, `due`, `sort` (list), `month`
  (calendar), `card`. A name is never put in the address (links get shared): it lives in `history.state` of that entry,
  so a shared link or fresh visit has no person filter. Switching view adds a history entry; other changes replace it.
- Summary: Feitos, Atrasados (deadline before today in Portugal, not done), Bloqueados, Sem responsável. Always
  board-wide; with filters on, "A mostrar X de Y (filtros ativos)" says so.
- Lista: table ordered by deadline (undated last), priority, state or title; below 768px each row is a stacked card.
- Calendário: a month, Monday first, deadlines compared with today in Europe/Lisbon; overdue cards outlined and those
  from earlier months listed above the grid; cards without a deadline in "Sem prazo", never dropped. Below 768px the
  grid becomes an agenda.
- Keyboard: Enter opens the card, Escape closes it.
- Responsive audit: `tests/responsive/responsive_audit.py` (`logistics-*` cases); the Kanban scroller counts as
  contained, not overflow.

## Reminders

Delivery: `LogisticsReminderDelivery`, run every minute by `LogisticsReminderBackgroundService`
(`LogisticsReminders:Enabled`, `IntervalSeconds`).

- Who schedules (on the card, `POST`/`PUT`/`DELETE /cards/{id}/reminders[/{reminderId}]`): owner and collaborators set
  reminders for themselves only (`Personal`; naming anybody else `403`); managers for any board viewers (none =
  themselves). Only the creator of a personal reminder (while still working on the card) or a manager edits/deletes it;
  rows saved before delivery existed are managers'. Members see their own reminders on the card; managers all.
- Time: `NextReminderDate` is Portugal wall-clock time, due at `PortugalTime.ToUtc` of it (10:00 is 10:00 UTC in
  winter, 09:00 UTC in summer). Daily/weekly steps in wall-clock days. A time skipped in spring fires at 02:00 summer
  time; a time repeated in autumn fires once, at its first occurrence. The date must be in the future when saved.
- Claim: each due occurrence is claimed with one atomic `UPDATE ... WHERE ClaimedUntil IS NULL OR ClaimedUntil < now`
  (10 minutes), so overlapping runs, restarts or a second instance never send it twice while the claim lasts.
- Re-checked at send: the card exists, is not done or archived, nor is its board (else the reminder stops); recipients
  are targets who are not expelled and still see the board; a personal reminder only while its member is still owner or
  collaborator (or manages Logística). Nobody left: it stops.
- Sent as a push plus the "Notificações RTUB" inbox copy: card title, board name and deadline, never the details. Opens
  `/logistics/{board}?card={id}`.
- After sending: one-time stops; daily/weekly moves to its next occurrence after now (missed ones skipped, never
  replayed).
- Late: more than 6 hours late (app was down) is skipped. Failure: an exception keeps the occurrence and retries 2 then
  4 minutes later; the third failure skips it. Guarantee: at least once, not exactly once (a crash between send and
  save re-sends after the claim expires). Members with push off still get the inbox copy.
- Rows without `ActivatedAt` are activated at the start of each run, nothing sent: a past one-time reminder stops;
  daily/weekly moves to its next occurrence after now; a future one is kept. The log line `Logistics reminders
  activated: ...` reports it.

Event pushes (`LogisticsNotifier`, after commit, never to the member who made the change): new responsible member
(card created or edited by a manager), new collaborator, @mention in a new comment (an edit never notifies again). One
push per event per recipient, card title and board name only. No per-category preference yet
([push-notifications](push-notifications.md) -> *Designed, not built*): a member who wants none turns push off in
`/profile`; the inbox copy stays. No email anywhere in the module.

## As minhas tarefas and pending actions

`GET /api/logistics/my-tasks` (`/logistics/tasks`): every card the caller owns or collaborates on, on boards they see
(assignment never grants access); archived cards and boards left out; managers also get open cards without a
responsible member. Filters (counts over that list): Por fazer, Atrasadas, Próximos 7 dias, Bloqueadas, Sem responsável
(managers), Feitas.

The Logística panel of `/pending-actions` uses the same rule (`LogisticsKanbanService.WorkableCards`, unowned cards
not listed even for managers), paged in SQL, with **Concluir** (`SetStatusAsync` as the caller, to the board's done
column) and **Abrir** (the card deep link). Nothing new is allowed: an assignee could already change status. See
[react-pending-actions](react-pending-actions.md).

## API

Under `/api/logistics`. GET responses are `no-store`; writes need `X-CSRF-TOKEN`.

| Endpoint | Does |
| --- | --- |
| `GET ?q=` | `{ active, completed, archived, canManage }`, boards with owner, visibility, counts (only boards the caller sees) |
| `POST /boards`, `PUT /boards/{id}`, `POST /boards/{id}/state`, `DELETE /boards/{id}` | board CRUD, `{ completed }` |
| `POST /boards/{id}/archive`, `POST /boards/{id}/duplicate`, `GET /templates`, `POST /boards/compose` | archive, copy, templates, reviewed template (managers) |
| `GET /boards/{id}` | lists, card faces, label texts, board files, `canManage` |
| `GET /events?q=`, `GET /members?q=&boardId=` | event picker (managers), member picker (<= 20, not expelled) |
| `POST /boards/{id}/lists`, `PUT /lists/{id}`, `POST /lists/{id}/move`, `DELETE /lists/{id}` | columns |
| `POST /lists/{id}/cards`, `GET`/`PUT`/`DELETE /cards/{id}` | cards (`GET`: workspace with `canManage`, `canWork`, `version`, comments, attachments, activity; `PUT`: workers, stale `409`) |
| `POST /cards/{id}/archive`, `POST /cards/{id}/duplicate` | managers |
| `POST /cards/{id}/status`, `POST /cards/{id}/move` | `{ status }` (workers), `{ listId, position }` (managers) |
| `POST /cards/{id}/checklist`, `PUT`/`DELETE .../checklist/{index}`, `POST .../checklist/{index}` | add `{ task }`, rename `{ expected, task }`, remove `?task=`, tick `{ task, done }` (workers) |
| `POST /cards/{id}/comments`, `PUT`/`DELETE /comments/{id}` | `{ body, mentionUserIds }`; edit (author), delete (author or manager) |
| `POST /cards/{id}/attachments` (multipart `file`), `POST /cards/{id}/links`, `GET`/`DELETE /cards/{id}/attachments/{attachmentId}` | card files and links; `GET` -> `{ url }` |
| `PUT /cards/{id}/labels`, `PUT /cards/{id}/links` | replace the set (managers) |
| `POST /cards/{id}/assignments`, `DELETE /cards/{id}/assignments/{userId}` | collaborators |
| `POST /cards/{id}/reminders`, `PUT`/`DELETE /cards/{id}/reminders/{reminderId}` | `{ frequency, nextReminderDate, userIds }`; answers the card |
| `GET /my-tasks` | `{ tasks, canManage }` |
| `GET`/`POST`/`DELETE /boards/{id}/files` | `?file=` -> `{ url }`; multipart `file`; `?file=` |

Saving card links refreshes the copied card titles. Member pickers search non-expelled members by nickname or name.

## Follow-ups

- Not built: external calendar subscription (iCal), per-category push preferences.
