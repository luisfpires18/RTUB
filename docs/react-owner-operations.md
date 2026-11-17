# React Owner tools

`/emails`, `/notifications`, `/users`, `/owner/tracing`, `/owner/db` and `/owner/dashboard` are React pages over one
Owner-only API, `/api/owner/*`.

Code: `src/RTUB.Web/Endpoints/OwnerEndpoints.cs` (thin, with the Owner gate),
`src/RTUB.Application/Services/OwnerOperationsService*.cs` (the work; `.Dashboard.cs` for `/owner/dashboard`),
`src/RTUB.Application/DTOs/OwnerOperationsDtos.cs`, `portal/src/Owner*.tsx` + `ownerApi.ts` + `OwnerUi.tsx`.
Tests: `RTUB.Integration.Tests/Api/OwnerOperationsApiTests.cs` (access, behavior, dashboard),
`RTUB.Web.Tests/ReactPortal/OwnerOperationsSourceTests.cs`, `RTUB.Web.Tests/Services/SqlValidationServiceTests.cs`.

## Permissions

All six pages and the whole API are **Owner only**; Admin has no access.

| Who | Pages | API |
| --- | --- | --- |
| Visitor | 302 to `/login?returnUrl=…` | 401 |
| Leitão / Caloiro / Tuno / Veterano, Mod, Admin | the shell with status 403 and the React refusal ("Acesso Restrito") | 403 |
| Owner | all | all |

The gate is `OwnerEndpoints.RequireOwner`, an endpoint filter on the whole `/api/owner` group. It checks the role before
the antiforgery check, so a non-Owner gets 403 and nothing is read or written. Every GET is `Cache-Control: no-store`;
every write needs `X-CSRF-TOKEN` (missing: 400). The page routes (`Program.cs`) serve the shell. The menu
(`MemberMenuAccess.Owner`) only hides links; the API enforces access. An Owner also holds Admin (`/users` adds Admin
when it grants Owner).

## /emails

- Form: title (≤200, subject sent as `[RTUB] {title}`) and content (≤5000), both required, lengths enforced on the
  server. Preview shows a mock template (greeting, body, "Saudações RTUBianas."). "Enviar" asks for confirmation.
- Recipients: accounts with `Subscribed && EmailConfirmed && Email`, recomputed from the database at send time; a
  duplicate address is sent once. Two lists ("Destinatários", "Quem não irá receber") with search (nickname, first name,
  last name, email), select-all over the search, bulk and per-row toggle (`PUT /api/owner/emails/recipients`), 25 per page
  (10/25/50/100).
- Sender: `EmailNotificationService.SendAnnouncementEmailAsync`, with its one-per-minute rate limit and failure message.
  No audit row. Sending is synchronous: the request returns when the batch is done (no live progress).

## /notifications

- Form: title (≤100) and message (≤500) with a preview. Recipients are accounts with a push subscription
  (`GetSubscribedUserIdsAsync`), all selected at first, with search, select-all and 25 per page. The server validates
  the fields and drops any id without a subscription.
- Send, behind a confirmation dialog, via `SendToSelectedUsersAsync` (icon `/icons/rtub-logo-192.png`, the site URL,
  `Tag = announcement-{ticks}`). Writes an audit row (`EntityType=Notification`, `Action=PushNotificationSent`,
  `{RecipientCount, NotificationBody}`, title as display name); an audit failure is only logged.
- Members' own push settings are separate ([react-push-v2](react-push-v2.md)).

## /users

- A card per account: avatar, online status (signed in within the last hour), nickname, full name, role, locked badge,
  username, email, last login. Order: online first, then latest login, then first and last name. Search covers id, first
  name, last name, nickname, username, email; done on the server; paging 20/40/60/80/100. `?staff=true` ("Só Mod, Admin e
  Owner") lists only Owner / Admin / Mod accounts.
- Details: id, email, last login, email subscription, locked, "must change password", lockout end; **Desbloquear**
  clears the lockout and the failed-attempt count.
- Role edit (Member / Mod / Admin / Owner) asks for confirmation: removes every role, adds the new one (plus Admin for
  Owner), updates the security stamp. Unknown role 400, unknown account 404.
- "Sincronizar Estado da Tuna" calls `MemberStatusService.ReconcileAllMemberStatusesAsync`.

## /owner/tracing

- Filters: user, excluded user, entity type, action, "critical only", search, **Esconder sistema**. Typeahead member
  fields (`datalist`; "Excluir" applies once the name is an exact username), quick toggles, removable chips. Paging
  20/40/60/80/100. Stats: total, critical on the current page, entity types, users.
- **Search** matches the stored changes **or the stored name**, together with the other filters; the total is exact
  ((page − 1) × size + count, plus 1 when the page is full). A name resolved at read time is not searchable.
- Cards: name (or "{type} #id"), Portuguese type (`ENTITY_LABELS` in `OwnerTracing.tsx`, presentation only; the type
  filter shows "Label (RawType)" and submits the raw type), action, "Crítica" (accent border), who, `dd/MM/yyyy HH:mm`,
  Detalhes (pretty-printed JSON, stored and current name) and delete (confirmed).
- Truncate (all logs, or one user's) needs `DELETE ALL`, **checked on the server**. Export is
  `GET /api/owner/tracing/export` (filtered list, without the search; JSON download). "Anúncio de sistema" (≤500) goes
  through `AnnouncementService`; the React shell shows it via `GET /api/announcement`
  ([react-portal-pilot](react-portal-pilot.md) → *System announcement*).
- **System actions**: audit entries the app wrote itself (daily `MemberActivityStatus` reconciliation, `MemberStatus`,
  any entry with `UserName` null or `System`: jobs, cleanups, geocoding). `AuditLogService.IsMemberAction` is the one
  rule. `?system=hide` (also applied to the export) hides them; the dashboard counts members' actions only.
- Messaging stays excluded (`AuditConfiguration.ExcludedEntityTypes`): never listed, searched, filtered or exported.

### Audit names and redaction

- Names are written at save time (`ApplicationDbContext.EntityDisplay.cs`): every audited type is named after the person
  or object ("Zé · iPhone" for a push subscription, "Zé - Serenata - Vai" for an enrollment, "Pedido · Serenata ·
  01/12/2026" for a request; the outside person's name and contacts stay in the changes). `LoadAuditNamesAsync` loads
  what the change tracker lacks in at most one batched query per table, none when nothing is needed, so a deleted row is
  named while it still exists.
- Push keys `Endpoint`, `Auth`, `P256dh` are written as `[redacted]` (`AuditConfiguration.RedactedProperties`). Older
  stored rows still hold them, but the list, Details and export show `[redacted]` too (`AuditLogAppender.RedactStored`,
  read time only; the key stays so the Owner sees it was set or changed). Stored rows are never rewritten. The log leaves
  out password hashes and stamps. Search still runs over the stored text (Owner only; a match never shows the value).
- Old entries are never rewritten. The page adds `currentDisplayName` at read time for entries whose stored name is
  missing or carries member ids (ids become nicknames, one query per page; a few simple types get the entity's current
  name, one query per type). Shown as "nome atual"; the export is the stored log.

### Logins dialog

`GET /api/owner/tracing/logins?from=&to=` (`yyyy-MM-dd` Portugal days, both included) → `{records, historySince}`: one
line per member with a `LoginEvents` row in the range (sign-ins, active Portugal days, first → last time, "Ver sessões":
sign-in times, newest first, ≤50). A member with no row in the range but a `LastLoginDate` in it is listed as "Só o
último acesso". The dialog says so when the range starts before the first `LoginEvents` row. Search, 12/24/36/48/60 per
page.

## /owner/db

- Table list (a select on phones), browsed page by page (10/25/50/100, 50 default). Cells: `[NULL]`,
  `[Binary: n bytes]`, dates as `yyyy-MM-dd HH:mm:ss`, text cut at 100 characters.
- SELECT tab: `SqlValidationService.ValidateQuery` allows SELECT only and rejects any write or DDL keyword. Results are
  paged in the browser.
- INSERT / UPDATE / DELETE tab: `ValidateModifyQuery` allows DML only and rejects DDL and SELECT. Both rules are applied
  **on the server**. A change needs a confirmation dialog showing the query, and the server refuses it without
  `confirmed: true`. A failing query returns the database's error as a 400 message.

## Dashboard (`/owner/dashboard`)

"Estatísticas" in the Owner menu. Read only, `no-store`.

- Tiles (one per series; click to chart, with the change against the same number of days just before): sign-ins, active
  members (distinct per day; distinct over the range for the tile), movements (audit rows except `Played`), uploads
  (`GalleryMedia`, `NaipeContents`, `PostMedia`, `MeetingAtaAttachments`, `CommentImages`), song plays
  (`SongPlayCounts`), naipe plays (`NaipePlayCounts`), enrollments (`Enrollments.EnrolledAt`).
- Now: accounts, signed in within the last hour (as `/users`), members with a push subscription.
- Busiest hours: `LoginEvents` by Portugal weekday × hour. Rankings (top 10): members by movements, and areas of the app
  (`OwnerOperationsService.Dashboard.cs` → `DashboardAreas`, entity type → Atuações / Ensaios / Logística / Tesouraria…;
  an unmapped type keeps its name). There are no song, naipe or action-type rankings; the play tiles stay.
- Every count is bucketed by **Portugal day** (`PortugalTime.FromUtc`); stored stamps stay UTC. Messaging is never
  counted. Bucketing is in memory over one narrow column per source.
- Charts (line, heatmap, bars) are plain SVG / CSS, one series at a time in `--purple-ink`, with a crosshair tooltip
  (pointer and arrow keys) and a "Ver como tabela" view.

### Páginas mais vistas

`PageViews` rows (`Day` Portugal day, `Path`, `Count`; unique `Day + Path`; migration `AddPageViews`, additive). Every
React page asks `GET /api/account/me` once per load, so `AccountController.Me` hands the request's Referer to
`PageViewCounter` (same-origin only; ids → `{id}`, no query, lower-case, ≤128 chars), which counts in memory;
`PageViewFlushService` upserts the buffer once a minute and on shutdown (a failed flush keeps the counts). Never a write
per request; no member, address or user agent. Signed-in members only. The buffer holds at most 300 distinct (day, path)
keys; a new path beyond that counts under `/outras`, so a client cannot grow memory or the table with made-up paths.
Labels (`PAGE_LABELS`) are the client's; an unknown route shows its path. Limits: a page restored from bfcache is not
re-counted; one app instance assumed (a scaled-out App Service would each count its own).

### `LoginEvents`

Migration `AddLoginEvents`, additive (an N-1 app ignores it): `UserId`, `At` (UTC), `IsSignIn`. Written with raw SQL,
never audited (`AuditConfiguration.ExcludedEntityTypes`), no foreign key (history outlives an account):
- `POST /auth/login` adds an `IsSignIn = 1` row after `LastLoginDate` (a failure is logged and never fails the sign-in);
- the cookie validation's throttled `LastLoginDate` write (once per 5 minutes per member) adds an `IsSignIn = 0` row only
  when the member has no row yet in this UTC hour (Portugal is a whole-hour offset, so that is a Portugal hour).

History starts at the deploy that added the table; before it only the latest `LastLoginDate` exists. Growth is about one
row per member per active hour; prune if it ever matters.

## API (`/api/owner`, Owner only)

| Method | Path | Notes |
| --- | --- | --- |
| GET | `/emails` | Subscribed / not subscribed accounts with a confirmed address |
| PUT | `/emails/recipients` | `{userIds[], subscribed}` |
| POST | `/emails` | `{title, content}` → `{success, sent, total, message}` |
| GET | `/notifications` | Accounts with a push subscription |
| POST | `/notifications` | `{title, body, userIds[]}` → `{recipients, sent, failed}`; audited |
| GET | `/users?q=&page=&pageSize=&staff=` | Cards; `staff=true`: Owner / Admin / Mod only |
| GET | `/users/{id}` | Details (no secrets) |
| PUT | `/users/{id}/role` | `{role}` (Member / Mod / Admin / Owner) |
| POST | `/users/{id}/unlock` | Clears the lockout and the failed-attempt count |
| POST | `/users/sync-status` | `{updated}` |
| GET | `/tracing/filters` | Users, entity types, actions |
| GET | `/tracing?user=&excludeUser=&entityType=&action=&critical=&q=&page=&pageSize=` | One page of the log |
| GET | `/tracing/export?…filters` | JSON file |
| GET | `/tracing/logins?from=&to=` | See Logins dialog |
| DELETE | `/tracing/{id}` | 204, or 404 |
| POST | `/tracing/truncate` | `{user?, confirmation: "DELETE ALL"}` |
| POST | `/tracing/announcement` | `{message}` (≤500) |
| GET | `/dashboard?from=&to=` | `yyyy-MM-dd` Portugal days, both included, ≤366; last 30 days when missing |
| GET | `/db/tables` | Table names (EF model) |
| GET | `/db/tables/{table}?page=&pageSize=` | One page as display strings; unknown table → 404 |
| POST | `/db/select` | `{query}`; SELECT only |
| POST | `/db/modify` | `{query, confirmed}`; INSERT / UPDATE / DELETE only |

## Follow-ups

- Role changes have no guard against the Owner demoting themselves or removing the last Owner.
- `/owner/db` shows every column, including `PasswordHash` and the security stamps in `AspNetUsers` (Owner only).
