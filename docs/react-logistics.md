# React Logística (`/logistics`, React track 023)

`/logistics` (boards) and `/logistics/{id}` (one board, a Kanban) are React (`portal/src/Logistics.tsx`,
`LogisticsBoard.tsx`, `LogisticsDialogs.tsx`) over `/api/logistics` (`Endpoints/LogisticsEndpoints.cs` →
`ILogisticsKanbanService`), replacing the Blazor `Pages/Management/Logistics.razor` and `LogisticsBoard.razor` (and
`wwwroot/js/kanban.js`). DEV only. Migrations: `AddLogisticsCardWorkspace` (066), `AddLogisticsBoardAccess` (067) and
`AddLogisticsReminderDelivery` (069), all additive (below).

| Route | Owner after 023 |
| --- | --- |
| `/logistics`, `/logistics/{id}` | React, signed-in members, Leitões included since 065 (visitors: 302 to `/login?returnUrl=…`, query kept since 069) |
| `/logistics/{id}?card={cardId}` | the board with that card open (069; links, pushes); a card or board the member can't see shows "não existe ou não tem acesso" |
| `/logistics/tasks` | "As minhas tarefas" (069, `LogisticsTasks.tsx`), signed-in members |

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

## Rules now (server-side; tasks 065 / 066, issues #246 / #247)

Every member (Leitões included) reads every board and card, its discussion and history, and downloads its files. The
card's **owner** and **collaborators** work on it (fields, checklist, status, comments, attachments); Mod, Admin and
Owner manage everything, including owner / collaborators and comment moderation. Full action matrix:
[permissions/logistics](permissions/logistics.md). Private boards / board access are phase 3 (#248): not implemented.

## Boards, access, workflow, files, archive, copies (task 067, issue #248)

**Schema** (`AddLogisticsBoardAccess`, additive only, no table rebuilt): `LogisticsBoards.OwnerUserId` (responsible
member; a plain indexed id - adding a foreign key to an existing SQLite table rebuilds it - so a deleted account reads as
none), `Visibility` (0 Members / 1 Private; existing boards 0), `ArchivedAt`, `LegacyFolder`; `LogisticsLists.State`
(null until mapped); `LogisticsCards.ArchivedAt`; new table `LogisticsBoardMembers` (board, member; unique; cascades with
either). `CardStatus` gains `Blocked` (3).

**Access**: see [permissions/logistics](permissions/logistics.md). One board rule (`CanSeeBoardAsync`) and one card rule
(`AccessAsync`) cover lists, cards, details, comments, mentions, pickers, counts, files and reminders; inaccessible is
`404`. Board responsibility grants nothing.

**Workflow**: each column has a state (TODO / WIP / BLOQUEADO / DONE) and is authoritative - a card's status is its
column's state. Moving a card (managers; owner and collaborators their own cards) sets the status; a status change moves
the card to the end of the first column with that state (none: `400`, the page only offers states the board has).
Changing a column's state changes its cards' status in the same save (history line per card). Deleting a column with
cards needs a destination column (`?moveTo=`); its cards move there and take its state - a column never takes cards
with it. New boards start with Por fazer / Em curso / Bloqueado / Feito.

**Legacy mapping** (`LogisticsWorkflow.MapLegacyAsync`, once at startup, idempotent, logged): a pre-067 column takes the
status most of its cards have (ties: TODO, WIP, BLOQUEADO, DONE); cards with other statuses move, in order, to new
columns "Name (STATE)" right after it; an empty column is DONE / WIP by its name ("feito", "done", "concluído" /
"curso", "wip", "progress"), else TODO. No card changes status or is lost; positions are renumbered. On the local copy
of the real data: 11 columns mapped, 7 split (FITAB's mixed columns), 0 cards in disagreement. Managers can merge the
split columns back by deleting them into another column (the cards take that column's state - a deliberate change).

**Board files**: new files go to `logistics/{env}/boards/{id}/files/` - by id, so a rename or the 1 September rollover
changes nothing, and outside the fiscal-year Documentação tree. Pre-067 files stay where they are
(`docs/{env}/{year}/Logistics/{LegacyFolder}/`) and are listed on the board for **every** year (newest first, with
the year); `LegacyFolder` is the board's sanitised name at mapping time, or none when two boards share it (their old
files stay in Documentação only, as before). Files are addressed by an opaque id (`f:name`, `l:year/name`); `?name=`
still works. Documentação still lists the old per-board folders, except a private board's to members off its access list.
Nothing is moved or deleted in storage.

**Archive**: boards Ativos / Concluídos (still editable, as before) / Arquivados (read-only, every write `409`
"closed"); cards can be archived too (out of the columns, in "Cartões arquivados", read-only). Managers archive and
restore; content is untouched.

**Copies and templates**: duplicate a card or a board (managers; what copies and what never does:
[permissions/logistics](permissions/logistics.md)). Templates **Organizar FITAB** and **Preparar
deslocação/atuação** (`LogisticsTemplates`): pick one, anchor it to an event's date or any date, edit every task
(include, title, deadline, priority, checklist), set visibility and access, then "Criar quadro"
(`POST /boards/compose`). Nothing is assigned or notified.

**Boards page**: tabs Ativos / Concluídos / Arquivados with counts, search, "Com tarefas minhas"; each board shows
Privado, its event, responsible member (or "Sem responsável"), progress (done / total) and overdue, without-owner and
own-task counts; managers' ⋯ menu (edit and access, duplicate, finish / reopen, archive / restore, delete); empty states
offer a template or a blank board.

## Reminders, notifications and "As minhas tarefas" (task 069, issue #249)

**Schema** (`AddLogisticsReminderDelivery`, additive, no table rebuilt): `LogisticsCardReminders.CreatedByUserId`,
`Personal`, `ActivatedAt`, `ClaimedUntil`, `Attempts`. Pre-069 rows come out not activated, no creator, not personal.

**Who schedules** (on the card, `POST`/`PUT`/`DELETE /cards/{id}/reminders[/{reminderId}]`; full matrix in
[permissions/logistics](permissions/logistics.md)): the card's owner and collaborators (Leitões too) set reminders for
themselves only (`Personal`; naming anybody else is `403`); managers for any members who see the board (none =
themselves). Only the creator of a personal reminder (while still working on the card) or a manager edits or deletes
it; pre-069 reminders are managers'. Members see their own reminders on the card; managers see all. The old
board-level `POST /boards/{id}/reminders` and its dialog are gone.

**Time**: `NextReminderDate` is Portugal wall-clock time (Europe/Lisbon). A reminder is due at `PortugalTime.ToUtc` of
it: 10:00 is 10:00 UTC in winter and 09:00 UTC in summer; a daily / weekly reminder steps in wall-clock days, so it
stays at 10:00 across both changes. A time skipped in spring (01:00-01:59 on the last Sunday of March) fires at 02:00
summer time (the instant the clock jumps); a time repeated in autumn fires once, at its first occurrence. The date
must be in the future when saved.

**Delivery** (`LogisticsReminderDelivery`, run every minute by `LogisticsReminderBackgroundService`;
`LogisticsReminders:Enabled`, `IntervalSeconds`):
- Each due occurrence is **claimed** with one atomic `UPDATE … WHERE ClaimedUntil IS NULL OR ClaimedUntil < now` (10
  minutes), so overlapping runs, a restart or a second instance never send it twice while the claim lasts.
- Re-checked at send time: the card exists, is not done and not archived, nor is its board (otherwise the reminder
  **stops**); recipients are the targets who are not expelled and still see the board; a personal reminder only while
  its member is still the card's owner or a collaborator (or manages Logística). Nobody left: it stops.
- Sent as a push plus the "Notificações RTUB" inbox copy (task 061): card title, board name and deadline, never the
  details. Opens `/logistics/{board}?card={id}`.
- Then: a one-time reminder stops; a daily / weekly one moves to its next occurrence after now (missed ones are
  skipped, never replayed).
- **Late**: an occurrence more than 6 hours late (the app was down) is skipped, not sent.
- **Failure**: an exception while sending keeps the occurrence and retries 2 and then 4 minutes later; the third
  failure skips it (the next occurrence stays). Failures of single devices (expired subscriptions, push service
  errors) are the push service's own, as for every push.
- **Guarantee**: at least once per occurrence, not exactly once. A crash between the send and the save sends the
  occurrence again when the claim expires (10 minutes). The push service accepting a push does not prove it was shown;
  members with push off still get the inbox copy.

**Rollout of pre-069 reminders** (saved since 2026-01, never sent): each run first activates rows with no
`ActivatedAt`, once and idempotently, with nothing sent: a one-time reminder already past **stops**; a daily / weekly
one moves to its next occurrence after now; a future one is kept and sent when due. The log line
`Logistics reminders activated: N pre-069 reminder(s); E one-time already past stopped, M recurring moved…` is the
report. Preview before release: `SELECT Frequency, IsActive, NextReminderDate FROM LogisticsCardReminders WHERE
ActivatedAt IS NULL` - the local copy of the real database had **0** reminders, so the rollout is a no-op there.

**Pushes on events** (`LogisticsNotifier`, after the change is committed, never to the member who made it): a new
responsible member (card created or edited by a manager), a new collaborator, an @mention in a new comment (an edit
never notifies again). One push per event per recipient; the recipients were checked against the board in the same
transaction. Card title and board name only. No per-category preference exists yet
([push-notifications](push-notifications.md) → *Designed, not built*): a member who wants none turns push off on this
device in `/profile`; the inbox copy stays.

**As minhas tarefas** (`GET /api/logistics/my-tasks`, `/logistics/tasks`): every card the caller owns or collaborates
on, on the boards they see (assignment never grants access: a card on a private board they are off is not listed),
archived cards and boards left out; managers also get the open cards without a responsible member. Filters (counts
over that list): Por fazer, Atrasadas, Próximos 7 dias, Bloqueadas, Sem responsável (managers), Feitas. Each row
opens its card. Links to it: the boards page and every board.

## The card workspace (task 066, issue #247)

**Schema** (`AddLogisticsCardWorkspace`, additive only, N-1 safe: 2.3.x ignores the new columns and tables):
`LogisticsCards.Priority` (Low 0 / Normal 1 / High 2 / Urgent 3; existing rows **Normal** via the column default, the
model has no default so an explicit Low is kept) and `LogisticsCards.Version` (0); new tables `LogisticsCardComments`
(body ≤ 2000, `MentionsJson` = mentioned ids, `EditedAt`), `LogisticsCardActivities` (history, not a `BaseEntity`: not
copied into the global audit log) and `LogisticsCardAttachments` (file or link). All cascade with the card; a deleted
account sets their author to null. Rollback: plain app rollback (the old app ignores them); the pre-migration snapshot
is taken as usual.

**People, no data rewritten**: the old "Atribuir a" member is the **owner** ("Responsável", optional; a card without one
shows "Sem responsável"); the `LogisticsCardAssignments` rows are the **collaborators** (an owner who also has a row
counts once, as owner). Only managers change either.

**Saving**: the card's own fields (title, details, priority, start, deadline, and the owner for managers) are a draft
saved together by "Guardar alterações" in the dialog footer; the footer says "Alterações por guardar" / "A guardar…" /
"Guardado.", closing with unsaved changes asks first. The save carries `version`: a stale one answers `409`
(`logistics:conflict`) and the page shows "Outra pessoa guardou este cartão…" with "Ver a versão atual (descartar as
minhas)" or "Guardar as minhas por cima" - nothing is lost silently. Status, checklist items, comments and attachments
save at once, item by item (a checklist edit names the item's text, so two people never overwrite each other; a
changed item answers "A lista de tarefas mudou entretanto"), and show only what the server answered.

**Discussion**: plain text (never HTML; line breaks kept), @mentions picked from members (names shown under the
comment; notifications in phase 4). **History**: one pt-PT line per meaningful change (status, title, details,
priority, start, deadline, owner, collaborators, checklist items, attachments, list moves, moderation), with actor and
Portugal time, newest first, the last 100, folded by default. **Attachments**: files (board file types, ≤ 10 MB, upload
progress, error + retry) under a stable key `logistics/{env}/cards/{id}/{random}/{name}` - outside the fiscal-year
Documentação tree, so they never move or show there - and http(s) links; both distinct from the board's files (shown
folded as "Ficheiros do quadro") and from links to other cards ("Cartões ligados"). An attachment is deleted by a manager or
by its uploader while still owner or collaborator (re-checked in the locked change; PR #266 review). Deleting an
attachment, a card, a list or a board deletes the stored files (best-effort, after the rows).

**Layout**: desktop, a two-column dialog (work on the left; status, priority, dates, people, labels, linked cards and
history on the right); phones, full screen, the quick facts first. The deadline shows as "Faltam N dias" / "Falta 1 dia"
/ "Termina hoje" / "Atrasado N dias" by the Portuguese calendar day; priority only when not normal (Urgente in red).
Board cards show priority, deadline, owner or "Sem responsável", and comment / attachment counts.

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
| `GET /api/logistics?q=` | `{ active, completed, archived, canManage }`, each board with owner, visibility and counts (only boards the caller sees) |
| `POST /boards/{id}/archive`, `POST /boards/{id}/duplicate`, `GET /templates`, `POST /boards/compose` | `{ archived }`, `{ name }`, the templates, a reviewed template (managers) |
| `POST /cards/{id}/archive`, `POST /cards/{id}/duplicate` | `{ archived }`, a clean copy (managers) |
| `POST /boards`, `PUT /boards/{id}`, `POST /boards/{id}/state`, `DELETE /boards/{id}` | board CRUD, `{ completed }` |
| `GET /events?q=`, `GET /members?q=&boardId=` | event picker (managers), member picker (≤ 20, not expelled) |
| `GET /boards/{id}` | lists, card faces, label texts, board files, `canManage` |
| `POST /boards/{id}/lists`, `PUT /lists/{id}`, `POST /lists/{id}/move`, `DELETE /lists/{id}` | lists |
| `POST /lists/{id}/cards`, `GET`/`PUT`/`DELETE /cards/{id}` | cards (`GET`: the workspace, every member, with `canManage`, `canWork`, `version`, comments, attachments, activity; `PUT`: workers, `{ …, priority, version }`, stale → `409`) |
| `POST /cards/{id}/status`, `POST /cards/{id}/move` | `{ status }` (workers), `{ listId, position }` (managers) |
| `POST /cards/{id}/checklist`, `PUT`/`DELETE /cards/{id}/checklist/{index}`, `POST /cards/{id}/checklist/{index}` | add `{ task }`, rename `{ expected, task }`, remove `?task=`, tick `{ task, done }` (workers; the text must match) |
| `POST /cards/{id}/comments`, `PUT`/`DELETE /comments/{id}` | `{ body, mentionUserIds }`; edit (author), delete (author or manager) |
| `POST /cards/{id}/attachments` (multipart `file`), `POST /cards/{id}/links`, `GET`/`DELETE /cards/{id}/attachments/{attachmentId}` | the card's files and links; `GET` → `{ url }` |
| `PUT /cards/{id}/labels`, `/links` | replace the set (managers) |
| `POST /cards/{id}/assignments`, `DELETE /cards/{id}/assignments/{userId}` | members |
| `POST /cards/{id}/reminders`, `PUT`/`DELETE /cards/{id}/reminders/{reminderId}` | `{ frequency, nextReminderDate, userIds }` (069; answers the card) |
| `GET /my-tasks` | `{ tasks, canManage }` (069) |
| `GET`/`POST`/`DELETE /boards/{id}/files` | `?file=` (id; `?name=` still works) → `{ url }`; multipart `file`; `?file=` |

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
Since 069: reminders are delivered and assignments / mentions push (above). No email anywhere in the module.

## Follow-ups

- (069, resolved) Reminder delivery, assignment / mention pushes and "As minhas tarefas".
- (042, resolved) `ILogistics{Board,List,Card}Service` and their repositories (no page caller) are removed; the board
  folder sanitizer moved unchanged into `LogisticsKanbanService`. `BoardCard` and `kanban.css` went with 037.
- (065, resolved) Members open card details read-only; assignees tick the checklist and change the status.
- (066, resolved) Ownership, discussion, history and card attachments.
- (067, resolved) Private boards and access lists, workflow columns, stable board files, archive, copies and templates.
  Next: list / calendar views (#250).
- Rollback across 069: the 2.3.x / 067 app ignores the new reminder columns and never sends; reminders saved after 069
  stay stored. Nothing to undo.
- Rollback across 067: the 2.3.x app ignores visibility (a private board is visible to every member again), columns'
  states and archive. Roll back only with that in mind, or restore the pre-migration snapshot.
- The whole-checklist replace (`PUT /cards/{id}/checklist`) is gone (066): it could overwrite another person's ticks.
- Board files follow the current fiscal year: after 1 September a board shows an empty folder (unchanged).
