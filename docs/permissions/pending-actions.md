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
| Active member (`IsSubjectToRule && !IsRetired`: Leitão, Caloiro, Tuno, Veterano, Tunossauro not Reformado) | yes | Atuações, Ensaios (answer in place, 072); Logística (073) |
| Reformado | **no**: `GET /api/pending-actions` is `404`, the React page goes to `/events`, the menu has no entry (`/api/account/me` → `retired`), and the daily reminder never evaluates them (087, owner decision 2026-11-07, whatever roles or positions they hold) | - |
| Account with no membership category (e.g. only Tuno Honorário, a bare Owner account) | yes | Logística |
| Direção in office (Magister, Vice-Magister, Secretário, 1.º / 2.º Tesoureiro), not Reformado | yes | + Questões (own open questions; 087) |
| 1.º / 2.º Tesoureiro (current mandate) | yes | + Tesouraria (074) |
| Secretário (current mandate) | yes | + Secretaria (074) |
| Presidente da Mesa / Magister / Presidente do CV (current mandate) | yes | + Reuniões (075) |
| Ensaiador / Magister (current mandate) | yes | + Presenças por aprovar, Repertório (075) |
| System role Mod or Admin (not the Owner, even with Admin too: 087), not Reformado | yes | + Membros (076) |
| Direção in office **and** Mod / Admin / Owner, not Reformado | yes | + Novidades (077; 087 narrowed it to the Direção) |
| Active member | yes | + Galeria (077) |
| Mod / Admin / Owner | as their member type | as their member type: **a role adds no panel** (Tesouraria / Secretaria follow the position, not the role); on Logística a manager still sees only their own cards (not unowned ones) |

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

### Galeria and Novidades (077)

| Request | Answer |
| --- | --- |
| Panel `gallery` as a Reformado / no category | `404` |
| Panel `news` without Mod / Admin / Owner, or outside the Direção in office (087) | `404` |
| Any panel or command as a Reformado (087) | `404` |
| Não tenho / Agora não on a pending row | `204`, own (fiscal year per event / ISO week) |
| Dismissing an event that has a photo, is cancelled or not over | `404` |
| Upload with `eventId` of a cancelled / unknown event, or not a number | `400` |

Nothing new may be written: the upload is the gallery's (any member), the composer the news module's.

### Membros (076)

| Request | Answer |
| --- | --- |
| Panel `members` without Mod / Admin / Owner (same category included) | `404` |
| `GET` / `PUT /api/members/{id}/info`, instruments, mentors without Mod / Admin / Owner | `403` |
| A Mod's full edit, create, delete, nickname, expel, reminder | `403` (unchanged Admin / Owner) |
| A forged body with name / email / category / role / position / status | those fields are not bound: unchanged |
| A complete date pair changed by a Mod; a date that does not apply | `400` |
| A save with a stale stamp | `409`, nothing written |

### Reuniões, presenças, repertório (075)

| Request | Answer |
| --- | --- |
| Panels `meetings`, `approvals`, `repertoire` without the position (Admin / Owner included) | `404` |
| Dismissing a "Criar ata" row | `403` (required) |
| Agora não on "Registar reunião", Não é necessário on a repertoire row | `204`, own, for the month / that event |
| Aprovar / Recusar a pending presença at an approvable rehearsal | `204` (the rehearsal module's rule: Admin / Owner / current Ensaiador / Magister) |
| A future, already decided or unknown presença | `404` |

### Tesouraria and Secretaria commands (074)

| Request | Answer |
| --- | --- |
| Any Tesouraria / Secretaria command by someone who does not hold the position now (Admin / Owner included) | `404` (as the panel) |
| Criar relatório while the year's report is missing | `204` through `CreateReportAsync`; a concurrent second one `404` / `409`; one report |
| Dismissing the report row | `403` (required) |
| Não / Reabrir on a begun, not cancelled event of the year | `204` if `CanManageReports` (normally given by the position's role), else `403` |
| Não on an event already classified, out of the year, future, cancelled | `404` |
| Criar pastas | `204` through `CreateFolderAsync` for the missing names only; existing names untouched |
| Agora não (Secretaria) | `204`, the Secretário's own, for this fiscal year |

### Logística and Questões commands (073)

| Request | Answer |
| --- | --- |
| Concluir on the caller's own open card on a board they see | `204` (the board's `SetStatusAsync`, same rules as on the board) |
| Someone else's card, an unassigned / archived / done card, a private board the caller is not on | `404` |
| A board with no done column | `400`, card unchanged |
| Fechar questão on the caller's own open question | `204` (`QuestionBoardService.CloseAsync`, author only) |
| A question the caller did not write (recipient included), closed or deleted | `404` |
| A command the row does not have (`yes` on a card) | `400` |

Assignment never opens a private board (as on `/logistics`); the module service runs as the caller with their roles from
the database, so a removed role or assignment is refused at once.

## Dismissal (`POST /api/pending-actions/dismissals`, antiforgery required)

| Request | Answer |
| --- | --- |
| A row that is optional (`dismissible`) and pending for the caller now | `204`; one row in `PendingActionDismissals` for caller + kind + source + period |
| A required row, or an answer row (Atuações / Ensaios) | `403`, nothing stored, whatever the body claims |
| Anything not pending for the caller now (unknown, someone else's, already dismissed, wrong panel) | `404`, nothing stored |
| No / bad `X-CSRF-TOKEN` | `400` |

Required/optional and the period are the source's, not the request's. A dismissal is the member's own and never hides
anything for anyone else; shared decisions (e.g. treasury "Sem movimentos", #255) are stored in their module instead.
