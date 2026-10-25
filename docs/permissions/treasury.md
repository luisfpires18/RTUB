# Permissions: /treasury

Source: `TreasuryAuthorization`. Page doc: [react-treasury](../react-treasury.md).

| Who | Reports, receipts, calotes, MBWay, Nerba (read) | Own calotes | Calotes, Nerba, MBWay add | Reports, activities, transactions, bank/cash | Publish / delete drafts | Transaction history | MBWay edit, delete |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Visitor | `401` | - | - | - | - | - | - |
| Leitão, Caloiro | no | yes | no | no | no | no | no |
| Member (not Leitão/Caloiro) | yes | yes | no | no | only treasury team | no | no |
| Mod | yes | yes | yes | only if 1.º / 2.º Tesoureiro | only treasury team | no | no |
| Admin | yes | yes | yes | yes | only treasury team | yes | no |
| Owner | yes | yes | yes | yes | yes | yes | yes |

"Treasury team" = 1.º or 2.º Tesoureiro (a position, not a role). The nav group "Logística e Tesouraria" shows for
Admin, Owner, Mod or any Tuno.
