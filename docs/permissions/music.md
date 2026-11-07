# Permissions: /music

Source: `MusicAuthorization`. Page doc: [react-music](../react-music.md).

| Who | What |
| --- | --- |
| Any signed-in member who can see the album | upload videos; see song and album totals |
| Uploader, Admin, Owner | delete a video |
| Mod, Admin, Owner | create and edit albums and songs |
| Admin, Owner | delete albums and songs; per-member statistics |
| Owner only | exclusive albums and their access list |

Which albums a viewer sees (private, exclusive) is in the page doc.

Task 075: the event repertoire editor (songs on an event, `EventRepertoireAdminService`) is also open to the current
Ensaiador / Magister for upcoming events; its song picker still shows only what Music shows them (album visibility rules
unchanged). See [events](events.md).
