# Permissions: /events

Source: `EventsAuthorization`. Page doc: [react-events](../react-events.md).

| Who | What |
| --- | --- |
| Visitor | the public agenda only (no descriptions, counts, repertoire, enrollments) |
| Any signed-in member | member view, who is going, their own enrollment |
| Mod, Admin, Owner, and any member whose categories include Leitão (049) | track who was contacted (`/events/{id}/contacts`): read the list and record / reset calls |
| Mod, Admin, Owner | create and edit an event (fields and image; the form's type list comes with the agenda) |
| Admin, Owner | delete, cancel / reactivate, notices, prizes, videos, repertoire; add or remove others' enrollments |
| Owner only | delete anyone's discussion posts and comments |
