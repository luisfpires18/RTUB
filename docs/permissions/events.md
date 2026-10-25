# Permissions: /events

Source: `EventsAuthorization`. Page doc: [react-events](../react-events.md).

| Who | What |
| --- | --- |
| Visitor | the public agenda only (no descriptions, counts, repertoire, enrollments) |
| Any signed-in member | member view, who is going, their own enrollment |
| Mod, Admin, Owner | track who was contacted (`/events/{id}/contacts`) |
| Admin, Owner | manage events, add or remove others' enrollments |
| Owner only | delete anyone's discussion posts and comments |
