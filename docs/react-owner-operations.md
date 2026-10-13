# React Owner tools (task 035)

`/emails`, `/notifications`, `/users`, `/owner/tracing` and `/owner/db` are React pages over one Owner-only API,
`/api/owner/*`. They were five Blazor pages. **All five are Owner only.** `/emails` and `/notifications` were Admin
pages, and the product decision for this task removed Admin access to them.

Code: `src/RTUB.Web/Endpoints/OwnerEndpoints.cs` (thin, with the Owner gate),
`src/RTUB.Application/Services/OwnerOperationsService.cs` (the work, over the services the old pages used),
`src/RTUB.Application/DTOs/OwnerOperationsDtos.cs`, and `portal/src/Owner*.tsx` + `ownerApi.ts` + `OwnerUi.tsx`.
There is no schema change and no migration.

## Permission matrix

| Who | Before (Blazor) | Now |
| --- | --- | --- |
| Visitor | 302 to sign in | Pages: 302 to `/login?returnUrl=…`. API: 401. |
| Leitão / Caloiro / Tuno / Veterano (Member role) | Refused on all five | Pages: 403 with the React refusal ("Acesso Restrito"). API: 403. |
| Mod | Refused on all five | 403 (page and API) |
| **Admin** | **`/emails` and `/notifications` allowed** (`[Authorize(Roles = "Admin")]`); the other three refused | **403 on all five** (page and API) |
| Owner | All five (an Owner also holds Admin, because `/users` adds Admin when it grants Owner) | All five |

The gate is `OwnerEndpoints.RequireOwner`, an endpoint filter on the whole `/api/owner` group. It checks the role and
runs before the antiforgery check, so a non-Owner gets 403 and nothing is read or written. Every GET is
`Cache-Control: no-store`, and every write needs `X-CSRF-TOKEN`. The page routes (`Program.cs`) serve the shell to
the Owner. Any other signed-in member gets the same shell with status 403, and the page shows the refusal. The menu
(`MemberMenuAccess.Owner`) and the Blazor navbar ("Operações", now `AuthorizeView Roles="Owner"`) only hide the links;
the API is what enforces access.

## Audit (old pages) → what the React page does

### /emails (was `Pages/Operations/Emails.razor`, Admin)
- **Old:** a title (≤200) and content (≤5000) form, with the subject sent as `[RTUB] {title}`. A preview modal showed a
  mock template (greeting, body, "Saudações RTUBianas."). "Enviar" went to every account with
  `Subscribed && EmailConfirmed && Email` and showed live per-recipient progress. "Limpar" cleared the form. The
  recipients list (`EmailRecipientsPreview`) had two sides, "Destinatários" and "Quem não irá receber", each with a
  search (nickname, first name, last name, email), select-all over the search, a bulk add or remove, and a per-row
  add or remove. Paging: 25 per page, with 10/25/50/100 offered. The only validation was client-side required fields;
  the length limits were HTML `maxlength` only. Side effect: `EmailNotificationService.SendAnnouncementEmailAsync`,
  which keeps its one-per-minute rate limit. The page wrote no audit row.
- **Now:** the same form, preview, recipients and toggles (`PUT /api/owner/emails/recipients` with one or many ids).
  "Enviar" asks for confirmation first. The server validates both fields as required and enforces the lengths. The
  recipients are recomputed from the database at send time, and a duplicate address is sent once. The same sender is
  used and its rate limit and failure message are kept. **Changed on purpose:** there is no live progress counter
  (the request returns when the batch is done), and nothing else changed.

### /notifications (was `Pages/Operations/Notifications.razor`, Admin)
- **Old:** a title (≤100) and message (≤500) form with a preview (logo, title, body, "Notificação Push RTUB"). The
  recipient list holds the accounts with a push subscription (`GetSubscribedUserIdsAsync`), all selected at first, with
  search, select-all over the search and 25 per page. The send goes through `SendToSelectedUsersAsync` with the icon
  `/icons/rtub-logo-192.png`, the site URL and `Tag = announcement-{ticks}`. The page then writes an audit row:
  `EntityType=Notification`, `Action=PushNotificationSent`, with `{RecipientCount, NotificationBody}` and the title as
  the display name; a failure here is only logged. After a send, the form is cleared and every account is selected
  again.
- **Now:** the same flow, behind a confirmation dialog. The server validates the fields and drops any id without a push
  subscription. It reuses the existing push sender and writes the same audit row. **Not in this task:** the members'
  own push settings (Push v2), and the `PushSubscriptions` table and push senders stay as they are.

### /users (was `Pages/Management/UserRoles.razor`, Owner)
- **Old:** a card for every account showing avatar, online status (signed in within the last hour), nickname, full
  name, role, a locked badge, username, email and last login. Online accounts come first, then the most recent login,
  then first and last name. Search covers id, first name, last name, nickname, username and email; paging is
  20/40/60/80/100. The details modal shows id, email, last login, email subscription, locked, "must change password"
  and the lockout end, with "Desbloquear" (clears the lockout and resets the failed-attempt count). The edit modal sets
  one role (Member / Mod / Admin / Owner): it removes every role, adds the new one, adds Admin as well for Owner, and
  updates the security stamp. Its "Guardar" saved straight away; the page's confirm dialog was never wired up. The
  "Sincronizar Estado da Tuna" button calls `MemberStatusService.ReconcileAllMemberStatusesAsync`.
- **Now:** the same features, with the search and paging done on the server. A role change now asks for
  **confirmation** (a new safeguard). An unknown role gets 400 and an unknown account 404.

### /owner/tracing (was `Pages/Operations/AuditLog.razor`, Owner)
- **Old:** filters for user, excluded user, entity type, action and "critical only". The search looks inside
  `Changes` only and **ignores the other filters**, and its total is an estimate. Stats: total records, critical count
  **on the current page**, number of entity types and number of users. The cards show the details (pretty-printed JSON)
  and a delete (with confirmation). Paging is 20/40/60/80/100. "Truncate" removes every log, or one user's, after
  typing `DELETE ALL`. "Export JSON" exports the filtered list, without the search. "Logins" shows who last signed in
  between two days, with search and 12/24/36/48/60 per page. The "Anúncio de sistema" (≤500) is broadcast through
  `AnnouncementService`.
- **Now:** all of the above. `DELETE ALL` is **checked on the server** (`POST /api/owner/tracing/truncate`); before, it
  was only checked in the browser. The export is `GET /api/owner/tracing/export`, a JSON file download. The search
  estimate is now right on later pages: it is (page − 1) × size + count, plus 1 when the page is full; before, it was
  wrong after page 1. Changes stored in the log are shown as they were recorded. The log already leaves out password
  hashes and stamps (`AuditLogAppender`).

### /owner/db (was `Pages/Operations/DatabaseViewer.razor`, Owner)
- **Old:** a list of tables (a select on phones), each browsed page by page (10/25/50/100, 50 by default). Cell
  display: `[NULL]`, `[Binary: n bytes]`, dates as `yyyy-MM-dd HH:mm:ss`, and text cut at 100 characters. The SELECT
  tab used `SqlValidationService.ValidateQuery`, which only allows SELECT and rejects any write or DDL keyword. The
  INSERT / UPDATE / DELETE tab used `ValidateModifyQuery`, which only allows DML and rejects DDL and SELECT; it showed a
  warning box but no confirmation. Errors were shown as the SQLite message.
- **Now:** the same, with both rules applied **on the server**. Before, the SELECT rule ran in the browser only and
  `ExecuteSelectQueryAsync` did not check it. A change now needs a **confirmation dialog** that shows the query, and
  the server refuses a change sent without `confirmed: true`. SELECT results are paged in the browser; before, every
  row was shown at once. A query that fails shows the database's error as a 400 message, as before.

## API (`/api/owner`, Owner only)

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/emails` | Subscribed / not subscribed accounts with a confirmed address |
| PUT | `/emails/recipients` | `{userIds[], subscribed}` |
| POST | `/emails` | `{title, content}` → `{success, sent, total, message}` |
| GET | `/notifications` | Accounts with a push subscription |
| POST | `/notifications` | `{title, body, userIds[]}` → `{recipients, sent, failed}`; audited |
| GET | `/users?q=&page=&pageSize=` | Cards, sorted as before |
| GET | `/users/{id}` | Details (no secrets) |
| PUT | `/users/{id}/role` | `{role}` (Member / Mod / Admin / Owner) |
| POST | `/users/{id}/unlock` | Clears the lockout and resets the failed-attempt count |
| POST | `/users/sync-status` | `{updated}` |
| GET | `/tracing/filters` | Users, entity types, actions |
| GET | `/tracing?user=&excludeUser=&entityType=&action=&critical=&q=&page=&pageSize=` | One page of the log |
| GET | `/tracing/export?…filters` | JSON file |
| GET | `/tracing/logins?from=&to=` | `yyyy-MM-dd`, both days included |
| DELETE | `/tracing/{id}` | 204, or 404 |
| POST | `/tracing/truncate` | `{user?, confirmation: "DELETE ALL"}` |
| POST | `/tracing/announcement` | `{message}` (≤500) |
| GET | `/db/tables` | Table names (EF model) |
| GET | `/db/tables/{table}?page=&pageSize=` | One page as display strings; unknown table → 404 |
| POST | `/db/select` | `{query}`; SELECT only |
| POST | `/db/modify` | `{query, confirmed}`; INSERT / UPDATE / DELETE only |

## Retired

- **Blazor pages:** `Emails.razor`, `Notifications.razor`, `AuditLog.razor`, `DatabaseViewer.razor` and
  `UserRoles.razor`, with their `.razor.css`.
- **Shared components:** `EmailRecipientsPreview` (+ css), `UserCard`, `TracingCard`, `LoginStatisticsButton` (+ css)
  and `SyncMemberStatusButton`.
- **Global CSS:** `3-components/user-card.css` and `tracing-card.css`.
- **Tests:** the bUnit and page tests (`EmailsTests`, `NotificationsPageTests`, `UserRolesPageTests`,
  `AuditLogPageTests`, `EmailRecipientsPreviewTests`). The SQL rules tests remain as
  `RTUB.Web.Tests/Services/SqlValidationServiceTests.cs`.
- **Kept**, because something else still uses them: `AnnouncementService` and `AnnouncementBanner` (037 retired the
  banner and made the service the store behind `GET /api/announcement`),
  `SqlValidationService`, `DatabaseViewerService`, `AuditLogService` and the push and email services.

## Tests

- `RTUB.Integration.Tests/Api/OwnerOperationsApiTests.cs`, through the real host:
  - **Access:** visitors get 401 on every call and the pages send them to sign in. Leitão, Caloiro, Tuno, Veterano, Mod,
    Admin and Admin+Veterano get 403 on every read and write, the 403 page shows the refusal, no data leaks and nothing
    changes. The Owner gets the pages and every read. Every GET is no-store, and a write without the antiforgery token
    gets 400.
  - **Behaviour:** emails (lists, toggles, validation, recipients sent to, the sender's refusal), notifications (only
    push subscribers, validation, the audit row), users (search, details, role changes including Owner+Admin, unlock,
    sync), tracing (filters, critical, search, export, delete / 404, truncate confirmation and truncating one user,
    logins, announcement) and db (tables, paging, unknown table, SELECT rule, failed query, the confirmation and the
    DML rule, an applied UPDATE).
- `RTUB.Web.Tests/ReactPortal/OwnerOperationsSourceTests.cs`: the retired files are gone, no Blazor `@page` owns the
  five routes, `main.tsx` and `Program.cs` map them, every route sits on the gated group, the navbar is Owner only with
  full navigations, and the pages contain no move wording and no removed module.
- Updated: `MemberShellTests` (the five links are now `portal.*`, `when: 'owner'`), `PortalRouteTests`,
  `OwnerPagesTests`, `LogisticsApiTests` (Admin and Mod refused `/emails`) and `RetiredImagesLabelsRoutesTests` (the
  menu is the Owner's).

## Deferred (recorded, not fixed)

- (resolved in 037) **The system announcement** reached only open Blazor pages (`AnnouncementBanner` in `MainLayout`),
  so nobody after 036. Since 037 the React shell polls `GET /api/announcement` and shows it (up to 30 s late, for 5
  minutes after the send): `react-portal-pilot.md` → *System announcement*. The Owner page and its API are unchanged.
- Role changes have no guard against the Owner demoting themselves or removing the last Owner (as before).
- `/owner/db` shows every column, including `PasswordHash` and the security stamps in `AspNetUsers` (as before; Owner
  only).
- Email sending is synchronous: a large batch keeps the request open until it ends (the old page streamed progress).
- No caller left: the Shared badges `LockStatusBadge`, `LoginStatusBadge`, `RoleBadge` (+ `RoleBadgeTests`,
  `wwwroot/js/roleBadge.js`) and `MemberCardLite`. Also the `.subscriber-list--h*` classes in
  `9-overrides/dynamic-style-classes.css` (asserted by `InlineStylePolicyTests`) and the 32px `.member-avatar-small`
  in `4-pages/meetings.css`.
- Comments in `TransactionService` still mention `AuditLog.razor`.
