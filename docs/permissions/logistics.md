# Permissions: /logistics

Source: `LogisticsAuthorization` plus the card access check `LogisticsKanbanService.AccessAsync`
(`LogisticsKanbanService.CardWorkspace.cs`), the one place every card rule goes through. Page doc:
[react-logistics](../react-logistics.md). Rules as of task 066 (issue #247, Logística 2/5); task 065 (#246) opened
Logística to Leitões.

Four separate things decide what a person may do here; keep them apart:

| Axis | What it is | Effect in Logística |
| --- | --- | --- |
| **System role** | Identity role: Member, Mod, Admin, Owner | Mod, Admin, Owner **manage** (Owner inherits Admin) |
| **Tuna category** | Leitão, Caloiro, Tuno, … | **No effect** since 065: a Leitão reads like any member. (Expelled accounts never get a session.) |
| **Assignment** | the card's **owner** ("Responsável", the old "Atribuir a") and its **collaborators** (the "Membros atribuídos" rows) | that card only: work on it (below) |
| **Board visibility** | private boards, explicit board access | **not implemented yet** (phase 3, #248): every board is visible to every signed-in member; it will be added in `AccessAsync` |

Assignment is read from the database on every write, inside the same locked transaction as the write (065 review): a
removed owner or collaborator is refused on the next write.

## Action matrix

`✓` allowed, `-` refused (`403`; visitors `401`). "Worker" = owner or collaborator of **that** card.

| Action | Viewer (any member, Leitão included) | Worker | Mod / Admin / Owner |
| --- | --- | --- | --- |
| Boards, lists, card faces, card details, discussion, history | ✓ | ✓ | ✓ |
| Download board files and the card's files / open its links | ✓ | ✓ | ✓ |
| Create reminders (stored only) | ✓ | ✓ | ✓ |
| Edit the card's title, details, priority, start, deadline (version-checked, `409` when stale) | - | ✓ | ✓ |
| Change the status | - | ✓ | ✓ |
| Checklist: add, rename, remove, tick (one item at a time; the item's text must match) | - | ✓ | ✓ |
| Comment, with @mentions of members | - | ✓ | ✓ |
| Edit a comment | - | own only, while still a worker | own only |
| Delete a comment | own only (even after losing the assignment) | own only | **any** (moderation, one history line) |
| Attach a file or a link | - | ✓ | ✓ |
| Delete an attachment | - (not even one they uploaded while assigned) | own (uploader), while still a worker | **any** |
| Change the owner, add / remove collaborators | - | - (an owner id other than the current one is `403`) | ✓ |
| Labels, links to other cards, move, delete the card | - | - | ✓ |
| Boards and lists (create, edit, finish, delete), board files upload / delete | - | - | ✓ |

Notes:

- **Nobody edits another author's comment**; a manager may only delete it (moderation). A deleted account leaves its
  comments, attachments and history lines with no author ("Membro removido").
- **Mentions**: only members who can see the board (every non-expelled member today; the board's access list in
  phase 3), at most 20 per comment, fixed when the comment is written. Notifications for mentions come in phase 4
  (#249); nothing is sent yet.
- **Member ids** reach managers only (they change assignments); everyone else gets names and avatars.
- **Attachment ids** are checked against the card in the URL: an attachment of another card is `404`.

Menu: `MemberMenuAccess.Logistics` is true for every signed-in member, so a Leitão's **Gestão** shows **Logística only**
(`management` and `treasury` stay false for a Leitão; Reuniões, Documentação, Questões, Tesouraria and Pedidos keep their
own page / API rules, unchanged).
