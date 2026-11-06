# Permissions: /pending-actions (As minhas ações pendentes)

Source: `PendingActionsService` + one `IPendingActionSource` per panel (`src/RTUB.Application/Services/PendingAction*.cs`).
Page doc: [react-pending-actions](../react-pending-actions.md).

The page grants **nothing**. It shows actions a member can already do in the module, and every button opens (or, in
later phases, calls) that module with that module's own rules. Panels are decided per call **from the database**
(categories, `IsRetired`, `IsExpelled`, roles; positions from #255/#256 on), never from the cookie's claims, so a role,
mandate or status change shows on the next load. A panel that does not apply to the member is `404` on its own URL.

| Who | Page | Panels (task 071) |
| --- | --- | --- |
| Visitor | `/pending-actions` → sign in (`returnUrl` kept); API `401` | - |
| Expelled / unknown account | API `401` (cannot sign in anyway) | - |
| Active member (`IsSubjectToRule && !IsRetired`: Leitão, Caloiro, Tuno, Veterano, Tunossauro not Reformado) | yes | Atuações, Ensaios (answer in place, 072) |
| Reformado | yes | none yet (participation is for active members; their own responsibilities come with #254-#257) |
| Account with no membership category (e.g. only Tuno Honorário, a bare Owner account) | yes | none yet |
| Mod, Admin, Owner | as their member type | as their member type: **a role adds no panel in 071** |

"Active" is the canonical rule of `docs/member-activity-status.md` (`ApplicationUser.IsRetired`, written only by the
reconciliation); the page never computes its own activity status, and being active grants no module permission.

## Answers (`POST /api/pending-actions/responses`, antiforgery required, task 072)

| Request | Answer |
| --- | --- |
| Vou / Não vou, Fui / Não fui on an activity pending for the caller now (active member, own unanswered, not cancelled, in the window) | `204`; the member's own Enrollment / RehearsalAttendance through the module's service |
| Anything else (already answered, cancelled, before PastFrom, previous fiscal year, unknown id, wrong kind, Reformado / no category) | `404`, nothing written |
| A concurrent first answer (other tab, module page) | `409`, the first one stays |
| Instrument not offered to the caller, note too long, answer not yes / no | `400` |

The page cannot approve anything: a past rehearsal "Fui" is pending (`Attended = false`) until Admin / Owner confirm it on
the rehearsal page, exactly as a module "vou". No new permission was added to `EventsAuthorization` or
`RehearsalsAuthorization`; the retrospective answer is the page's own narrow rule (own, current year from PastFrom,
not cancelled, unanswered).

## Dismissal (`POST /api/pending-actions/dismissals`, antiforgery required)

| Request | Answer |
| --- | --- |
| A row that is optional (`dismissible`) and pending for the caller now | `204`; one row in `PendingActionDismissals` for caller + kind + source + period |
| A required row, or an answer row (Atuações / Ensaios) | `403`, nothing stored, whatever the body claims |
| Anything not pending for the caller now (unknown, someone else's, already dismissed, wrong panel) | `404`, nothing stored |
| No / bad `X-CSRF-TOKEN` | `400` |

Required/optional and the period are the source's, not the request's. A dismissal is the member's own and never hides
anything for anyone else; shared decisions (e.g. treasury "Sem movimentos", #255) are stored in their module instead.
