# Permissions: /events

Source: `EventsAuthorization`. Page doc: [react-events](../react-events.md).

| Who | What |
| --- | --- |
| Visitor | the public agenda only (no descriptions, counts, repertoire, enrollments) |
| Any signed-in member | member view, who is going, their own enrollment |
| Active member, on `/pending-actions` (072) | first answer to their own unanswered events: upcoming Vou / Não vou, past (current fiscal year from their admission month, not cancelled) Fui / Não fui; never changes an existing answer ([pending-actions](pending-actions.md)) |
| Mod, Admin, Owner, and any member whose categories include Leitão (049) | track who was contacted (`/events/{id}/contacts`): read the list and record / reset calls |
| Mod, Admin, Owner | create and edit an event (fields and image; the form's type list comes with the agenda) |
| Admin, Owner | delete, cancel / reactivate, notices, prizes, videos, repertoire; add or remove others' enrollments |
| Current Ensaiador or Magister (position, not role; task 075) | the repertoire of an upcoming, not cancelled event (the same editor, `canManageRepertoire`) — no other event management |
| Owner only | delete anyone's discussion posts and comments |
