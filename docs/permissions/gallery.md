# Permissions: /gallery

Source: `GalleryAuthorization`. Page doc: [react-gallery](../react-gallery.md).

| Who | What |
| --- | --- |
| Visitor | public items only, no person tags |
| Any signed-in member | every item and its tags; upload (and tag people) |
| The uploader, Admin, Owner | edit and delete an item |
| Mod | no extra rights |

## Event link (077)
Uploading with `eventId` needs nothing beyond the upload right (any signed-in member); the event must exist and not be cancelled (`400` otherwise). The link grants no right on the event, and edit / delete rules are unchanged.
