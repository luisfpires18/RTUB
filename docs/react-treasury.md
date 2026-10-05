# React Tesouraria (`/treasury`, React track 024)

Tesouraria is React: `portal/src/Treasury*.tsx` over `/api/treasury` (`Endpoints/TreasuryEndpoints.cs` →
`ITreasuryService` for reports, activities and transactions, `ITreasuryRecordsService` for calotes, MBWay and Nerba).
It replaces the Blazor `Pages/Management/Finance.razor`, `Report.razor`, `MbwayTransfers.razor`, `Nerba.razor`,
`NerbaOrderDetail.razor` and `Pages/Public/Calotes.razor`. DEV only; **no schema change**.

| Route | Owner after 024 | Old route (302 here, GET/HEAD) |
| --- | --- | --- |
| `/treasury` | React: annual reports | `/finance` |
| `/treasury/reports/{id}` | React: one report (activities, transactions, receipts) | `/finance/report/{id}` |
| `/treasury/calotes` (`?fy=2025-2026`) | React: member debts | `/calotes` (query kept) |
| `/treasury/mbway` | React: MBWay transfers | `/mbway`, `/mbway/{reportId}` |
| `/treasury/nerba`, `/treasury/nerba/{eventId}` | React: Nerba orders per event | `/nerba`, `/nerba/{reportId}`, `/nerba/event/{id}` |

All are for signed-in members: visitors get a 302 to `/login?returnUrl=…`, the API answers `401`.
Links: the Blazor `MainLayout` "Gestão → Tesouraria" (now `/treasury`), the React `/profile` actions, the calote push
reminder (now `/treasury/calotes`).

## Audit (before 024)

| Old page | Lines | Auth on the page | What it did |
| --- | --- | --- | --- |
| `/finance` | 587 | `[Authorize]`; Caloiros (not Mod/Admin/Owner) sent to `/`; Leitões saw an empty list | report cards (title, year, income, expenses, balance, "Ano atual", Publicado / Rascunho), search (title, year), 3 per page, PDF download, create (year only), publish, delete (drafts) |
| `/finance/report/{id}` | 1519 | `[Authorize]` **only**: any signed-in account, Leitões and Caloiros included, could open it by URL | summary tiles, bank / cash edit, activities (accordion), transactions per activity (search, 10 per page), receipts, lock / unlock, transaction history |
| `/calotes` | 895 | **none**: visitors saw every member's debts, names, photos, descriptions and commitment dates | debts per member, total, fiscal-year filter, search; Mod/Admin add / edit / delete and set the commitment date |
| `/mbway`, `/mbway/{reportId}` | 559 | **none**: visitors saw names, amounts, **phone numbers** and descriptions | transfers (all years), totals per recipient, search, 12 per page; Mod/Admin add, **Owner** edit / delete |
| `/nerba`, `/nerba/{reportId}` | 248 | **none** | Nerba events (upcoming / past) with item count and total; Mod/Admin delete all orders of an event |
| `/nerba/event/{id}` | 590 | `[Authorize]` | order items per day (multi-day events), sortable table, search, totals; Mod/Admin add / edit / delete |
| `/share` | 95 | none | PWA Web Share Target ("Partilhar Conteúdo"): **not finance**, unchanged |

Every rule above lived only in the circuit (UI hiding); no service checked the caller.

- **Tables** (unchanged): `Reports` (title, `Year` = fiscal start year, summary, `PdfData` (unused by the pages),
  `IsPublished`, `PublishedAt`), `Activities` (report, name ≤ 200, description ≤ 1000, start / optional end date,
  `IsLocked`), `Transactions` (activity, date, description ≤ 500, category ≤ 100, amount > 0, type `Income` /
  `Expense`, `ReceiptUrl`, unused `UserId`), `MemberDebts` (user, `AmountOwed` > 0, description ≤ 500,
  `CompromisedUntil`, fiscal year), `MbwayTransfers` (date, amount > 0, `TransferTo` ≤ 200, `TransferFrom` ≤ 200,
  member, phone ≤ 20, description ≤ 500, fiscal year never set), `NerbaOrders` (event, item ≤ 200, type ≤ 100, stock,
  price per unit, optional day, report never set). Local data: 1 report (draft 2025-2026), 18 activities, 51
  transactions (none with a receipt), 18 debts of 7 members, 16 transfers, 12 Nerba orders.
- **Money model**: no "status" column anywhere. A report is *Rascunho* or *Publicado* (publish is one-way in the UI;
  publishing locks the report). An activity is open or *bloqueada* (no new transactions). A transaction is income or
  expense. A debt exists or is removed (paying = deleting it; no paid / forgiven / cancelled state). Payment method is
  implicit: bank, cash, MBWay (a separate register, **not counted** in report totals).
- **Totals**: income / expenses / balance = transactions of every activity whose name has no `CALOTES` or `BANCO`
  (`Activity.IsHiddenFromCalculations`; a `CAIXA` activity **is** counted). "Dinheiro no Banco" / "em Caixa" = the
  balance of the first activity whose name contains `BANCO` / `CAIXA`; "Dinheiro Total" = bank + cash; "Calotes" =
  the debts of the fiscal year whose start year is the report's year. The activity list hides names with `CALOTES`,
  `BANCO` or `CAIXA`. Editing bank / cash writes one "Saldo" transaction in the `DINHEIRO NO BANCO` / `DINHEIRO EM
  CAIXA` activity (created if missing; negative values become expenses; 0 removes it).
- **Amounts**: `decimal`, euros, shown with two decimals (`N2` / `F2`). No rounding anywhere.
- **Receipts**: image or PDF ≤ 10 MB (client-side check only), uploaded to R2 `receipts/{env}/{transactionId}_{ts}` as
  **public-read** objects; the transaction stores the public URL. Replacing or removing a receipt (or deleting the
  transaction) deletes the object; deleting an activity or a report does not (orphans, unchanged).
- **History**: "Histórico de Transações" (Admin) reads the `Transaction` audit log of the report's activities (who,
  when, created / modified / deleted; bank / cash rows only as "modified").
- **PDF**: QuestPDF summary + activities and transactions, for everyone who could open the page.
- **Notifications**: `CalotesNotificationBackgroundService` sends one daily push per member with debts in the current
  fiscal year, skipping debts whose commitment date is still in the future; the push opened `/calotes`. No email. No
  page sends anything.
- **Old tests**: bUnit `FinancePageTests`, `ReportPageTests`, `CalotesPageTests` and integration `FinancePagesTests`
  (pages only; retired with the pages). Service, entity and card tests stay.

## Scope

Everything moved: reports (list, create, publish, delete drafts, PDF), one report (totals, bank / cash, activities,
transactions, receipts, lock / unlock, history), calotes, MBWay and Nerba (events and items). **No bridge**: no
Blazor Tesouraria page is left. `/share` is not finance and stays as it is.

## Privacy and rules now (`TreasuryAuthorization`, server-side on every endpoint)

| Who | Reports, report, PDF, receipts | Calotes | MBWay, Nerba | Changes |
| --- | --- | --- | --- | --- |
| Visitor | `401` (pages: 302 to sign in) | `401` | `401` | `401` |
| Caloiro, Leitão (no role) | `403` | **own debts only** (`scope: "own"`) | `403` | `403` |
| Member (Tuno, Veterano, …) | read | every debt of the year | read | `403` |
| Mod | read | read + manage | read; MBWay add; Nerba manage | reports: only in the treasury team |
| Mod of the treasury team (1.º / 2.º Tesoureiro) | read + manage drafts, lock / unlock | as Mod | as Mod | + publish, delete drafts |
| Treasury team without a role | read | read | read | publish, delete drafts (as before) |
| Admin | read + manage drafts, lock / unlock, **history** | manage | MBWay add; Nerba manage | no publish (as before) |
| Owner (inherits Admin) | everything | manage | MBWay add, **edit, delete** (Owner only, as before) | everything |

- DTOs only, no entities. Members get names (nickname / full name) and avatars; **no e-mail, phone of a member or
  user id** except: member ids for Mod / Admin / Owner in calotes (to edit) and for the Owner in MBWay (to edit); the
  member pickers (managers only) search e-mail but never return it.
- Calotes descriptions and commitment dates stay visible to treasury members, as the old page showed them (to
  everyone). MBWay phone numbers and descriptions are part of the register and stay visible to treasury members (they
  were public). There are no internal notes anywhere in the module.
- Every write needs the antiforgery token (`X-CSRF-TOKEN`); GETs are `no-store`.

## Record rules

- **Reports**: fiscal start year between 1991 and the current one, one per year; title `Relatório de Contas Y - Y+1`.
  Publish is one-way (`409` if already published). Only drafts are deleted (`409` otherwise).
- **Published report**: read-only (`409`), except lock / unlock (as before).
- **Activity**: name ≤ 200, start date, optional end ≥ start, description ≤ 1000. Locked: no new transaction (`409`);
  edit and delete of its transactions still allowed (as before).
- **Transaction**: date, description ≤ 500, category ≤ 100, amount > 0 with at most 2 decimals, `Income` / `Expense`;
  receipt image or PDF ≤ 10 MB (now checked on the server). Edit can replace or remove the receipt.
- **Bank / cash**: any value with at most 2 decimals; writes the old "Saldo" transaction (`FinanceManagementService`).
- **Calote**: member exists and is not a Leitão, amount > 0 (2 decimals), description ≤ 500; a new debt inherits the
  member's commitment; the commitment applies to all the member's debts of the year. Paying = removing the debt (no
  paid / forgiven / cancelled state exists).
- **MBWay**: date, amount > 0 (2 decimals), recipient member (required, as the old service), sender ≤ 200, phone ≤ 20,
  description ≤ 500; `TransferTo` = the member's nickname (or username), as the old picker set it.
- **Nerba**: only events of type Nerba; item ≤ 200, type ≤ 100, quantity ≥ 1, unit price ≥ 0 (2 decimals); a
  multi-day event files each item under one of its days (first by default); total = quantity × price.
- Money is shown as `1234,56 €` (pt-PT, two decimals).

## API (`/api/treasury`; GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /` | `{ reports, canCreate, canPublish, availableYears }` |
| `POST /reports`, `POST /reports/{id}/publish`, `DELETE /reports/{id}` | `{ year }`; publish; delete a draft |
| `GET /reports/{id}`, `GET /reports/{id}/pdf`, `GET /reports/{id}/history?page=&pageSize=` | report, PDF file, history |
| `PUT /reports/{id}/balance` | `{ kind: "bank" \| "cash", value }` |
| `POST /reports/{id}/activities`, `PUT`/`DELETE /activities/{id}`, `POST /activities/{id}/lock` | activities, `{ locked }` |
| `POST /activities/{id}/transactions`, `PUT`/`DELETE /transactions/{id}` | multipart `date, description, category, amount, type, removeReceipt`, file `receipt` |
| `GET /calotes?fy=`, `POST /calotes`, `PUT`/`DELETE /calotes/{id}`, `PUT /calotes/commitment` | calotes |
| `GET /members?q=&forDebts=` | member picker (managers, ≤ 20) |
| `GET /mbway`, `POST /mbway`, `PUT`/`DELETE /mbway/{id}` | transfers |
| `GET /nerba`, `DELETE /nerba/{eventId}`, `GET /nerba/{eventId}`, `POST /nerba/{eventId}/orders`, `PUT`/`DELETE /nerba/orders/{id}` | Nerba |

Report, calote, MBWay and Nerba writes answer with the refreshed page data.

## Changed on purpose

- **Privacy**: calotes, MBWay and Nerba need sign-in (they were open to visitors); the report page refuses Caloiros and
  Leitões (any account could open it by URL); Caloiros and Leitões see only their own calotes (the push reminder sends
  them there).
- Owner manages calotes, MBWay adds and Nerba without the Admin role; Owner sees the history.
- **Bug fixed**: `Transactions.ActivityId` has no ON DELETE action, so deleting an activity (or a draft report) with
  transactions failed. It stays one "Eliminar" action: the activity (or the report and its activities) and its
  transactions are deleted in **one database transaction** (rows and their audit log commit or roll back together; a
  failure leaves every financial row in place). Their receipts are deleted from storage after the commit,
  best-effort (a storage error never fails the delete). No schema change.
- Amounts must have at most two decimals; receipts are type-checked on the server.
- MBWay: the "Membro da Tuna" toggle and free-text recipient are gone (the old service refused any transfer without a
  member, so free text never saved).
- Lists are no longer paged by number: reports all shown, transactions / transfers "Mostrar mais" by 10 / 12; Nerba
  shows every item with a day filter instead of hiding the table until a day is chosen.
- The push reminder opens `/treasury/calotes`.

## Storage and notifications

Receipts keep the old R2 path and **public-read** URLs (`IReceiptStorageService`): anyone holding a receipt URL can
open it, as before; the API hands URLs only to treasury members. Tests use `FakeReceiptStorage`; browser validation
never uploaded a receipt. The daily calote push (`CalotesNotificationBackgroundService`) is unchanged apart from its
URL; no endpoint sends e-mail or push (tests assert it with recording mocks and the reminder disabled).

## Follow-ups

- Receipts are public by URL; moving them to private objects with pre-signed links needs a storage change.
- `ReportPdfService` caches by `Report.UpdatedAt`, which transaction changes do not touch: a PDF can be up to an hour
  stale (unchanged).
- `ReportCard`, `TransactionCard`, `MbwayTransferCard`, `NerbaOrderCard` (Shared, + `ReportCardTests`) and
  `ITransactionFilterService` / `IDebtService` have no page caller.
- `Transactions` rows with no activity (14 locally) appear nowhere; decide whether to clean them up.
