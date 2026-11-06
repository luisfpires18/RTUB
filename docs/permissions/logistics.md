# Permissions: /logistics

Source: `LogisticsAuthorization` (+ the assignment check in `LogisticsKanbanService.ChangeCardAsync`). Page doc:
[react-logistics](../react-logistics.md). Rules since task 065 (issue #246, Logística 1/5).

Four separate things decide what a person may do here; keep them apart:

| Axis | What it is | Effect in Logística |
| --- | --- | --- |
| **System role** | Identity role: Member, Mod, Admin, Owner | Mod, Admin, Owner **manage** (Owner inherits Admin) |
| **Tuna category** | Leitão, Caloiro, Tuno, … | **No effect** since 065: a Leitão reads like any member. (Expelled accounts never get a session.) |
| **Assignment** | the card's "Atribuir a" member or one of its "Membros atribuídos" | that card only: tick checklist items, change the status |
| **Board visibility** | private boards, explicit board access | **not implemented yet** (phase 3, #248): every board is visible to every signed-in member |

| Who | What |
| --- | --- |
| Visitor | `401` (pages: 302 to `/login`) |
| Any signed-in member, Leitão included | every board, list, card face and **card details** (checklist, links, people - names and avatars, no ids); board file download; create reminders |
| Assigned member (on that card) | the above + `POST /cards/{id}/checklist/{index}` (tick / untick one item, text must match) and `POST /cards/{id}/status`; checked against the database on every write, so a removed assignment refuses the next write |
| Mod, Admin, Owner | everything else: boards, lists, cards (create, edit, delete, move), labels, checklist text (`PUT /checklist`), links, assignments, files upload / delete |

An assignee may **not**: replace, reorder, add or delete checklist items, edit title / details / dates, labels, links,
assignments, move or delete the card, or do anything on a card not assigned to them (another card of the same board,
another board): `403`.

Menu: `MemberMenuAccess.Logistics` is true for every signed-in member, so a Leitão's **Gestão** shows **Logística only**
(`management` and `treasury` stay false for a Leitão; Reuniões, Documentação, Questões, Tesouraria and Pedidos keep their
own page / API rules, unchanged).
