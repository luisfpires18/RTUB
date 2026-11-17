# React: As minhas ações pendentes (`/pending-actions`)

A page of grouped actions with the buttons on each row. Not an inbox, feed, chat or wizard, and not statistics.
Nothing is copied into a generic "actions" table: every panel is a query over its module's own data, run on every
load. The only stored thing is a member's own dismissal of an optional action.

Code: `src/RTUB.Core/Entities/PendingActionDismissal.cs`, `Interfaces/IPendingActionsService.cs` (service +
`IPendingActionSource` contract), `Services/PendingActionsService.cs`, `Services/PendingActionSources.*.cs`,
`Endpoints/PendingActionsEndpoints.cs`, `portal/src/PendingActions.tsx`, `pendingActionsApi.ts`, `styles.css` (`.pa-*`).
A new panel = a new `IPendingActionSource` registered in `ServiceCollectionExtensions` (registration order = page order).
Tests: `PendingActionsServiceTests`, `PendingActionsApiTests`, `RehearsalAttendanceAdminWorkflowTests`
(`AddFirstAttendanceAsync`); `tests/responsive/responsive_audit.py` scenarios `pending-actions*` (the dialog scenario
opens the "Fechar questão" confirmation and never confirms: the audit must not write).

## Permissions

The page grants nothing. Every button opens, or calls, the module that owns the action, under that module's own rules.
Panels are decided per call from the database (categories, `IsRetired`, `IsExpelled`, roles, positions), never from
cookie claims, so a role, mandate or status change shows on the next load. A panel that does not apply to the caller is
`404` on its own URL and for its commands (Admin / Owner included when the panel follows a position).

"Active" = `MemberActivityStatusEvaluator.IsSubjectToRule(user) && !user.IsRetired`
([member-activity-status](member-activity-status.md)). The page never computes activity status itself.

| Who | Page | Panels |
| --- | --- | --- |
| Visitor | `/pending-actions` goes to sign in (`returnUrl` kept); API `401` | - |
| Expelled / unknown account | API `401` | - |
| Reformado | none: `GET /api/pending-actions` is `404`, the React page goes to `/events`, the menu hides the entry (`/api/account/me` → `retired`), `PendingActionsService.Applies` refuses every source, and the daily reminder never evaluates them, whatever roles or positions they hold | - |
| Active member | yes | Atuações, Ensaios, Logística, Galeria |
| Account with no membership category (e.g. bare Owner account) | yes | Logística |
| Direção in office (Magister, Vice-Magister, Secretário, 1.º / 2.º Tesoureiro), not Reformado | yes | + Questões |
| 1.º / 2.º Tesoureiro (current mandate) | yes | + Tesouraria |
| Secretário (current mandate) | yes | + Secretaria |
| Presidente da Mesa / Magister / Presidente do CV (current mandate) | yes | + Reuniões |
| Ensaiador / Magister (current mandate) | yes | + Presenças por aprovar, Repertório |
| Mod or Admin (not the Owner, even with Admin too), not Reformado | yes | + Membros |
| Direção in office and Mod / Admin / Owner, not Reformado | yes | + Novidades |

A system role adds no panel by itself: Tesouraria / Secretaria / Reuniões / Aprovações / Repertório follow the position.
Positions are `ApplicationUser.Positions` (the mandate in office, synced by `SyncCurrentMandateAsync`; the previous one
stays until the new year's first assignment); a mandate that ends removes the panel at once. Holding a Direção position
grants Admin (Mod for a Caloiro) on its own (`RoleManagementService`), so a treasurer passes
`TreasuryAuthorization.CanManageReports`; if that role was removed by hand the module refuses (`403`) and the page says so.

Commands run the module's service **as the member, with roles read from the database**
(`PendingActionMember.ToPrincipal()`), so the module's rules decide and a removed role or assignment is refused at once.
Assignment never opens a private board.

### Responses (`POST /api/pending-actions/responses`, antiforgery)

| Request | Answer |
| --- | --- |
| Vou / Não vou, Fui / Não fui on an activity pending for the caller now | `204`; own Enrollment / RehearsalAttendance through the module's service |
| Anything else (already answered, cancelled, before PastFrom, previous fiscal year, unknown id, wrong kind, Reformado / no category) | `404`, nothing written |
| Concurrent first answer (other tab, module page) | `409`, the first stays |
| Instrument not offered, note too long, bad answer | `400` |
| Command the row does not have (`yes` on a card) | `400` |
| Concluir a board with no done column | `400` ("Este quadro não tem uma coluna DONE."), card unchanged |

The page cannot approve anything: a past rehearsal "Fui" stays pending (`Attended = false`) until an Admin / Owner
confirms it on the rehearsal page. No permission was added to `EventsAuthorization` or `RehearsalsAuthorization`.

### Dismissals (`POST /api/pending-actions/dismissals`, antiforgery)

| Request | Answer |
| --- | --- |
| Optional (`dismissible`) row pending for the caller now | `204`; one `PendingActionDismissals` row |
| Required row or answer row (Atuações / Ensaios) | `403`, nothing stored |
| Not pending for the caller (unknown, someone else's, already dismissed, wrong panel) | `404`, nothing stored |
| No / bad `X-CSRF-TOKEN` | `400` |

Required / optional and the period belong to the source, not the request. A dismissal is personal and hides nothing for
anyone else; shared decisions (treasury "Sem movimentos") live in their module.

## Panels

| Panel | Category | Who | Rows / buttons |
| --- | --- | --- | --- |
| Atuações | participation | active | unanswered events: Próximas → **Vou / Não vou**; Passadas → **Fui / Não fui**; title opens `/events/{id}` |
| Ensaios | participation | active | unanswered rehearsals, same buttons; title opens `/rehearsals/{id}` |
| Logística | tasks | not Reformado | own open cards (owner or collaborator): **Concluir**, **Abrir** (`/logistics/{board}?card={id}`) |
| Questões | tasks | Direção in office | own open questions: **Fechar questão** (asks first), **Abrir** (`/questions?question={id}`) |
| Tesouraria | responsibilities | 1.º / 2.º Tesoureiro | **Criar relatório** (Obrigatório), then per begun event **Sim** / **Não**; "Sem movimentos" filter with **Reabrir** |
| Secretaria | responsibilities | Secretário | **Criar pastas** / **Agora não** |
| Reuniões | responsibilities | Presidente da Mesa / Magister / Presidente do CV | **Criar ata** (Obrigatório); monthly **Registar reunião** |
| Presenças por aprovar | responsibilities | Ensaiador / Magister | **Aprovar** / **Recusar** |
| Repertório | responsibilities | Ensaiador / Magister | **Preparar repertório** / **Não é necessário** |
| Membros | responsibilities | Mod / Admin | **Completar** (`/members?info={id}`) |
| Galeria | participation | active | **Adicionar** / **Não tenho** |
| Novidades | participation | Direção + Mod / Admin / Owner | weekly **Criar novidade** (`/news?new=1`) / **Agora não** |

A panel that does not apply is not shown. Reformados have no page at all (see Permissions).

## Page

- Shared shell header. Eyebrow "Área de membros", title "As minhas ações pendentes", lead, "No ativo" / "Reformado"
  badge and Portugal's date.
- Menu: first link of the **Membro** group (every signed-in member), not under Gestão.
- Two columns from 900 px, one below, in registration order. Category buttons (Tudo / Participação / Tarefas /
  Responsabilidades) appear only when panels span two or more categories.
- Each panel: title + pending count, sub-filters with counts (Próximas / Passadas), **three rows per page**, footer
  "1–3 de 7 · página 1 de 3". The row area keeps a three-row minimum height (empty, loading, error included); rows grow
  for wrapped text, never clipped.
- Row: title ("Obrigatório" when required; links to the item when it has a page), date / time / place, buttons.
  Required: one action, no dismiss. Optional: the action + "Agora não". Answer rows carry per-activity labels for screen
  readers ("Vou: Serenata…").
- Vou / Fui opens a small dialog (instrument + optional note) **only** with two or more instruments to choose from;
  otherwise it is sent at once with the primary (or none). Não vou / Não fui is sent at once. Cancel / Esc sends nothing.
  The row stays until the server answers; then the panel reloads the same page (an emptied last page shows the previous
  one), focus stays on the panel title, buttons are disabled while in flight.
- The page reloads on back navigation (bfcache) and on resume (`visibilitychange`), so an answer given in the module
  resolves the row.
- Offline / failure: the row stays with "Não foi possível guardar…". Stale click (answered elsewhere, cancelled, out of
  window) → `404` and "Esta atividade já tinha resposta ou mudou entretanto."
- A saved "Vou" under Próximas (Atuações, Ensaios) offers "Adicionar ao Google Calendar" once the module's own read
  confirms the member is going to an open, upcoming activity; "Fui" never does. A failed read skips the offer, never the
  answer. Rules: [react-events](react-events.md).

## API (`Endpoints/PendingActionsEndpoints.cs`, `no-store`)

| Call | Answer |
| --- | --- |
| `GET /api/pending-actions` | `{ today, fiscalYear, status: active\|retired\|none, panels: [first page of each panel that applies] }` |
| `GET /api/pending-actions/panels/{panel}?filter=&page=` | `{ key, title, category, filters[{key,label,count}], filter, page, pageSize: 3, total, rows }` |
| `POST /api/pending-actions/dismissals` `{ kind, sourceKey }` | see Dismissals |
| `POST /api/pending-actions/responses` `{ kind, sourceKey, answer: yes\|no\|done\|close, instrument?, notes? }` | see Responses |

Rows carry `kind`, `sourceKey`, `title`, `date`, `time`, `context`, `required`, `dismissible`, `href`,
`actions[{label, href, primary, answer}]`; the page also carries `instruments` / `defaultInstrument`. No user ids,
emails or other people's data. Page size is fixed at 3; an out-of-range page answers the last one; an unknown filter
answers the default.

Queries are bounded: counts are SQL `COUNT`, rows `ORDER BY … OFFSET/LIMIT 3`, "unanswered" is a `NOT EXISTS` on the
member's own Enrollment / RehearsalAttendance. No member list, file listing or history is loaded to render a panel.

## Eligibility: Atuações and Ensaios

| Panel | Source | Pending when |
| --- | --- | --- |
| Atuações | `Events` + the member's `Enrollments` | no Enrollment row of theirs (going or not), not cancelled; Próximas: last day (`EndDate ?? Date`) today or later; Passadas: last day before today and start on or after **PastFrom** |
| Ensaios | `Rehearsals` + the member's `RehearsalAttendances` | no attendance row of theirs, not cancelled; Próximas: day today or later; Passadas: day before today, on or after **PastFrom** |

- Fiscal year: 1 September - 31 August (`FiscalYearHelper`), by the activity's start date. No 30-day or 7-day cutoff.
- **PastFrom**: 1 September of the current fiscal year, or the first day of the member's admission month (earliest of
  Leitão / Caloiro / Tuno; month missing = January) when later. Nobody is asked about activities before they joined.
- Boundaries use the module's own rule on `PortugalTime` and the injected `TimeProvider`. A multi-day or later-today
  event is Próximas until its last day is over (midnight, Portugal); a rehearsal is Próximas all of its day. So a Vou on
  the page is accepted exactly when the module's own answer modal accepts it. Upcoming has no end date.
- Another member's answer never resolves mine; my answer in the module (or an Admin adding me) resolves it on next load.

## Answers

Each answer re-checks on the server that the activity is pending for the caller **now** (same query as the panel), then
writes through the module's domain service. The caller's id comes from the session only; `sourceKey` is just the
activity id, so another member's row cannot be addressed.

| Answer | Writes | Side effects |
| --- | --- | --- |
| Vou (upcoming event) | `IEnrollmentService.CreateEnrollmentAsync`: `WillAttend = true`, instrument (offered list; none = not playing), note, other instruments, `CategoryAtEvent` snapshot | push to the others going (the service's rule), as the event page |
| Não vou (upcoming event) | same, `WillAttend = false` + optional reason | the service's "não vai" push |
| Fui (past event) | same, `WillAttend = true`, **no push** (`skipNotification`; pushes are for future events only) | **counts at once** as a performance (the temporary participation source of [member-activity-status](member-activity-status.md), the same row an Admin adds in "Quem foi") and in leaderboard XP; status reconciled immediately (`ReconcileMemberStatusAsync`). Admin corrects it in "Quem foi" |
| Não fui (past event) | same, `WillAttend = false` | none; kept as the member's answer |
| Vou / Não vou (upcoming rehearsal) | `IRehearsalAttendanceService.AddFirstAttendanceAsync` (create-only): `WillAttend`, `Attended = false`, `CategoryAtRehearsal` | the service's push to others going |
| Fui (past rehearsal) | same, `WillAttend = true`, `Attended = false` (**pending**) | none until an Admin confirms on the rehearsal page (then XP, status and the member's push); never self-approved |
| Não fui (past rehearsal) | same, `WillAttend = false` | none |

- Create-only: an existing answer is never changed from here. A retry finds nothing pending (`404`); a race hits the
  unique `(EventId, UserId)` / `(RehearsalId, UserId)` index (`409`) or the service's existence check. Changing an
  answer is done on the module page.
- No event or rehearsal is created; a past answer only adds the member's own row.
- Module parity: `/events/{id}` shows "Disseste que vais" and "Quem vai"; `/rehearsals/{id}` shows the presença as
  pending and the Admin's approval queue includes it.

## Logística and Questões

Logística is for every signed-in member who is not Reformado (Leitões included; activity status plays no part). Questões
is the Direção's panel, but the rows are still the member's own open questions.

| Panel | Pending when | Command | Module call | Leaves when |
| --- | --- | --- | --- | --- |
| Logística | card not archived, not done, on a non-archived board the member sees (`LogisticsKanbanService.WorkableCards`, same as "As minhas tarefas"), and the member is owner or collaborator | Concluir (`done`) | `ILogisticsKanbanService.SetStatusAsync(id, Done)`: moves to the board's done column, history, version | done, unassigned, archived, board archived or made private without them |
| Questões | the member is the author, not closed, not deleted (`QuestionsAuthorization`: only the author closes) | Fechar questão (`close`), after confirmation | `IQuestionBoardService.CloseAsync`; thread and replies are kept | closed, deleted |

- Order: Logística by deadline (none last), then id; Questões answered ones first (the member's turn), then latest
  activity. No sub-filters. Context: board, status, priority (Alta / Urgente), checklist n/m, Responsável / Colaborador;
  recipient position and whether it is answered.
- Unowned cards are left out even for managers: they are not the member's work.
- Not dismissible, no snooze: real work. Opening is not completion; only the module's state removes the row.
- `/questions?question={id}` opens that conversation once; the API decides access.

## Tesouraria and Secretaria

Both follow the current position; writes follow the module's rules (see Permissions).

### Tesouraria (1.º / 2.º Tesoureiro, shared)

| Row | Pending when | Buttons | Resolves |
| --- | --- | --- | --- |
| Relatório de contas {ano}, **Obrigatório** | no `Reports` row with `Year` = the fiscal year's start | Criar relatório → `ITreasuryService.CreateReportAsync` (title "Relatório de Contas Y - Y+1") | a report of the year exists (made here or on `/treasury`). Not dismissible (`403`) |
| one per event (only once the report exists) | not cancelled, starts in the fiscal year and **has begun** (start before tomorrow), `Event.HasNoFinances` false, and no activity of this year's report with `Activity.EventId` = the event | **Sim** → `/treasury/reports/{id}?activityForEvent={event}` opens "Nova atividade" with the event's data (nothing saved until Guardar). **Não** (confirmed) → `HasNoFinances = true` | an activity linked to the event is saved, or "Não". Deleting or unlinking the activity brings the row back |
| "Sem movimentos" filter | `HasNoFinances` true for a begun event of the year | **Reabrir** → `HasNoFinances = false` | back under "Por classificar" |

- No transaction is created from the page. Association is the durable `Activity.EventId`, never a name match.
- Não / Reabrir have no module page; they require `TreasuryAuthorization.CanManageReports` (`403` otherwise) and are
  audited with the event. Não on an already classified, out-of-year, future or cancelled event is `404`.
- `TreasuryService.CreateReportAsync` checks and inserts under one process-wide lock (single App Service instance; a
  unique index on `Reports.Year` would be needed to scale out), so parallel clicks make one report; the others get
  `404` / `409` and the page refreshes.
- Order: events oldest first; "Sem movimentos" newest first. Future events are not asked yet.

### Secretaria (Secretário)

- Structure: the previous fiscal year's top-level Documentação folders as they are (the board files folder `Logistics`
  and its sub-folders left out). A first year asks nothing.
- Pending when one of those names (case-insensitive) is missing in the current year. One row previewing the names.
- **Criar pastas** (confirmed; nothing existing is altered) → `IDocumentationService.CreateFolderAsync` per missing
  name, as the member; it refuses an existing name, so a repeat is harmless. Folders are only created, never deleted.
- **Agora não**: the Secretário's own dismissal, period = fiscal year.
- When storage cannot be listed nothing is suggested.

## Reuniões, Presenças por aprovar, Repertório

Matrix and module rules: [react-meetings](react-meetings.md), [react-rehearsals](react-rehearsals.md),
[react-events](react-events.md).

| Panel | Row | Pending when | Buttons | Resolves |
| --- | --- | --- | --- | --- |
| Reuniões | Criar ata, **Obrigatório** | a meeting of a body the member presides, not cancelled, before today, in the current fiscal year, with no `MeetingAtas` row | **Criar ata** → `/meetings?meeting={id}&ata=1` | a saved ata, draft included (opening the editor is not completion). Not dismissible (`403`) |
| Reuniões | Registar reunião · {body} | always, per body presided (the app cannot know a missing meeting) | **Registar reunião** → `/meetings?new={Type}`; **Agora não** | own dismissal for the month (`yyyy-MM`) |
| Presenças por aprovar | {member} · ensaio | `WillAttend && !Attended` at a not cancelled rehearsal of the fiscal year that can be approved (past, or today starting ≥ 21:00), never the member's own | **Aprovar** → `IRehearsalAdminService.ApproveAsync`; **Recusar** (confirmed) → `RefuseAsync` | approved / refused (here or on the rehearsal page). A future, decided or unknown presença is `404` |
| Repertório | {event} | upcoming, not cancelled, no `EventRepertoires` row | **Preparar repertório** → `/events/{id}?repertoire=1`; **Não é necessário** | a repertoire saved by anyone; "Não é necessário" is own, per event |

- Approval keeps every side effect (member push, status reconcile, XP). Refusal is durable: kept as not attended
  (audited), member notified, so the member's "Fui / Não fui" never returns for it.
- "Today from 21:00" is decided in memory for today's rehearsals (SQLite cannot compare `TimeSpan`); the rest is SQL.
- Approving: the rehearsal module's rule (Admin / Owner / current Ensaiador / Magister).

## Membros

Follows the **system role** (Mod, Admin), the people who may use the limited member-information edit. Rows: members not
expelled with a Leitão / Caloiro / Tuno category and at least one applicable field empty (`MemberInfoRules`: Cidade,
Contacto, Instrumento, Entrada na Tuna, Passagem a Caloiro / Tuno, Padrinho, by category: a Leitão is never asked for
Caloiro / Tuno dates, Fundador / Tuno Honorário have no Leitão / Caloiro date or padrinho, a Fundador's Tuno date is
fixed, an Honorário plays no instrument). Counted and paged in SQL (category JSON queried with `json_each`), sorted by
name. A row leaves once the field is saved, anywhere. Not dismissible.

The editor behind it (see [react-members](react-members.md)):

| Request | Answer |
| --- | --- |
| `GET` / `PUT /api/members/{id}/info`, instruments, mentors without Mod / Admin / Owner | `403` |
| A Mod's full edit, create, delete, nickname, expel, reminder | `403` (Admin / Owner only) |
| Forged body with name / email / category / role / position / status | those fields are not bound |
| Complete date pair changed by a Mod; a date that does not apply | `400` |
| Save with a stale stamp | `409`, nothing written |

## Galeria and Novidades

### Galeria (active members)

- Source of truth: `GalleryMedia.EventId`, set only by an upload that names the event (`POST /api/gallery` form field
  `eventId`; the event must exist and not be cancelled, else `400`). Legacy items have no link and are **never matched
  by title or date**.
- Pending when: the event is not cancelled, starts on or after 1 September of the fiscal year, its last day is before
  today (Portugal), and no **image** (`MediaType.Image`) is linked. A video does not resolve it, on purpose. Newest first.
  `Event.IsPublic` plays no part (members see every event).
- A photo uploaded by anyone removes the row for everyone. Deleting that photo brings it back (the query is the state);
  deleting the event clears `EventId` on its media in the same save (`EventService.DeleteEventAsync`), the media stays.
- **Não tenho**: own dismissal of that event, period = fiscal year. Dismissing an event that has a photo, is cancelled
  or not over is `404`.
- **Adicionar** opens `/gallery?upload=1&event={id}&date=&title=` with the upload dialog prefilled (title = event name,
  date = event day, "Só para membros" on). Cancel stores nothing.
- Reformados and accounts without a category get no Galeria panel; they can still upload on `/gallery`.
- Privacy: the row shows only the event's name, day and place; no gallery item, preview or photo count.
- Schema: `GalleryMedia.EventId` (migration `AddGalleryMediaEventId`): nullable INTEGER, indexed, **no foreign key** (as
  `Activities.EventId`). Additive and N-1 safe; rolling back loses only the panel's use.

### Novidades (writers)

- Who: `NewsAuthorization.CanWrite` with the database's roles, within the Direção in office. Others: `404`.
- Pending when: no dismissal for this Portugal ISO week (`2026-W45`) and no `NewsPost` by the member created within
  Monday 00:00 → next Monday 00:00 Portugal (as UTC). A saved **draft** counts; nothing is published automatically.
  Another writer's post does not resolve mine.
- **Criar novidade** opens the composer on `/news?new=1`. A new post needs an explicit audience (**Público** or
  **Apenas RTUB**, none preselected); edits keep the post's visibility. Draft / publish / delete rights are the news
  module's ([react-news](react-news.md)).
- **Agora não**: own, for the week; returns next Monday (new period key); stored server-side, so it survives devices.

## Daily push

`PendingActionsReminderService` (hosted): `PendingActionsReminder:Enabled` = true, `:ScheduledTime` = `18:00` Portugal
time, `:CatchUpHours` = 6; Azure overrides `PendingActionsReminder__*`.

- Who: every account not expelled (active, Reformado, Leitão, position holders, no category) counted with
  `IPendingActionsService.CountPendingAsync`: the page's own sources with the database's categories, roles and positions,
  every panel, filter and page, dismissed optional rows excluded, required rows always counted. Nothing is cached.
- What: pending > 0 → one push "As minhas ações pendentes" / "Tens ações pendentes para tratar. Abre a página para as
  consultar." (no count, no detail), link `/pending-actions`, tag `pending-actions-reminder`; `PushNotificationService`
  fans it out and keeps the usual "Notificações RTUB" inbox copy. Pending = 0 → no push, no inbox copy. Opted-out /
  no subscription: no device is sent to (the inbox copy is kept, as for every push).
- Once per member and day: before sending, the run claims the member's Portugal day in `PendingActionDismissals` (kind
  `reminder.push`, period `yyyy-MM-dd`, unique index; no source uses that kind, so it hides nothing). A restart,
  catch-up or second instance finds the claim and sends nothing. **At most once**: a push failure after the claim is
  logged and not retried that day (it may have been delivered before failing).
- Failures: a count that throws is neither empty nor pending; that member is retried after the others (3 attempts in
  all), then logged with no claim and no push. One member's failure never stops the run. A claim that cannot be stored
  is the same: only an existing claim row means "already sent"; nothing is sent without a claim.
- Schedule: daily at the configured Portugal time (DST-safe, `PortugalTime`). Started late it still runs that day within
  `CatchUpHours`; later, the day is skipped. Never a backlog.
- Deep link `/pending-actions` is relative and same-origin; a signed-out tap goes through `/login` with the return URL
  kept. Catalogue: [push-notifications](push-notifications.md).

## Dismissal storage (`PendingActionDismissals`, migration `AddPendingActionDismissals`)

| Property | Rule |
| --- | --- |
| Key | `UserId` + `ActionKind` + `SourceKey` + `Period` (unique) |
| Period | chosen by the source: fiscal year (`2026-2027`), ISO week (`2026-W45`), month (`yyyy-MM`) or `""` (once). A new period is a new key, so the prompt returns by itself; nothing is deleted to reset |
| Retries | a repeat finds nothing pending (`404`); concurrent repeats meet the unique index: one row |
| Account deleted | rows cascade (FK to `AspNetUsers`) |
| Audit | not audited (personal, low value), like `LoginEvents` |

Optional rows: Secretaria (fiscal year), Registar reunião (month), Repertório (once per event), Galeria (fiscal year per
event), Novidades (ISO week).

Schema notes (all additive, N-1 safe; rolling back leaves rows unused, nothing to restore):
- `AddPendingActionDismissals`: one table, one unique index.
- `AddEventFinanceClassification`: `Events.HasNoFinances` (INTEGER, default 0) and `Activities.EventId` (nullable,
  indexed, **no foreign key**: SQLite would rebuild `Activities`; precedent `LogisticsBoards.OwnerUserId`). Deleting an
  event (`EventService.DeleteEventAsync`, the only deletion path) clears its activities' `EventId` in the same save.

## Login landing

`Program.MemberLanding`: a local `returnUrl` (deep link, `/login?returnUrl=…`, the PWA's pending push target) always
wins. Without one, every member lands on `/events`; a signed-in member opening `/login` goes there too. The page is
reached from the menu and the daily push. The manifest `start_url` (`/`) is unchanged.
