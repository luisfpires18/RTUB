# Member activity status (active / Reformado) — task 044B

RGI, in substance: Reformados are effective members away from RTUB activity for six months or more; they return after
three months of uninterrupted activity. Code name: `IsRetired` (`true` = Reformado).

## Ownership

| Piece | Role |
| --- | --- |
| `MemberActivityStatusEvaluator` (Application, static, pure) | **The rules.** `Evaluate(currentIsRetired, activityDates, activeSince, now)`, `InitialIsRetired(categories)`, `IsSubjectToRule(user)`, `ActiveSince(user)`. Nothing else decides state. |
| `ApplicationUser.IsRetired` | **Authoritative** current flag. |
| `MemberStatus` table | **Projection/cache**: `IsRetired` (always written with the user flag, same `SaveChanges`), last rehearsal / event / activity, counts, progress for the UI. Recomputed from attendance on every reconciliation; never read as truth. `LastUpdatedAt` = when the projection last changed. `OverrideRetired` is dead (not read or written); drop it in a later migration. |
| `MemberStatusService` | Orchestration, two separate paths over the same batch-loaded activity (one query per source) and evaluator. **Read** (`GetMemberStatusAsync`, `GetMemberStatusesBatchAsync`; profile, member detail, lists): calculate and display only — shows the persisted flag with computed progress; never writes the user, the projection or the audit, never notifies. **Reconcile** (`ReconcileMemberStatusAsync`, `ReconcileAllMemberStatusesAsync`): calculate + persist flag, projection and transition audit in one `SaveChanges` per member, then notify. |

Reconcile callers (the only mutation paths): `MemberStatusUpdateBackgroundService` (startup + daily), the startup
population in `Program.cs` after the MemberStatus migration, `RehearsalAttendanceService.UpdateAttendanceAsync`
(presença confirmed), `EnrollmentService` (answer "vai"), and the Owner sync (`POST /api/owner/users/sync-status`).
Between a threshold passing and the next reconciliation a read still shows the persisted flag (active, "Próximo da
reforma"; or Reformado, "3/3").
| `MemberStatusUpdateBackgroundService` | Scheduling only: once on startup, then daily at `MemberStatusUpdate:ScheduledTime` (local, via `TimeProvider`). Logs the run counts; `Error` if any member failed. |

Clock: one injected `TimeProvider` (`TimeProvider.System` in DI); no `DateTime.Now/UtcNow` in the rules or the service.

## Qualifying activity

- **Ensaio**: `RehearsalAttendance.Attended` (confirmed by Admin) at a rehearsal with `Date < now`, not `IsCanceled`.
- **Atuação**: `Enrollment.WillAttend` at an event that has ended (`EndDate ?? Date < now`), not `IsCancelled`; dated by
  its start. **Temporary source:** `WillAttend` is the member's answer ("vai"), reused as post-event participation
  because no performance-attendance field or table exists (after the event the app shows it as "Quem foi / Foste" and
  Admin corrects it by removing / adding answers). A member who never withdraws from an event they missed gets credit.
  The query is isolated in `MemberStatusService.PerformanceActivityAsync`; when real attendance exists, replace only it.

## Rules

- **Population**: anyone with a membership category — Leitão, Caloiro, Tuno, Veterano, Tunossauro (Fundadores are
  Tunos) — **even when they also have `TunoHonorario`**: the honorary distinction does not invalidate membership in
  another category and never takes precedence. Excluded: someone who is only Tuno Honorário, and users with no category. Excluded members keep their flag; their projection is still refreshed on reconcile.
- **Initial state** (every creation path: `MemberAdminService.CreateAsync` via `ApplyInitialState`, seed `MemberBuilder`
  default, Owner bootstrap): `IsRetired = !categories.Contains(Leitao)`. A Leitão created without a Leitão date gets the
  creation month (`YearLeitao` / `MonthLeitao`), so the six months always have a start.
- **Active → Reformado** when `now >= threshold`, `threshold = max(lastActivity, activeSince).AddMonths(6)` (calendar
  months, `DateTime.AddMonths`: 31 Aug → 28/29 Feb; not 180 days). Exactly at the threshold = six full months = Reformado.
  **Start-date hierarchy** for the six months: (1) the latest qualifying activity; (2) when there is none or it is
  older, the latest admission milestone — Tuno, Caloiro or Leitão year+month, first day of the month, January when the
  month is missing (`ActiveSince`); (3) neither → Reformado at once (`NoQualifyingActivity`). There is no account
  creation timestamp (`ApplicationUser` has none, Identity stores none; `MemberStatus.CreatedAt` is the projection row,
  not the account), which is why creation fills the Leitão month instead. Precision is the month: a Leitão who joins on
  15 Jan 2026 starts on 1 Jan 2026 and, with no activity, is reformed from 1 Jul 2026.
- **Reformado → active** when the calendar months with activity, counted back from the current month (or from the
  previous one while the current month has none yet), reach three. Several activities in a month = one month; a gap or a
  stale run (not reaching last month) does not count.
- **No pre-reformation credit**: a member is reformed only after six months with no activity, so a run counted back from
  now always stops in that gap; no streak counter is stored. (Activity backfilled into the gap afterwards does count: it
  is real activity.)

## Transitions

- Audit: `AuditLog` `EntityType = "MemberActivityStatus"`, `Action = Reformed | Activated`, `UserId`/`UserName` = the
  member, `Changes` JSON with `Reason`, `LastActivity`, `Threshold` (reformed) or `Months` (activated). Visible in
  `/owner/tracing` (the `ApplicationUser` change is also audited by the context).
- Push (only with `MemberStatusUpdate:PushNotificationsEnabled`): reactivation broadcast on an actual Reformado → active
  change; the retirement warning once, when an active member's months left become 1. Repeated runs send nothing new.
- Idempotent: same data + same `now` → no state change, no audit, no push, no projection write.
- A member whose evaluation throws is logged with its id and counted as `Failed`; the run continues.
  `ReconcileAllMemberStatusesAsync` returns `MemberStatusReconciliationResult` (evaluated / activated / reformed /
  unchanged / excluded / failed); the Owner sync returns `{ updated, changed, failed }`.

Tests: `MemberActivityStatusEvaluatorTests` (rules, boundaries) and `MemberStatusServiceTests` (orchestration,
regressions) in `RTUB.Application.Tests`.
