# React Tesouraria (`/treasury`)

`portal/src/Treasury*.tsx` over `/api/treasury`.
Code: `Endpoints/TreasuryEndpoints.cs` -> `ITreasuryService` (reports, activities, transactions) and
`ITreasuryRecordsService` (calotes, MBWay, Nerba); `TreasuryAuthorization`, `FinanceManagementService` (bank/cash),
`ReportPdfService`, `IReceiptStorageService`, `CalotesNotificationBackgroundService`.
Tests: `TreasuryApiTests`-style integration tests with `FakeReceiptStorage` (no receipt reaches R2).

| Route | Old URL (302 here, GET/HEAD) |
| --- | --- |
| `/treasury`: annual reports | `/finance` |
| `/treasury/reports/{id}`: one report (activities, transactions, receipts) | `/finance/report/{id}` |
| `/treasury/calotes` (`?fy=2025-2026`): member debts | `/calotes` (query kept) |
| `/treasury/mbway`: MBWay transfers | `/mbway`, `/mbway/{reportId}` |
| `/treasury/nerba`, `/treasury/nerba/{eventId}`: Nerba orders per event | `/nerba`, `/nerba/{reportId}`, `/nerba/event/{id}` |

All need sign-in: visitors get a 302 to `/login?returnUrl=...`, the API answers `401`. Links: Gestão -> Tesouraria,
the `/profile` actions, the calote push reminder (`/treasury/calotes`).

## Permissions

Server-side on every endpoint in `TreasuryAuthorization`. "Treasury team" = 1.º or 2.º Tesoureiro (a position, not a
role).

| Who | Reports, report, PDF, receipts | Calotes | MBWay, Nerba | Changes |
| --- | --- | --- | --- | --- |
| Visitor | `401` | `401` | `401` | `401` |
| Caloiro, Leitão (no role) | `403` | own debts only (`scope: "own"`) | `403` | `403` |
| Member (Tuno, Veterano, ...) | read | every debt of the year | read | `403`; publish/delete drafts only if in the treasury team |
| Mod | read | read + manage | read; MBWay add; Nerba manage | reports/activities/transactions/bank-cash only if in the treasury team |
| Mod in the treasury team | read + manage drafts, lock/unlock | as Mod | as Mod | + publish, delete drafts |
| Admin | read + manage drafts, lock/unlock, history | manage | MBWay add; Nerba manage | no publish unless in the treasury team |
| Owner (inherits Admin) | everything | manage | MBWay add, edit, delete (Owner only) | everything |

- DTOs only, no entities. Members get names (nickname/full name) and avatars; no e-mail, member phone or user id except
  member ids for Mod/Admin/Owner in calotes (to edit) and for the Owner in MBWay (to edit). Member pickers (managers
  only) search e-mail but never return it.
- Calote descriptions and commitment dates, and MBWay phone numbers and descriptions, are visible to treasury members
  (the register's content). No internal notes anywhere in the module.
- Every write needs the antiforgery token (`X-CSRF-TOKEN`); GETs are `no-store`.

## Data and money model

- Tables: `Reports` (title, `Year` = fiscal start year, summary, `PdfData` unused, `IsPublished`, `PublishedAt`),
  `Activities` (report, name <= 200, description <= 1000, start/optional end date, `IsLocked`, optional `EventId`),
  `Transactions` (activity, date, description <= 500, category <= 100, amount > 0, `Income`/`Expense`, `ReceiptUrl`,
  unused `UserId`), `MemberDebts` (user, `AmountOwed` > 0, description <= 500, `CompromisedUntil`, fiscal year),
  `MbwayTransfers` (date, amount > 0, `TransferTo` <= 200, `TransferFrom` <= 200, member, phone <= 20, description <=
  500, fiscal year never set), `NerbaOrders` (event, item <= 200, type <= 100, stock, price per unit, optional day,
  report never set). `Transactions` rows with no activity appear nowhere.
- No status column. A report is Rascunho or Publicado (one-way; publishing locks it). An activity is open or bloqueada
  (no new transactions). A debt exists or is removed: paying = deleting it. MBWay is a separate register, not counted in
  report totals.
- Totals: income/expenses/balance = transactions of every activity whose name has no `CALOTES` or `BANCO`
  (`Activity.IsHiddenFromCalculations`; a `CAIXA` activity is counted). "Dinheiro no Banco"/"em Caixa" = balance of the
  first activity whose name contains `BANCO`/`CAIXA`; "Dinheiro Total" = bank + cash; "Calotes" = the debts of the
  fiscal year starting in the report's year. The activity list hides names with `CALOTES`, `BANCO`, `CAIXA`. Editing
  bank/cash writes one "Saldo" transaction in `DINHEIRO NO BANCO`/`DINHEIRO EM CAIXA` (created if missing; negative
  values become expenses; 0 removes it).
- Amounts are `decimal` euros, at most 2 decimals, no rounding; shown as `1234,56 €` (pt-PT).
- History ("Histórico de Transações", Admin/Owner) reads the `Transaction` audit log of the report's activities (bank/cash
  rows only as "modified").
- PDF: QuestPDF summary + activities and transactions. `ReportPdfService` caches by `Report.UpdatedAt`, which
  transaction changes do not touch, so a PDF can be up to an hour stale.
- Deleting an activity (or a draft report) with transactions: `Transactions.ActivityId` has no ON DELETE action, so the
  service deletes the activity (or report and activities) and its transactions in one database transaction (rows and
  audit log commit or roll back together). Their receipts are deleted from storage after the commit, best effort.

## Record rules

- Reports: fiscal start year between 1991 and the current one, one per year (`CreateReportAsync` checks and creates under
  one lock, so parallel clicks cannot duplicate); title `Relatório de Contas Y - Y+1`. Publish is one-way (`409` if
  already published). Only drafts are deleted (`409` otherwise). A published report is read-only (`409`) except
  lock/unlock.
- Activity: name <= 200, start date, optional end >= start, description <= 1000. Locked: no new transaction (`409`);
  edit/delete of its transactions still allowed. May name the event it accounts for ("Atuação": not cancelled events
  starting in the report's fiscal year; "Nenhuma" unlinks); the row shows the event's name as a tag.
- Transaction: date, description <= 500, category <= 100, amount > 0 with <= 2 decimals, `Income`/`Expense`. Receipt:
  image or PDF <= 10 MB, checked on the server; edit can replace or remove it.
- Bank/cash: any value with <= 2 decimals.
- Calote: member exists and is not a Leitão, amount > 0, description <= 500; a new debt inherits the member's
  commitment; the commitment applies to all the member's debts of the year.
- MBWay: date, amount > 0, recipient member required, sender <= 200, phone <= 20, description <= 500; `TransferTo` = the
  member's nickname (or username).
- Nerba: only events of type Nerba; item <= 200, type <= 100, quantity >= 1, unit price >= 0; a multi-day event files
  each item under one of its days (first by default); total = quantity x price.
- Lists: all reports shown; transactions/transfers "Mostrar mais" by 10/12; Nerba shows every item with a day filter.

## Receipts and notifications

Receipts go to R2 `receipts/{env}/{transactionId}_{ts}` as public-read objects (`IReceiptStorageService`); the
transaction stores the public URL, so anyone holding it can open it. The API hands URLs only to treasury members.
Replacing/removing a receipt, or deleting the transaction, deletes the object; deleting an activity or report does so
after commit (above).

`CalotesNotificationBackgroundService` sends one daily push per member with debts in the current fiscal year, skipping
debts whose commitment date is still in the future; it opens `/treasury/calotes`. No endpoint sends e-mail or push.

## API

Under `/api/treasury`. GET `no-store`; writes need `X-CSRF-TOKEN`. Report, calote, MBWay and Nerba writes answer with
the refreshed page data.

| Endpoint | Does |
| --- | --- |
| `GET /` | `{ reports, canCreate, canPublish, availableYears }` |
| `POST /reports`, `POST /reports/{id}/publish`, `DELETE /reports/{id}` | `{ year }`; publish; delete a draft |
| `GET /reports/{id}`, `GET /reports/{id}/pdf`, `GET /reports/{id}/history?page=&pageSize=` | report, PDF file, history |
| `PUT /reports/{id}/balance` | `{ kind: "bank" \| "cash", value }` |
| `POST /reports/{id}/activities`, `PUT`/`DELETE /activities/{id}`, `POST /activities/{id}/lock` | activities, `{ locked }` |
| `POST /activities/{id}/transactions`, `PUT`/`DELETE /transactions/{id}` | multipart `date, description, category, amount, type, removeReceipt`, file `receipt` |
| `GET /calotes?fy=`, `POST /calotes`, `PUT`/`DELETE /calotes/{id}`, `PUT /calotes/commitment` | calotes |
| `GET /members?q=&forDebts=` | member picker (managers, <= 20) |
| `GET /mbway`, `POST /mbway`, `PUT`/`DELETE /mbway/{id}` | transfers |
| `GET /nerba`, `DELETE /nerba/{eventId}`, `GET /nerba/{eventId}`, `POST /nerba/{eventId}/orders`, `PUT`/`DELETE /nerba/orders/{id}` | Nerba |

## As minhas ações pendentes

The current treasury team (position, not role) gets the Tesouraria panel on `/pending-actions`: create the year's report
(`CreateReportAsync`) and classify each begun event: an activity linked to it, or the shared "Sem movimentos"
(`Event.HasNoFinances`, set and reopened there; needs `CanManageReports`). The writes keep the rules above.
`/treasury/reports/{id}?activityForEvent={eventId}` opens "Nova atividade" prefilled with that event's name, dates and
link (managers, draft reports only); the query is removed from the address once read. Activity-event link: migration
`AddEventFinanceClassification`. See [react-pending-actions](react-pending-actions.md).

## Follow-ups

- Receipts are public by URL; private objects with pre-signed links need a storage change.
- Report PDF cache can be stale (see Data and money model).
