# Permissions: /rehearsals

Source: `RehearsalsAuthorization`. Page doc: [react-rehearsals](../react-rehearsals.md).

| Who | What |
| --- | --- |
| Visitor | nothing |
| Any signed-in member | every rehearsal, who goes, their own presença, remove their own, statistics |
| Active member, on `/pending-actions` (072) | first answer to their own unanswered rehearsals, upcoming or past (current fiscal year from their admission month); a "Fui" is pending until Admin / Owner confirm it ([pending-actions](pending-actions.md)) |
| Mod, Admin, Owner | create (one date or a range) and edit a rehearsal |
| Admin, Owner | cancel / reactivate, delete, push notice, approve / remove anyone's presença, add a member |
| Current Ensaiador or Magister (position, not role; task 075) | approve or **refuse** a pending presença once the rehearsal can be approved (past, or today from 21:00) — never their own (only Admin / Owner may), nothing else of management |

**Refuse (075)**: `POST /api/rehearsals/{id}/attendances/{attendanceId}/refuse` (Admin / Owner / Ensaiador / Magister,
approvable rehearsal, pending presença). Not a deletion: the presença is kept as not attended (`WillAttend = false`,
audited) and the member gets the "Presença recusada" push, so a past "Fui" is never asked again. Removing (DELETE) stays
Admin / Owner (and one's own).
