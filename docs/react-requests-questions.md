# React Pedidos (`/requests`) and Perguntas (`/questions`), task 031

Both are React (`portal/src/Requests.tsx`, `portal/src/Questions.tsx`) over `/api/requests`
(`Endpoints/RequestAdminEndpoints.cs` → `IRequestAdminService`) and `/api/questions` (`Endpoints/QuestionEndpoints.cs` →
`IQuestionBoardService`), replacing the Blazor `Pages/Management/Requests.razor` and `Questions.razor`. DEV only;
**no schema change**, no migration. The public form stays the React `/request` (`POST /api/public/requests`, task 003).

| Route | Owner after 031 |
| --- | --- |
| `/requests` | React, signed-in members (visitors: 302 to `/login?returnUrl=%2Frequests`); the API refuses Leitões |
| `/questions` | React, signed-in members (visitors: 302 to `/login?returnUrl=%2Fquestions`) |

Links: the member menu (`MemberShell.tsx`: Pedidos for Admin, Questões for everyone but Leitões, the old navbar's
rules), the Blazor `MainLayout` Gestão menu (now plain full-page links), the push notifications (new request →
`/requests`; question asked / answered / replied / reminded → `/questions`).

## Audit (old pages)

**`/requests`** (535 lines, `[Authorize]`):
- **Who**: any signed-in member; Leitões sent to `/` unless Admin or Owner. Approve, reject and delete: the `Admin`
  role only (the page's `IsOwner` flag was the Admin role). Members saw everything, contact details included.
- **List**: every request, newest first; search over name, email, event type, location, phone (case and accents
  ignored); fiscal year by creation date (current year by default, "Todos os anos"); status filter Pendente / Aprovado /
  Rejeitado; split into "Pedidos Pendentes" (Pending) and "Pedidos Respondidos" (Confirmed, Rejected), 4 a page each.
  `Analysing` (only set by seed data) appeared in neither list.
- **Details**: name, email, phone, event type, preferred date (the end date of a range was not shown), location,
  submitted at, message.
- **Actions**: approve (then "Criar Evento?" → the React agenda's create form prefilled by `RequestToEventService`),
  reject, delete (hard). Approve / reject were offered on pending cards only, but the service accepted any status.
- **Notifications**: none on approve / reject / delete. A new request pushes to Admins and Owners (`RequestService`,
  unchanged).

**`/questions`** (841 lines, `[Authorize]`):
- **Who**: any signed-in member, Leitões included (the navbar hid the link from Leitões). No role overrides.
- **Lists**: open and closed questions, 10 a page (server-side), by latest activity; search over title, content and both
  members' names; filter by recipient. The closed list only shows when not empty.
- **Ask**: pick a member holding an Órgãos Sociais position (one entry per position; "Direção - X" for the Direção,
  just the position elsewhere), title 1-100, question at least 10 characters; push to the recipient.
- **Replies**: turns — the recipient answers (Answered), then only the author (In discussion), and so on; push to the
  other side each time. Close, delete (soft) and "remind" (push to the recipient while it is their turn): the author
  only. The UI stopped replies on closed questions; the service did not.

## Rules now (server-side)

`RequestsAuthorization` and `QuestionsAuthorization` (Application/Helpers), enforced in the two services.

| Who | `/requests` read | approve / reject / delete | `/questions` read, ask | reply | close / delete / remind |
| --- | --- | --- | --- | --- | --- |
| Visitor | 401 | 401 | 401 | 401 | 401 |
| Leitão | 403 | 403 | yes | on their turn | own questions |
| Caloiro, Tuno, other member, Mod | yes | 403 | yes | on their turn | own questions |
| Admin | yes | yes | yes | on their turn | own questions |
| Owner | yes | yes (inherits Admin) | yes | on their turn | own questions |

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/requests?fiscalYear=&q=&status=` | `{ fiscalYears, fiscalYear, pending, answered, canManage }`; no `fiscalYear` = current, `""` = every year; status `pending`, `confirmed`, `rejected`; bad year / status → 400 |
| `POST /api/requests/{id}/approve` | `{ request, createEventUrl }`; 400 when already answered, 404 when unknown |
| `POST /api/requests/{id}/reject` | the request; 400 when already answered |
| `DELETE /api/requests/{id}` | 204 (hard delete) |
| `GET /api/questions?closed=&q=&recipient=&page=&pageSize=` | `{ items, total, page, pageSize }`; 10 by default, at most 50 |
| `GET /api/questions/recipients` | every (member, Órgãos Sociais position) pair: `{ id, displayName, fullName, avatarUrl, position, role }` |
| `GET /api/questions/{id}` | `{ question, replies }`; 404 when deleted |
| `POST /api/questions` | `{ title, content, recipientId, position }` → 201 detail; 400 per field |
| `POST /api/questions/{id}/replies` | `{ content }` (1-5000) → detail; 403 off turn, 400 when closed |
| `POST /api/questions/{id}/close` | detail (closing twice is harmless); author only |
| `POST /api/questions/{id}/remind` | 204; author only, while it is the recipient's turn |
| `DELETE /api/questions/{id}` | 204 (soft); author only |

Questions carry, for the caller, `isMine`, `canReply`, `canRemind`. People are name and avatar only (no contact
field); requests carry the requester's own contact details, as the old page did.

## Changed on purpose

- **Owner inherits Admin** on requests (approve / reject / delete), as everywhere else on the React track; before, an
  Owner without the Admin role could not.
- **Requests in analysis** are listed with the pending ones (they appeared nowhere before); approve / reject now refuse
  a request that is already answered (the old service accepted any status).
- **A question goes only to a member who holds the chosen position** (the old service trusted the form), replies to a
  closed question are refused (the old service accepted them), and question and reply text is capped at 5000
  characters (no cap before). Text is trimmed.
- Requests show the end date of a date range; lists use "Mostrar mais" (requests, 8 at a time) and Anterior / Seguinte
  (questions) instead of the old pagers.
- Wording: "Questões" in the member menu (the Blazor navbar keeps "Perguntas"); "Aprovado" for a confirmed request.

## Follow-ups

- Members (not only Admin) still read requesters' email and phone, as before; decide whether to narrow it.
- `IQuestionService` now has methods with no caller (`GetAllOrgaoSocialMembersAsync`, `HasOrgaoSocialPosition`,
  `GetMembersWithPositionAsync`, `CanAnswerAsync`), and `IRequestService.GetPendingRequestsAsync` too; kept, with tests.
- Global CSS left by the Blazor cards: `3-components/request-card.css`, `question-card.css` and `4-pages/questions.css`
  (`request-grid`, `filter-search-container` and `filter-dropdown-container` were also used by Meetings / Naipes; no
  markup user left since 034).
- A question notification opens `/questions`, not the question itself.
