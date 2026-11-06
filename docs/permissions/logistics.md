# Permissions: /logistics

Sources: `LogisticsAuthorization` (who is a manager), the board rule `LogisticsKanbanService.CanSeeBoardAsync`
(`LogisticsKanbanService.Boards.cs`) and the card rule `AccessAsync` (`LogisticsKanbanService.CardWorkspace.cs`). Every
read and write under a board goes through them. Page doc: [react-logistics](../react-logistics.md). Rules as of task 067
(issue #248, Logística 3/5); 065 (#246) opened Logística to Leitões, 066 (#247) made the card a workspace.

Four separate things decide what a person may do here; keep them apart:

| Axis | What it is | Effect in Logística |
| --- | --- | --- |
| **System role** | Identity role: Member, Mod, Admin, Owner | Mod, Admin, Owner **manage** (Owner inherits Admin) and **see every board**, private ones included (staff override) |
| **Tuna category** | Leitão, Caloiro, Tuno, … | **No effect**: a Leitão is a member like any other here. (Expelled accounts never get a session.) |
| **Board visibility** | **Todos os membros** (default, every pre-067 board) or **Privado** (its access list only) | who may see the board and everything under it |
| **Assignment** | the card's **owner** ("Responsável") and **collaborators** | that card only: work on it - **never grants access** to a private board |

The board's **responsible member** (`OwnerUserId`) is accountability only: it grants no staff rights, no access rights
(on a private board they must already be on the access list), no right to manage access.

## Board access

- A board the caller may not see is **not disclosed**: `404` on the board, its file list and files, its cards, card
  details, comments, attachments, reminders, the board-scoped member picker (`/members?boardId=`), writes included; it is
  absent from `/api/logistics` (lists and counts). Documentação hides a private board's pre-067 folder the same way.
- Access is read from the database on every request, so a removal (from the access list, or a board made private)
  applies to the next read or write. The page drops what it showed when it gets `404`.
- Only members who may see the board can be its responsible member, an owner or collaborator of its cards, mentioned in
  its comments or targeted by its reminders (`400` "não tem acesso" otherwise; the pickers only offer them).
- Only managers create, edit (incl. visibility and access list), duplicate, finish, archive, restore and delete boards;
  they may invite anyone not expelled, Leitões included.

## Action matrix

`✓` allowed, `-` refused (`403`; visitors `401`; a board the caller may not see `404`). "Viewer" = sees the board;
"Worker" = owner or collaborator of **that** card (and a viewer). Archived boards / cards: every write `409` (read-only)
except restore.

| Action | Viewer (Leitão included) | Worker | Mod / Admin / Owner |
| --- | --- | --- | --- |
| Boards list, board, columns, card faces, card details, discussion, history | ✓ | ✓ | ✓ (every board) |
| Download board files and the card's files / open its links | ✓ | ✓ | ✓ |
| Create reminders (stored only) | ✓ | ✓ | ✓ |
| Edit the card's title, details, priority, start, deadline (version-checked, `409` when stale) | - | ✓ | ✓ |
| Change the status (= move to the first column with that state) | - | ✓ | ✓ |
| Move the card to another column / position of the board | - | ✓ (own cards) | ✓ |
| Checklist: add, rename, remove, tick (one item at a time; the item's text must match) | - | ✓ | ✓ |
| Comment, with @mentions of board viewers | - | ✓ | ✓ |
| Edit a comment | - | own only, while still a worker | own only |
| Delete a comment | own only | own only | **any** (moderation, one history line) |
| Attach a file or a link | - | ✓ | ✓ |
| Delete an attachment | - | own (uploader), while still a worker | **any** |
| Change the owner, add / remove collaborators | - | - (another owner id: `403`) | ✓ (board viewers only) |
| Labels, links to other cards, duplicate, archive / restore, delete the card | - | - | ✓ |
| Columns: create, name and state, reorder, delete (cards move to a chosen column) | - | - | ✓ |
| Boards: create (blank or template), edit, access, duplicate, finish, archive / restore, delete; board files upload / delete | - | - | ✓ |

## Copies and templates

- **Duplicate a card** (managers): title "(cópia)", details, labels, priority, dates and the checklist, all unticked.
  Never the owner, collaborators, comments, history, attachments, reminders or card links.
- **Duplicate a board** (managers): its columns and active cards (as above), its visibility, access list and
  responsible member - a copy is never wider than its source. No board files, no archived cards.
- **Templates** (managers): previewed with real dates and editable before anything is created; nothing is assigned or
  notified.

Notes:

- **Nobody edits another author's comment**; a manager may only delete it. A deleted account leaves its comments,
  attachments and history lines with no author ("Membro removido"), and a board without a responsible member.
- **Member ids** reach managers only (they change assignments and access); everyone else gets names and avatars.
- **Attachment and file ids** are checked against the card / board in the URL; a file id is never a storage key.
- Mention notifications, reminders and "As minhas tarefas" are phase 4 (#249): nothing is sent yet.

Menu: `MemberMenuAccess.Logistics` is true for every signed-in member, so a Leitão's **Gestão** shows **Logística only**
(`management` and `treasury` stay false for a Leitão; Reuniões, Documentação, Questões, Tesouraria and Pedidos keep their
own page / API rules, unchanged).
