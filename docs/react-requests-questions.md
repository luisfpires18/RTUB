# React Pedidos (`/requests`) and Perguntas (`/questions`)

Code: `portal/src/Requests.tsx`, `portal/src/Questions.tsx`; `/api/requests` (`Endpoints/RequestAdminEndpoints.cs` →
`IRequestAdminService`) and `/api/questions` (`Endpoints/QuestionEndpoints.cs` → `IQuestionBoardService`); rules in
`Helpers/RequestsAuthorization.cs` and `Helpers/QuestionsAuthorization.cs`, enforced in the two services. The public
form is the separate React `/request` (`POST /api/public/requests`).

Routes: both need a signed-in member (visitors: 302 to `/login?returnUrl=...`; the API answers 401). Member menu
(`MemberShell.tsx`): "Pedidos" for Admin, "Questões" for everyone but Leitões. Pushes: new request -> `/requests`;
question asked / answered / replied / reminded -> `/questions` (it opens the page, not the question).

## Pedidos

- List: every request, newest first. Search over name, email, event type, location, phone (case and accents ignored);
  fiscal year by creation date (current by default, "Todos os anos"); status Pendente / Aprovado / Rejeitado. Sections
  "Pedidos Pendentes" (Pending and Analysing) and "Pedidos Respondidos" (Confirmed, Rejected); "Mostrar mais", 8 at a time.
- Details: name, email, phone, event type, preferred date (with the end of a range), location, submitted at, message.
- Approve then offers "Criar Evento?", which opens the agenda's create form prefilled by `RequestToEventService`. Reject.
  Delete is hard. Approve / reject refuse a request that is already answered.
- A new request pushes to Admins and Owners (`RequestService`). Approve, reject and delete notify nobody.

## Perguntas

- Open and closed lists, 10 per page, by latest activity; search over title, content and both members' names; filter by
  recipient. Anterior / Seguinte pager.
- Ask: pick a member holding an Órgãos Sociais position (one entry per position; "Direção - X" for the Direção, the bare
  position elsewhere); title 1-100, text at least 10 characters; push to the recipient. The service checks the recipient
  really holds the chosen position.
- Replies alternate: the recipient answers (Answered), then only the author (In discussion), and so on; push to the other
  side each time. Text is trimmed and capped at 5000. Closed questions refuse replies.
- Close, delete (soft) and remind (push to the recipient, only on the recipient's turn): the author only.

## Permissions

| Who | `/requests` read | approve / reject / delete | `/questions` read, ask | reply | close / delete / remind |
| --- | --- | --- | --- | --- | --- |
| Visitor | 401 | 401 | 401 | 401 | 401 |
| Leitão | 403 (unless Admin / Owner) | 403 | yes | on their turn | own questions |
| Caloiro, Tuno, other member, Mod | yes | 403 | yes | on their turn | own questions |
| Admin | yes | yes | yes | on their turn | own questions |
| Owner | yes | yes (inherits Admin) | yes | on their turn | own questions |

Members (not only Admin) read the requester's contact details. No role overrides the questions rules. The pending-actions
Questões panel (`/pending-actions`) lists the caller's own open questions; **Fechar questão** calls
`QuestionBoardService.CloseAsync` as the caller (author only), and **Abrir** deep-links to `/questions?question={id}`,
which opens that conversation once on load (the API decides access). See [react-pending-actions](react-pending-actions.md).

## API

GET answers are `no-store`; writes need `X-CSRF-TOKEN`.

| Endpoint | Does |
| --- | --- |
| `GET /api/requests?fiscalYear=&q=&status=` | `{ fiscalYears, fiscalYear, pending, answered, canManage }`; no `fiscalYear` = current, `""` = every year; status `pending`, `confirmed`, `rejected`; bad year / status -> 400 |
| `POST /api/requests/{id}/approve` | `{ request, createEventUrl }`; 400 when already answered, 404 when unknown |
| `POST /api/requests/{id}/reject` | the request; 400 when already answered |
| `DELETE /api/requests/{id}` | 204 (hard delete) |
| `GET /api/questions?closed=&q=&recipient=&page=&pageSize=` | `{ items, total, page, pageSize }`; 10 by default, at most 50 |
| `GET /api/questions/recipients` | every (member, Órgãos Sociais position) pair: `{ id, displayName, fullName, avatarUrl, position, role }` |
| `GET /api/questions/{id}` | `{ question, replies }`; 404 when deleted |
| `POST /api/questions` | `{ title, content, recipientId, position }` -> 201 detail; 400 per field |
| `POST /api/questions/{id}/replies` | `{ content }` (1-5000) -> detail; 403 off turn, 400 when closed |
| `POST /api/questions/{id}/close` | detail (closing twice is harmless); author only |
| `POST /api/questions/{id}/remind` | 204; author only, while it is the recipient's turn |
| `DELETE /api/questions/{id}` | 204 (soft); author only |

Questions carry, for the caller, `isMine`, `canReply`, `canRemind`. People are name and avatar only (no contact field);
requests carry the requester's own contact details.

## Follow-ups

- Decide whether members (not only Admin) should keep reading requesters' email and phone.
- `IRequestService.GetPendingRequestsAsync` has no caller besides tests.
