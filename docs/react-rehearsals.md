# React Rehearsals (`/rehearsals`, React track 014)

`/rehearsals` is a React page (`portal/src/Rehearsals.tsx`) and `/rehearsals/{id}` one rehearsal
(`RehearsalDetail.tsx`); the modals live in `RehearsalDialogs.tsx`. Everything reads a thin API
(`Endpoints/RehearsalEndpoints.cs` → `IRehearsalAgendaService` for members, `IRehearsalAdminService` for
Admin/Owner; rules in `Helpers/RehearsalsAuthorization.cs`). The 2,986-line Blazor `Pages/Activities/Rehearsals.razor`
is retired. DEV only; **no schema change**.

**Terminology.** Rehearsals use *attendance* / **presença** (`RehearsalAttendance`, "Presenças", "As minhas
presenças", "Marcar presença"). Events keep *enrollment* / **inscrição**; neither borrows the other's word
(`PortalRouteTests` checks both directions).

## Routes

| Route | Owner | Notes |
| --- | --- | --- |
| `/rehearsals` | React (014) | Members only: a visitor gets `302 /login?returnUrl=/rehearsals` (the Blazor page was `[Authorize]`). `?season=` (and the old `?fy=`) picks the archive season; `?q=` searches. |
| `/rehearsals/{id}` | React (014) | New: one rehearsal with its presenças (the old page used modals). Same sign-in redirect. |
| Push links | unchanged | Notifications still open `/rehearsals`. |

The Blazor menu links `/rehearsals` with a full page load (`data-enhance-nav="false"`).

## Audit (real `app.db`, read-only scratch copy, aggregates only)

| Table | Fields | Real data |
| --- | --- | --- |
| `Rehearsals` | `Date` (date only, unique per day by the app), `Location` required ≤200 (default "Centro Académico"), `Theme` ≤500, `Description` ≤1000, `Notes` ≤1000, `StartTime` 21:30 / `EndTime` 00:00 (fixed, never edited), `IsCanceled`, `CancellationReason` ≤1000 | 74 rehearsals, 9 cancelled; all 21:30-00:00; 66 on Tue/Thu; 73 at "Centro Académico"; 16 with a theme, 9 a description, 0 notes |
| `RehearsalAttendances` | `RehearsalId`, `UserId`, `WillAttend` (vou / não vou), `Attended` (confirmed by Admin), `Instrument`, `OtherInstruments` ≤200, `Notes` ≤500, `CheckedInAt`, `CategoryAtRehearsal` (snapshot) | 833 rows: 733 going (all confirmed), 100 not going, 0 pending; 45 with a note (longest 49) |

No recurring-rehearsal table: rehearsals are created one by one, or as every Tuesday and Thursday of a range.
Statuses are derived, not stored: **pending** (vou, not confirmed), **confirmed** (`Attended`), **not going**
(`WillAttend = false`). There is no "late", "justified" or "unknown" status; a "não vou" carries an optional reason
in its note.

### Rules - old page vs React

| | Old Blazor `/rehearsals` | React now |
| --- | --- | --- |
| Who reads | any signed-in member: every rehearsal, who goes, their notes | same |
| Own presença | upcoming (day not over), not cancelled: Vou / Não vou, "Quero tocar" + instrument (own instruments; a Leitão with none: any), note | same rules, **server-side**; one modal, as the events answer; instrument validated against the offered list; note ≤500 |
| New "vou" | pending (`Attended = false`) until confirmed | same |
| Remove own presença | from the list, any time; on a past card while pending | same (`DELETE /attendance`), any time |
| Manage | `IsInRole("Admin")` in the UI only | **Admin or Owner** (Owner inherits Admin), enforced in `RehearsalAdminService`; Mod has no rehearsal rights (as before) |
| Create | one date (duplicate refused) or every Tue/Thu of a range (≤90 days, from today; taken dates skipped) | same; **description and notes typed when creating are now saved** (the old create dropped them) |
| Edit | location, theme, description, notes (never date or time) | same |
| Cancel | reason required ≤1000; deletes every presença; no email | same (also allowed on a past rehearsal, as before) |
| Reactivate | upcoming cancelled only; presenças not restored | same, with a confirm saying so |
| Delete | hard delete (presenças cascade) | same, behind a confirm |
| Push notice | upcoming: message to subscribed members, optionally Leitões and Caloiros; audited `PushNotificationSent` | same (counts instead of name lists); refused on past or cancelled |
| Confirm / remove others / add a member | once **approvable** (past, or today starting ≥21:00): confirm pending (member gets a push, retirement status updated), remove any, add a member confirmed with their primary instrument | same; a member who already answered is not offered; expelled members not offered |
| Statistics | members: per member confirmed and pending presenças at the range's past, not cancelled rehearsals; share; category filter; search | same (`GET /api/rehearsals/stats`), a modal |
| "Minhas Presenças" | last 10 (or all) past, not cancelled rehearsals, confirmed share | same, a modal from the agenda data |
| Notifications | own presença: push to others going (and on cancellation); confirm: push to the member | unchanged: the same `RehearsalAttendanceService` calls; tests use a push fake |
| Search / filters | season (fiscal year), search both lists | season + search on the archive; upcoming always shown |

Not kept: pagination (a season is ~70 rehearsals) and the approval filter of the old modal (pending shows as a pill).

## API

| Endpoint | Who | Notes |
| --- | --- | --- |
| `GET /api/rehearsals` | member | `{ canManage, upcoming, past }`; each with `goingCount`, `mine` (`pending`/`approved`/`notGoing`), `pendingCount` (Admin/Owner only). |
| `GET /api/rehearsals/{id}` | member | rehearsal, notes, presenças `{ going, leitoes, notGoing }` (row id, person, status, instrument, note, `canApprove`, `canRemove`), instrument counts. No user id, email or phone. |
| `GET`/`PUT`/`DELETE /api/rehearsals/{id}/attendance` | member | own presença; `PUT { willAttend, instrument, notes }`; 409 past or cancelled; 400 field errors. |
| `GET /api/rehearsals/stats?from=&to=` | member | statistics. |
| `POST /api/rehearsals`, `POST /api/rehearsals/range`, `PUT`/`DELETE /api/rehearsals/{id}` | Admin/Owner | `{ date, location, theme, description, notes }`; range `{ from, to, location, theme, description }` → `{ created, skipped }`. |
| `POST /api/rehearsals/{id}/cancel`, `/reactivate` | Admin/Owner | `{ reason }`; 409 when not allowed. |
| `GET`/`POST /api/rehearsals/{id}/notice` | Admin/Owner | counts; `{ message, onlyLeitoesAndCaloiros }` → `{ sent, failed }`. |
| `POST /api/rehearsals/{id}/attendances/{attendanceId}/approve` | Admin/Owner | approvable only (409 otherwise). |
| `DELETE /api/rehearsals/{id}/attendances/{attendanceId}` | Admin/Owner (approvable) or the member | |
| `GET /api/rehearsals/{id}/attendances/members?q=`, `POST /api/rehearsals/{id}/attendances` | Admin/Owner | add a member, confirmed; approvable only; user ids only to Admin/Owner. |

Every write needs the antiforgery token (`X-CSRF-TOKEN`); outcomes reuse `EventResult<T>`.

## Retired

`Pages/Activities/Rehearsals.razor` (+ CSS); the shared `RehearsalCard`, `ParticipationModal` and `InstrumentCounter`
(only that page used them); `IRehearsalFilterService`, `IRehearsalUrlService`, `IRehearsalAttendanceFilterService`,
`IRehearsalStatisticsService` and their implementations; the form/display DTOs `AttendanceFormModel`,
`RehearsalFormModel`, `RehearsalRangeFormModel`, `RehearsalAttendanceDisplay`; their bUnit/placebo tests.
`RehearsalService`, `RehearsalAttendanceService`, the entities and the background services (reminders, approval
reminder, member status, rankings) are unchanged.

## Follow-ups

- Bulk "confirmar todas" does not exist (it did not before either); pending today: 0.
- The 302 sign-in redirects stay as they are at the PROD cutover (they are not legacy redirects).
