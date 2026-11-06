# React: As minhas ações pendentes (`/pending-actions`)

Roadmap #251; foundation = task 071 (issue #252). Permissions: [permissions/pending-actions](permissions/pending-actions.md).

A page of **grouped actions** with the buttons on each row. Not an inbox, notifications feed, chat or one-at-a-time
wizard, and not a statistics page. Nothing is copied into a generic "actions" table: every panel is a query over its
module's own data, run on every load; the only thing stored is a member's own dismissal of an optional action.

## Delivered (071) and still to come

| Panel | Category | State | Rows / buttons |
| --- | --- | --- | --- |
| Atuações | participation | **delivered (read + link)** | unanswered events: Próximas → "Responder" (`/events/{id}?respond=1`, the event's own answer modal); Passadas → "Ver atuação". In-page Vou / Não vou, Fui / Não fui: **#253** |
| Ensaios | participation | **delivered (read + link)** | unanswered rehearsals: Próximas → "Responder" (`/rehearsals/{id}`); Passadas → "Ver ensaio". In-page answers (presença stays pending until approved): **#253** |
| Logística, Questões | tasks | #254 | - |
| Tesouraria, Secretaria | responsibilities | #255 | - |
| Reuniões / atas, presenças por aprovar, repertório | responsibilities | #256 | - |
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
- Row: title (+ "Obrigatório" when required), date / time / place, buttons on the right. Required: one action, no
  dismiss. Optional: the action + "Agora não". Answer rows (attendance): the module's action only.

## API (`Endpoints/PendingActionsEndpoints.cs`, `no-store`)

| Call | Answer |
| --- | --- |
| `GET /api/pending-actions` | `{ today, fiscalYear, status: active\|retired\|none, panels: [first page of each panel that applies] }` |
| `GET /api/pending-actions/panels/{panel}?filter=&page=` | one panel page: `{ key, title, category, filters[{key,label,count}], filter, page, pageSize: 3, total, rows }` |
| `POST /api/pending-actions/dismissals` `{ kind, sourceKey }` | `204` / `403` required or answer / `404` not pending; antiforgery |

Rows carry `kind`, `sourceKey`, `title`, `date`, `time`, `context`, `required`, `dismissible`, `actions[{label, href,
primary}]` - no user ids, emails or other people's data. Page size is fixed (3); an out-of-range page answers the last
one (an emptied last page clamps); an unknown filter answers the default one.

Queries are bounded: counts are SQL `COUNT`, rows `ORDER BY … OFFSET/LIMIT 3`, "unanswered" is a `NOT EXISTS` on the
member's own Enrollment / RehearsalAttendance. No member list, file listing or history is loaded to render a panel.

## Eligibility and source of truth

| Panel | Who | Source of truth | Pending when |
| --- | --- | --- | --- |
| Atuações | active member | `Events` + the member's `Enrollments` | no Enrollment row of theirs (going or not), not cancelled; Próximas: last day (`EndDate ?? Date`) today or later; Passadas: last day before today and start in the current fiscal year |
| Ensaios | active member | `Rehearsals` + the member's `RehearsalAttendances` | no attendance row of theirs, not cancelled; Próximas: day today or later; Passadas: day before today, in the current fiscal year |

- **Active member**: `MemberActivityStatusEvaluator.IsSubjectToRule(user) && !user.IsRetired`
  (`docs/member-activity-status.md`). Read from the database on every call.
- **Fiscal year**: 1 September - 31 August (`FiscalYearHelper`), by the activity's start date. Past prompts never reach
  before 1 September of the current year; no 30-day or 7-day cutoff anywhere.
- **Dates**: "today" is Portugal's (`PortugalTime`), as in each module. Upcoming has no end: every future unanswered
  activity is in the panel, paged.
- Another member's answer never resolves mine; my answer in the module (or by an Admin adding me) resolves it on the
  next load, which the page does on back navigation (bfcache) and on resume (`visibilitychange`).
- Open question for #253: a Leitão who joined mid-year sees the year's earlier past activities; whether to floor
  "Passadas" at the admission month is to be decided with the in-page answers.

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

In 071 no delivered panel is optional, so the page never shows "Agora não" yet; the mechanism is covered by tests with
stand-in sources and is used from #255 / #258.

## Login landing

`Program.MemberLanding`: a local `returnUrl` (deep link, `/login?returnUrl=…` from a members-only route, the PWA's
pending push target) always wins. Without one, an **active** member lands on `/pending-actions`; everyone else still
on `/events`. A signed-in active member opening `/login` goes to `/pending-actions` too. The manifest `start_url`
(`/`) is unchanged.

## Migration and rollback

`AddPendingActionDismissals` is additive (one new table, one unique index, FK to `AspNetUsers` with cascade). N-1
safe: 2.3.x ignores the table. Rolling the app back hides the page and leaves the rows unused; nothing to restore.

## Files

`src/RTUB.Core/Entities/PendingActionDismissal.cs`, `src/RTUB.Application/Interfaces/IPendingActionsService.cs`
(service + `IPendingActionSource` contract), `src/RTUB.Application/Services/PendingActionsService.cs`,
`src/RTUB.Application/Services/PendingActionSources.Attendance.cs`, `src/RTUB.Web/Endpoints/PendingActionsEndpoints.cs`,
`src/RTUB.Web/portal/src/PendingActions.tsx`, `pendingActionsApi.ts`, `styles.css` (`.pa-*`). A new panel = a new
`IPendingActionSource` registered in `ServiceCollectionExtensions` (order = page order).

Tests: `tests/RTUB.Application.Tests/Services/PendingActionsServiceTests.cs` (eligibility, fiscal year, paging, clamp,
dismissal, live roles), `tests/RTUB.Integration.Tests/Api/PendingActionsApiTests.cs` (real host + SQLite, CSRF, landing),
`tests/responsive/responsive_audit.py` scenarios `pending-actions`, `pending-actions-past`.
