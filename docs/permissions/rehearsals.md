# Permissions: /rehearsals

Source: `RehearsalsAuthorization`. Page doc: [react-rehearsals](../react-rehearsals.md).

| Who | What |
| --- | --- |
| Visitor | nothing |
| Any signed-in member | every rehearsal, who goes, their own presença, remove their own, statistics |
| Active member, on `/pending-actions` (072) | first answer to their own unanswered rehearsals, upcoming or past (current fiscal year from their admission month); a "Fui" is pending until Admin / Owner confirm it ([pending-actions](pending-actions.md)) |
| Mod, Admin, Owner | create (one date or a range) and edit a rehearsal |
| Admin, Owner | cancel / reactivate, delete, push notice, approve / remove anyone's presença, add a member |
