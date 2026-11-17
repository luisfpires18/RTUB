# React Rehearsals (`/rehearsals`)

`/rehearsals` is the React page (`src/RTUB.Web/portal/src/Rehearsals.tsx`); `/rehearsals/{id}` is one rehearsal (`RehearsalDetail.tsx`).
Modals live in `RehearsalDialogs.tsx`. Members read through `Endpoints/RehearsalEndpoints.cs` → `IRehearsalAgendaService`.
Admin/Owner and the approval rules use `IRehearsalAdminService`. Rules: `Helpers/RehearsalsAuthorization.cs`.
Also `Services/RehearsalAttendanceService.cs` (presenças, notifications) and `Services/RehearsalService.cs`.
Background services handle reminders, approval reminders, member status and rankings.

**Terminology.** Rehearsals use *attendance* (presença): `RehearsalAttendance`, "Presenças", "As minhas presenças", "Marcar presença".
Events keep *enrollment* (inscrição). Neither borrows the other's word.

## Routes

| Route | Notes |
| --- | --- |
| `/rehearsals` | Members only. A visitor gets `302` to `/login?returnUrl=/rehearsals`. `?season=` (and the old `?fy=`) picks the archive season; `?q=` searches. |
| `/rehearsals/{id}` | One rehearsal with its presenças. Same sign-in redirect. |

Push notifications still open `/rehearsals`. The sign-in `302`s are not legacy redirects and stay at the PROD cutover.

## Data

No schema change in the current rules. Constraints that matter:

- `Rehearsals`: `Date` (date only; one rehearsal per day, the app refuses duplicates), `Location` (required, ≤200, default "Centro Académico"),
  `Theme` (≤500), `Description` (≤1000), `Notes` (≤1000), `StartTime` 21:30 and `EndTime` 00:00 (fixed, never edited), `IsCanceled`,
  `CancellationReason` (≤1000). No recurring-rehearsal table: rehearsals are created one by one, or as every Tuesday and Thursday of a range.
- `RehearsalAttendances`: `RehearsalId`, `UserId`, `WillAttend`, `Attended` (confirmed by Admin or an approver), `Instrument`,
  `OtherInstruments` (≤200), `Notes` (≤500), `CheckedInAt`, `CategoryAtRehearsal` (snapshot).
- Statuses are derived, not stored: **pending** (vou, not confirmed), **confirmed** (`Attended`), **not going** (`WillAttend = false`).
  No late, justified or unknown status. A "não vou" may carry a note as its reason.

## Permissions

| Who | What |
| --- | --- |
| Visitor | Nothing. |
| Any signed-in member | Every rehearsal, who goes, own presença, remove own presença, statistics. |
| Active member on `/pending-actions` | First answer to own unanswered rehearsals, upcoming or past (current fiscal year from admission month). A "Fui" is pending until Admin or Owner confirm it. |
| Mod, Admin, Owner | Create (one date or a range) and edit a rehearsal (`RehearsalsAuthorization.CanEdit`, `canEdit` in the agenda and detail DTOs). |
| Admin, Owner | Cancel / reactivate, delete, push notice, confirm or remove anyone's presença, add a member. |
| Current Ensaiador or Magister (position, not role; 075) | Approve or refuse a pending presença once the rehearsal is approvable. Never their own (only Admin or Owner may). Nothing else of management. |

Approvable: a past rehearsal, or today from 21:00.

**Refuse (075):** `POST /api/rehearsals/{id}/attendances/{attendanceId}/refuse`, by Admin, Owner, Ensaiador or Magister, on an approvable rehearsal
with a pending presença. Not a deletion: the presença stays as not attended (`WillAttend = false`, audited), and the member gets the
"Presença recusada" push, so a past "Fui" is never asked again. Removing a presença (`DELETE`) stays Admin/Owner, or the member for their own.

## Own presença

- Upcoming (day not over) and not cancelled: Vou / Não vou, "Quero tocar" with an instrument (own instruments; a Leitão with none: any), and a note ≤500.
  Same rules server-side, in one modal, like the events answer. The instrument must be in the offered list.
- A new "vou" is pending until confirmed.
- Members may remove their own presença at any time.
- Notifications: own presença → push to others going (and on cancellation). Confirmation → push to the member.

## Create, edit, cancel, delete

- Create one date (duplicate refused), or every Tuesday and Thursday of a range (≤90 days from today; taken dates are skipped).
  Description and notes are saved on create.
- Edit: location, theme, description, notes. Never date or time.
- Cancel: reason required, ≤1000. Deletes every presença. No email. Allowed on a past rehearsal too.
- Reactivate: upcoming cancelled rehearsals only. Presenças are not restored; the confirm says so.
- Delete: hard delete, presenças cascade, behind a confirm.
- Push notice: upcoming only (refused on past or cancelled). Message to subscribed members, optionally Leitões and Caloiros only. Audited as `PushNotificationSent`. Counts are shown, not name lists.

## Approvals and management of presenças

- Confirm pending: member gets a push and the retirement status updates.
- Remove any presença. Add a member, confirmed, with their primary instrument. Members who already answered, and expelled members, are not offered.
- Approvals are allowed only once the rehearsal is approvable (see Permissions).

## Statistics and "Minhas presenças"

- Statistics (`GET /api/rehearsals/stats`, modal): per member, confirmed and pending presenças at the range's past, not cancelled rehearsals; share; category filter; search.
- "Minhas presenças": last 10 (or all) past, not cancelled rehearsals, with the confirmed share. Built from the agenda data.
- Archive: season (fiscal year) and search. Upcoming rehearsals are always shown. No pagination; a season is about 70 rehearsals.

## Pending actions

`/pending-actions` answers a member's own unanswered rehearsals in place ([react-pending-actions](react-pending-actions.md)). It uses
`IRehearsalAttendanceService.AddFirstAttendanceAsync`, create-only: same row, category snapshot and push as `MarkAttendanceAsync`.
An existing presença is never touched. A "Fui" is `Attended = false`, pending in approval, and earns nothing until confirmed.

## Calendário

A new "vou" to an upcoming rehearsal offers "Adicionar ao Google Calendar" ("Presença registada, pendente de aprovação."). The detail page keeps
"Adicionar ao calendário" while the member is going. The calendar approves nothing. Rules, link and dates: [react-events → Calendário](react-events.md).

## API

Every write needs `X-CSRF-TOKEN`. Outcomes reuse `EventResult<T>`.

| Endpoint | Who | Notes |
| --- | --- | --- |
| `GET /api/rehearsals` | member | `{ canManage, canEdit, upcoming, past }`; each with `goingCount`, `mine` (`pending` / `approved` / `notGoing`), `pendingCount` (Admin/Owner). |
| `GET /api/rehearsals/{id}` | member | Rehearsal, notes, presenças `{ going, leitoes, notGoing }` (row id, person, status, instrument, note, `canApprove`, `canRemove`), instrument counts. No user id, email or phone. |
| `GET` / `PUT` / `DELETE /api/rehearsals/{id}/attendance` | member | Own presença. `PUT { willAttend, instrument, notes }`; `409` past or cancelled; `400` field errors. |
| `GET /api/rehearsals/stats?from=&to=` | member | Statistics. |
| `POST /api/rehearsals` | Mod and above | `{ date, location, theme, description, notes }`; duplicate refused. |
| `POST /api/rehearsals/range` | Mod and above | `{ from, to, location, theme, description }` → `{ created, skipped }`. |
| `PUT /api/rehearsals/{id}` | Mod and above | Edit fields, not date or time. |
| `DELETE /api/rehearsals/{id}` | Admin/Owner | Hard delete. |
| `POST /api/rehearsals/{id}/cancel` | Admin/Owner | `{ reason }`; `409` when not allowed. |
| `POST /api/rehearsals/{id}/reactivate` | Admin/Owner | `409` unless cancelled and upcoming. |
| `GET` / `POST /api/rehearsals/{id}/notice` | Admin/Owner | Counts; `{ message, onlyLeitoesAndCaloiros }` → `{ sent, failed }`; `409` past or cancelled. |
| `POST /api/rehearsals/{id}/attendances/{attendanceId}/approve` | Admin/Owner, Ensaiador, Magister | Approvable only; `409` otherwise. |
| `POST /api/rehearsals/{id}/attendances/{attendanceId}/refuse` | Admin/Owner, Ensaiador, Magister | Approvable and pending only (see Permissions). |
| `DELETE /api/rehearsals/{id}/attendances/{attendanceId}` | Admin/Owner (approvable only), or the member for own | |
| `GET /api/rehearsals/{id}/attendances/members?q=` | Admin/Owner | Add-member search. |
| `POST /api/rehearsals/{id}/attendances` | Admin/Owner | Add a member, confirmed; approvable only. |

Code pointers: `Endpoints/RehearsalEndpoints.cs`, `Helpers/RehearsalsAuthorization.cs`, `Services/RehearsalAdminService.cs`,
`Services/RehearsalAttendanceService.cs`.
