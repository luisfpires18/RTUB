# React: As minhas ações pendentes (`/pending-actions`)

Roadmap #251; foundation = task 071 (issue #252); in-page answers = task 072 (issue #253); Logística and Questões =
task 073 (issue #254); Tesouraria and Secretaria = task 074 (issue #255);
Reuniões, Presenças por aprovar and Repertório = task 075 (issue #256). Permissions: [permissions/pending-actions](permissions/pending-actions.md).

A page of **grouped actions** with the buttons on each row. Not an inbox, notifications feed, chat or one-at-a-time
wizard, and not a statistics page. Nothing is copied into a generic "actions" table: every panel is a query over its
module's own data, run on every load; the only thing stored is a member's own dismissal of an optional action.

## Delivered (071-075) and still to come

| Panel | Category | State | Rows / buttons |
| --- | --- | --- | --- |
| Atuações | participation | **delivered (072)** | unanswered events: Próximas → **Vou / Não vou**; Passadas → **Fui / Não fui**; the title opens `/events/{id}` |
| Ensaios | participation | **delivered (072)** | unanswered rehearsals: Próximas → **Vou / Não vou**; Passadas → **Fui / Não fui** (a "Fui" stays pending until approved); the title opens `/rehearsals/{id}` |
| Logística | tasks | **delivered (073)** | the member's own open cards (owner or collaborator) on boards they see: **Concluir** (the board's own status change to its done column) and **Abrir** (`/logistics/{board}?card={id}`) |
| Questões | tasks | **delivered (073)** | the member's own open questions (the ones they may close): **Fechar questão** (asks first) and **Abrir** (`/questions?question={id}` opens the conversation) |
| Tesouraria | responsibilities | **delivered (074)** | current 1.º / 2.º Tesoureiro: **Criar relatório** (Obrigatório while the year's report is missing), then each begun event of the year: **Sim** (opens the report's "Nova atividade" for that event) / **Não** (shared "Sem movimentos", confirmed); "Sem movimentos" filter with **Reabrir** |
| Secretaria | responsibilities | **delivered (074)** | current Secretário: **Criar pastas** (last year's top-level folders missing this year, confirmed) / **Agora não** (own, this fiscal year) |
| Reuniões | responsibilities | **delivered (075)** | current Presidente da Mesa / Magister / Presidente do CV: **Criar ata** (Obrigatório) for each past meeting of their bodies this fiscal year with no ata saved; optional monthly **Registar reunião** per body (Agora não = this month) |
| Presenças por aprovar | responsibilities | **delivered (075)** | current Ensaiador / Magister: **Aprovar** / **Recusar** (confirmed) each pending presença at an approvable rehearsal of the fiscal year |
| Repertório | responsibilities | **delivered (075)** | current Ensaiador / Magister: upcoming, not cancelled events with no repertoire: **Preparar repertório** / **Não é necessário** (own dismissal per event) |
| Membros (campos em falta) | responsibilities | #257 | - |
| Galeria e novidades | participation | #258 | - |
| Push summary | - | #259 (uses `IPendingActionsService.CountPendingAsync`) | - |

A panel that is not delivered is **not shown** (no empty placeholder pretending a module is done).

## Page

- Shared shell header (crest left, inbox / profile / menu right). Eyebrow "Área de membros", serif title "As minhas
  ações pendentes", lead "Responde às atividades e trata do que está por fazer.", "No ativo" / "Reformado" badge and
  Portugal's date. No weekly framing.
- Menu: "As minhas ações pendentes" is the first link of the **Membro** group (every signed-in member, Leitões too),
  not under Gestão.
- Panels: two columns from 900 px (Atuações | Ensaios, later panels follow in registration order), one column below.
  Category buttons (Tudo / Participação / Tarefas / Responsabilidades) appear only when the panels span two or more
  categories.
- Each panel: title + pending count, its sub-filters with counts (Próximas / Passadas), **three rows per page**, a footer
  with previous / next and "1–3 de 7 · página 1 de 3". The row area keeps a three-row minimum height (empty, loading and
  error states included); rows grow for wrapped text, never clipped. Phones: rows wrap, buttons go under the text.
- Row: title (+ "Obrigatório" when required; links to the item's page when it has one), date / time / place, buttons
  on the right. Required: one action, no dismiss. Optional: the action + "Agora não". Answer rows (attendance): the two
  answers, each labelled with the activity for screen readers ("Vou: Serenata…").
- Vou / Fui opens a small dialog (instrument + optional note) **only** when the member has two or more instruments to
  choose from; otherwise it is sent at once with the primary (or none). Não vou / Não fui is sent at once. Cancel / Esc
  sends nothing. The row stays until the server answers; then the panel asks for the same page again (the row leaves,
  the counts drop, an emptied last page shows the one before), focus stays on the panel title and the page does not
  scroll. Buttons are disabled while a request is in flight.

## API (`Endpoints/PendingActionsEndpoints.cs`, `no-store`)

| Call | Answer |
| --- | --- |
| `GET /api/pending-actions` | `{ today, fiscalYear, status: active\|retired\|none, panels: [first page of each panel that applies] }` |
| `GET /api/pending-actions/panels/{panel}?filter=&page=` | one panel page: `{ key, title, category, filters[{key,label,count}], filter, page, pageSize: 3, total, rows }` |
| `POST /api/pending-actions/dismissals` `{ kind, sourceKey }` | `204` / `403` required or answer / `404` not pending; antiforgery |
| `POST /api/pending-actions/responses` `{ kind, sourceKey, answer: yes\|no\|done\|close, instrument?, notes? }` (072, 073) | `204` saved / `404` not pending for the caller now (answered here, in the module or by an Admin; cancelled; outside the window; not active) / `409` a concurrent first answer won / `400` bad answer, instrument not offered, note too long; antiforgery |

Rows carry `kind`, `sourceKey`, `title`, `date`, `time`, `context`, `required`, `dismissible`, `href`, `actions[{label,
href, primary, answer}]`; the page also carries `instruments` / `defaultInstrument` for the answer dialog - no user ids, emails or other people's data. Page size is fixed (3); an out-of-range page answers the last
one (an emptied last page clamps); an unknown filter answers the default one.

Queries are bounded: counts are SQL `COUNT`, rows `ORDER BY … OFFSET/LIMIT 3`, "unanswered" is a `NOT EXISTS` on the
member's own Enrollment / RehearsalAttendance. No member list, file listing or history is loaded to render a panel.

## Eligibility and source of truth

| Panel | Who | Source of truth | Pending when |
| --- | --- | --- | --- |
| Atuações | active member | `Events` + the member's `Enrollments` | no Enrollment row of theirs (going or not), not cancelled; Próximas: last day (`EndDate ?? Date`) today or later; Passadas: last day before today and start on or after **PastFrom** |
| Ensaios | active member | `Rehearsals` + the member's `RehearsalAttendances` | no attendance row of theirs, not cancelled; Próximas: day today or later; Passadas: day before today, on or after **PastFrom** |

- **Active member**: `MemberActivityStatusEvaluator.IsSubjectToRule(user) && !user.IsRetired`
  (`docs/member-activity-status.md`). Read from the database on every call.
- **Fiscal year**: 1 September - 31 August (`FiscalYearHelper`), by the activity's start date. Past prompts never reach
  before 1 September of the current year; no 30-day or 7-day cutoff anywhere.
- **PastFrom** (072, decides the 071 open question): 1 September of the current fiscal year, or the first day of the
  member's admission month (earliest of Leitão / Caloiro / Tuno; month missing = January) when that is later. A Leitão
  who joined in October is not asked about September; activities before someone joined are not their missing answers.
- **Boundaries**: the module's own rule, on `PortugalTime` and the injected `TimeProvider`. An event in progress
  (multi-day, or later today) is Próximas until its last day is over (midnight, Portugal); a rehearsal is Próximas all of
  its day. So a Vou on the page is accepted exactly when the module's own answer modal accepts it.
- **Dates**: "today" is Portugal's (`PortugalTime`), as in each module. Upcoming has no end: every future unanswered
  activity is in the panel, paged.
- Another member's answer never resolves mine; my answer in the module (or by an Admin adding me) resolves it on the
  next load, which the page does on back navigation (bfcache) and on resume (`visibilitychange`).

## Answers (072)

Each answer re-checks, on the server, that the activity is pending for the caller **now** (same query as the panel),
then writes through the module's own domain service. The caller's id comes from the session only; `sourceKey` is just
the activity id, so another member's row can never be addressed.

| Answer | Writes (domain service) | Side effects |
| --- | --- | --- |
| Vou (upcoming event) | `IEnrollmentService.CreateEnrollmentAsync`: Enrollment `WillAttend = true`, instrument (offered list; none = not playing), note, other instruments, `CategoryAtEvent` snapshot | push to the others going (the service's own rule), as the event page |
| Não vou (upcoming event) | same, `WillAttend = false` + optional reason | the service's "não vai" push, as the event page |
| Fui (past event) | same, `WillAttend = true`, **no push** (the service sends pushes for future events only; `skipNotification` too) | **counts at once** as a performance (`WillAttend` at an ended event is the temporary participation source of `docs/member-activity-status.md`, the same row an Admin adds in "Quem foi") and in the leaderboard XP; status reconciled immediately (`ReconcileMemberStatusAsync`), as a presença approval does. Admin corrects it in "Quem foi" |
| Não fui (past event) | same, `WillAttend = false` | none; kept as the member's answer ("Não foram") |
| Vou / Não vou (upcoming rehearsal) | `IRehearsalAttendanceService.AddFirstAttendanceAsync` (new, create-only): `WillAttend` true / false, `Attended = false`, `CategoryAtRehearsal` | the service's push to others going, as the rehearsal page |
| Fui (past rehearsal) | same, `WillAttend = true`, `Attended = false` (**pending**) | none until an Admin confirms it on the rehearsal page (then XP, activity status and the member's push, as any presença); never self-approved |
| Não fui (past rehearsal) | same, `WillAttend = false` | none |

- **Create-only.** An existing answer is never changed from here: a retry or double click finds nothing pending (`404`);
  a race with the module page or another tab hits the unique `(EventId, UserId)` / `(RehearsalId, UserId)` index
  (`409`) or the service's existence check. Changing an answer is still done on the module page.
- **No new rehearsal or event** is ever created; a past answer only adds the member's own row.
- **Stale reads**: cancelled, removed, answered elsewhere or out of the window between the read and the click → `404`,
  and the page refreshes the panel with "Esta atividade já tinha resposta ou mudou entretanto."
- **Offline / failure**: the row stays, with "Não foi possível guardar…"; nothing is shown as saved.
- Module parity: `/events/{id}` shows "Disseste que vais" and "Quem vai"; `/rehearsals/{id}` shows the presença as
  pending, the Admin's approval queue includes it.

## Logística and Questões (073)

Both panels are for **every signed-in member** (Leitões and Reformados included: it is their own work; activity status
plays no part). Each command re-checks the row is pending for the member, then calls the module's own service **as the
member, with the roles read from the database** (`PendingActionMember.ToPrincipal()`), so the module's rules decide.

| Panel | Pending when | Command | Module call | Leaves the panel when |
| --- | --- | --- | --- | --- |
| Logística | card not archived, not done, on a board the member sees and that is not archived (`LogisticsKanbanService.WorkableCards`, the same rule as "As minhas tarefas"), and the member is its owner or a collaborator | Concluir (`done`) | `ILogisticsKanbanService.SetStatusAsync(id, Done)`: moves the card to the board's done column, history, version | done (here or on the board), unassigned, archived, board archived or made private without them |
| Questões | the member is the author, not closed, not deleted (`QuestionsAuthorization`: only the author closes; no role overrides) | Fechar questão (`close`), after a confirmation | `IQuestionBoardService.CloseAsync`: the thread and replies are kept | closed (here or on `/questions`), deleted |

- **Order**: Logística by deadline (none last), then id; Questões answered ones first (the member's turn), then latest
  activity. No sub-filters. Context: board, status, priority (Alta / Urgente), checklist n/m, Responsável / Colaborador;
  recipient position and whether it is answered.
- Unowned cards are left out even for Mod / Admin / Owner ("As minhas tarefas" shows them to managers): they are not
  the member's work.
- **Not dismissible, no snooze**: a card or an open question is real work. Opening it is not completion; only the
  module's state removes it. ("Mais tarde" was optional in #254 and is not offered.)
- A board with no done column answers `400` ("Este quadro não tem uma coluna DONE.", the board's message), and the card stays.
- Retries: a second Concluir / Fechar finds nothing pending (`404`).
- `/questions?question={id}` (new) opens that conversation once; the API decides access as before.

## Tesouraria and Secretaria (074)

Shown by the **current mandate**: `ApplicationUser.Positions` (the mandate in office, synced by `SyncCurrentMandateAsync`;
the previous one stays until the new year's first assignment). A role without the position shows nothing (`404` on the
panel and its commands); a mandate that ends removes the panel at once. Writes still follow the module's rule, as the
member with the database's roles. Holding a Direção position grants Admin (Mod for a Caloiro) on its own
(`RoleManagementService`), so a treasurer passes `TreasuryAuthorization.CanManageReports`; if that role was removed by
hand the module refuses (`403`) and the page says so. **No permission was added.**

### Tesouraria (1.º / 2.º Tesoureiro, shared by both)

| Row | Pending when | Buttons | Resolves |
| --- | --- | --- | --- |
| Relatório de contas {ano} — **Obrigatório** | no `Reports` row with `Year` = the fiscal year's start | Criar relatório → `ITreasuryService.CreateReportAsync` (title "Relatório de Contas Y - Y+1", year rule, permission) | a report of the year exists (made here or on `/treasury`). Not dismissible (`403`) |
| one per event (only once the report exists) | not cancelled, starts in the fiscal year and **has begun** (start before tomorrow), `Event.HasNoFinances` false, and no activity **of this year's report** with `Activity.EventId` = the event | **Sim** → `/treasury/reports/{id}?activityForEvent={event}`: the report page opens "Nova atividade" with the event's name, dates and link (nothing is saved until Guardar; Cancelar leaves the row). **Não** (confirmed, "Marcar sem movimentos") → `HasNoFinances = true` | an activity linked to the event is saved (on the report page, any treasurer), or "Não". Deleting the activity or unlinking it (edit → Atuação: Nenhuma) brings the row back |
| "Sem movimentos" filter | `HasNoFinances` true for a begun event of the year | **Reabrir** → `HasNoFinances = false` | back under "Por classificar" |

- No transaction is ever created from the page; "Sim" only opens the activity form. Association is the durable
  `Activity.EventId`, never a name match.
- "Não" / "Reabrir" have no module page; they require `TreasuryAuthorization.CanManageReports` (the report rule) and are
  audited with the event (`Event.HasNoFinances` is a column of the audited `Events`).
- **Concurrency**: `TreasuryService.CreateReportAsync` now checks and inserts under one process-wide lock (single App
  Service instance; a unique index on `Reports.Year` would be needed to scale out), so parallel clicks from both
  treasurers, here or on `/treasury`, make one report: the others get `404` / `409` and the page refreshes.
- Order: events by date (oldest first); "Sem movimentos" newest first. Future events are not asked yet.

### Secretaria (Secretário)

- **Structure**: the previous fiscal year's top-level Documentação folders, as they are (the board files folder
  `Logistics` and its sub-folders left out). No folder list is invented; a first year (no previous folders) asks nothing.
- **Pending when** one of those names (case-insensitive) is missing in the current year. One row, previewing the names.
- **Criar pastas** (confirmed: "Criar N pastas em AAAA-AAAA: …? Nenhuma pasta ou ficheiro existente é alterado.") →
  `IDocumentationService.CreateFolderAsync` for each missing name, as the member: its name rule and permission; it
  refuses a name that exists, so nothing is overwritten and a repeat is harmless. Folders are only created, never deleted.
- **Agora não**: the Secretário's own dismissal, period = the fiscal year (back next September, or for a new Secretário).
- When storage cannot be listed the module shows no folders, so nothing is suggested ("Nada por criar…").

## Reuniões, Presenças por aprovar, Repertório (075)

All three follow the **current position** (a role adds nothing); the writes are the modules', which check the same
positions. Matrix and module changes: [permissions/meetings](permissions/meetings.md), rehearsals, events.

| Panel | Row | Pending when | Buttons | Resolves |
| --- | --- | --- | --- | --- |
| Reuniões | Criar ata — **Obrigatório** | a meeting of a body the member presides over, not cancelled, before today, in the current fiscal year (older gaps belong to earlier mandates), with no `MeetingAtas` row | **Criar ata** → `/meetings?meeting={id}&ata=1` (the module's ata editor) | a saved ata, draft included (opening the editor is not completion; publishing is separate). Not dismissible (`403`) |
| Reuniões | Registar reunião · {body} | always, per body presided (the app cannot know a missing meeting; nothing is inferred) | **Registar reunião** → `/meetings?new={Type}`; **Agora não** | the member's own dismissal for the month (`yyyy-MM`): back next month |
| Presenças por aprovar | {member} · ensaio | `WillAttend && !Attended` at a not cancelled rehearsal of the fiscal year that can be approved (past, or today starting ≥ 21:00) | **Aprovar** → `IRehearsalAdminService.ApproveAsync`; **Recusar** (confirmed) → `RefuseAsync` | approved / refused (here or on the rehearsal page). A future presença is never listed nor approvable early |
| Repertório | {event} | upcoming (last day today or later), not cancelled, no `EventRepertoires` row | **Preparar repertório** → `/events/{id}?repertoire=1`; **Não é necessário** | a repertoire saved by anyone (for everyone); "Não é necessário" is the member's own, for that event |

- Approval keeps every side effect (member push, activity status reconcile, XP). Refusal is durable: kept as not
  attended (audited), member notified, so the member's "Fui / Não fui" never comes back for it.
- Approvable "today from 21:00" is decided in memory for today's rehearsals (SQLite cannot compare `TimeSpan`); the rest
  is SQL.

## Dismissal (`PendingActionDismissals`, migration `AddPendingActionDismissals`)

| Property | Rule |
| --- | --- |
| Who may dismiss | the member, for themselves, only an **optional** row pending for them now (server decides) |
| Required rows / answer rows | never: `403`, also for crafted requests |
| Key | `UserId` + `ActionKind` + `SourceKey` + `Period` (unique) |
| Period (reset) | chosen by the source: fiscal year (`2026-2027`), ISO week (`2026-W45`, the news prompt in #258) or `""` (once). A new period is a new key, so the prompt returns by itself; nothing is ever deleted to reset |
| Scope | personal: other members are unaffected. Shared decisions (treasury "Sem movimentos") are module data, not dismissals |
| Retries | a repeat finds nothing pending (`404`, nothing stored); concurrent repeats meet the unique index: one row |
| Account deleted | rows cascade |
| Audit | not audited (personal, low value), like `LoginEvents` |

The first optional row is the Secretaria's (074); the news prompt (#258) follows.

## Login landing

`Program.MemberLanding`: a local `returnUrl` (deep link, `/login?returnUrl=…` from a members-only route, the PWA's
pending push target) always wins. Without one, an **active** member lands on `/pending-actions`; everyone else still
on `/events`. A signed-in active member opening `/login` goes to `/pending-actions` too. The manifest `start_url`
(`/`) is unchanged.

## Migration and rollback

**074** `AddEventFinanceClassification`: additive — `Events.HasNoFinances` (INTEGER, default 0) and `Activities.EventId`
(nullable, indexed, **no foreign key**: SQLite would rebuild `Activities`; precedent `LogisticsBoards.OwnerUserId`, 067).
Deleting an event (`EventService.DeleteEventAsync`, the only deletion path) clears its activities' `EventId` in the same
save, so no activity points at a deleted event. N-1 safe: 2.3.x ignores both columns and inserts events with
the default. Rolling back loses only the links' use; no data to restore.

**071**:

`AddPendingActionDismissals` is additive (one new table, one unique index, FK to `AspNetUsers` with cascade). N-1
safe: 2.3.x ignores the table. Rolling the app back hides the page and leaves the rows unused; nothing to restore.

## Files

`src/RTUB.Core/Entities/PendingActionDismissal.cs`, `src/RTUB.Application/Interfaces/IPendingActionsService.cs`
(service + `IPendingActionSource` contract), `src/RTUB.Application/Services/PendingActionsService.cs`,
`src/RTUB.Application/Services/PendingActionSources.Attendance.cs`, `src/RTUB.Web/Endpoints/PendingActionsEndpoints.cs`,
`src/RTUB.Web/portal/src/PendingActions.tsx`, `pendingActionsApi.ts`, `styles.css` (`.pa-*`). A new panel = a new
`IPendingActionSource` registered in `ServiceCollectionExtensions` (order = page order).

Tests: `tests/RTUB.Application.Tests/Services/PendingActionsServiceTests.cs` (eligibility, fiscal year, paging, clamp,
dismissal, live roles), `tests/RTUB.Integration.Tests/Api/PendingActionsApiTests.cs` (real host + SQLite, CSRF, landing; 072: answers through
the real services, module parity, races, stale reads), `RehearsalAttendanceAdminWorkflowTests` (`AddFirstAttendanceAsync`),
`tests/responsive/responsive_audit.py` scenarios `pending-actions`, `pending-actions-past`, `pending-actions-dialog`
(opens the "Fechar questão" confirmation and never confirms: the audit must not write; 073 replaced the 072
`pending-actions-answer` scenario, which clicked the first primary button and so answered or completed rows on the
audit database).
