# Permissions: /requests and /questions

Source: `RequestsAuthorization, QuestionsAuthorization`. Page doc: [react-requests-questions](../react-requests-questions.md).

| Who | What |
| --- | --- |
| /requests: Visitor | `401` |
| /requests: Leitão | `403`, unless Admin or Owner |
| /requests: Member, Mod | every request, contact details included |
| /requests: Admin, Owner | also approve, reject, delete |
| /questions: Visitor | `401` |
| /questions: any member (Leitão included) | read every question; ask one to a holder of an Órgãos Sociais position |
| /questions: recipient and author | replies alternate: recipient first, then author, and so on; nobody replies to a closed question |
| /questions: the author only | close, delete (soft), remind while it is the recipient's turn |

No role (Admin, Owner) overrides the questions rules. The menu shows "Pedidos" to the Admin role only.
