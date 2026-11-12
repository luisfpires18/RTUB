# React Gallery (`/gallery`)

`/gallery` is the React timeline (`src/RTUB.Web/portal/src/Gallery.tsx`) over a viewer-aware API. Members upload. The uploader,
Admin or Owner edit, tag and delete, in modals on the same page (`GalleryManage.tsx`).

Code: `Endpoints/GalleryEndpoints.cs` → `IGalleryTimelineService` (reads), `IGalleryManagementService` (writes, `Services/GalleryManagementService.cs`),
`Helpers/GalleryAuthorization.cs`, `Services/CloudflareGalleryMediaStorageService.cs`.

## Routes

| Route | Notes |
| --- | --- |
| `/gallery` | Timeline for everyone; management for members. `?item=<id>` opens one item. `?upload=1&event=&date=&title=` opens the upload prefilled; the query is dropped after reading. |
| `/member/gallery` | `302` to `/gallery` (GET and HEAD only). |

## Data

No schema change in the current rules.

- `GalleryMedia`: `Title` (required, ≤200), `MediaType` (0 image, 1 video), `MediaUrl` (required, public R2 URL), `Year` (required),
  `Month` (1-12, required), `Day` (1-31, optional), `TakenAt` (optional; order tie-break), `IsPrivate` (default true; means members-only),
  `UploaderId`, `EventId` (optional link, no FK; deleting the event clears it), audit fields.
  `ThumbnailUrl` is not used: no thumbnails are generated, and tiles use the file itself.
- `GalleryMediaPersonTags`: `GalleryMediaId`, `UserId` (required). Cascades with the media.
- Tag names come from `AspNetUsers` (nickname, or first and last name). Members only.

## Visibility

- The session decides, never a parameter. `public=true` (the home preview) can only narrow a member's view to the visitors' one.
- **Visitor:** `IsPrivate = false` items only, in the items, the count and the year list. A members-only item id answers `404`, like a missing one.
  No person tags and no person filter (`person` is ignored).
- **Signed-in member:** every item; members-only ones are flagged `membersOnly`. Person tags and the person filter. Tags are not visible to visitors.
- Expelled members arrive as anonymous, because the cookie validator drops their session.
- Members-only is a listing rule, not storage privacy. Files are `PublicRead`, so anyone who already has an item's URL can open it.
  The API never sends that URL to a visitor.

## Permissions

| Who | What |
| --- | --- |
| Visitor | Public items only, no person tags. |
| Any signed-in member | Every item and its tags. Upload, and tag people. |
| The uploader, Admin, Owner | Edit and delete an item. Owner inherits Admin. Items carry `canEdit` for the caller. |
| Mod | No extra rights. |

Upload with `eventId` needs only the upload right. The event must exist and not be cancelled (`400` `eventId` otherwise). The link grants no right on the event.

## API

Writes need `X-CSRF-TOKEN`. Reads are GET only, `no-store`.

| Endpoint | Who | Notes |
| --- | --- | --- |
| `GET /api/gallery?page=&pageSize=&year=&q=&person=&public=` | anyone | Paging: 24 by default, 1-60. Order: year, month, day, `TakenAt ?? CreatedAt`, then `Id`, all newest first. |
| `GET /api/gallery/items/{id}` | anyone | `404` for a members-only item to a visitor. |
| `POST /api/gallery` | member | Multipart upload; optional `eventId` form field. |
| `PUT /api/gallery/items/{id}` | uploader, Admin, Owner | Title, date, members-only flag, tags. |
| `DELETE /api/gallery/items/{id}` | uploader, Admin, Owner | Stored file first, then the row. |
| `GET /api/gallery/people` | member | Members who are not expelled, by nickname or name, for tagging. |

DTOs (`DTOs/GalleryDtos.cs`) carry title, type, url, date parts, `membersOnly`, tag names and `canEdit`. They never carry the uploader,
audit fields, `TakenAt`, thumbnails or entities.

## Upload and edit

- **Upload:** file `image/*` or `video/*`, by its type. Images ≤10 MB; videos ≤100 MB (`GalleryMedia:MaxVideoSize`). Title required, ≤200.
  Date. "Visível apenas para membros" (on by default). Who appears (tags). No crop, no thumbnail. The server checks the same rules again.
  Preview; title suggested from the file name; progress bar from the upload.
- **Date rule** (`GalleryManagementService.ParseDate`, explained under the field): 1 January keeps only the year; the 1st of another month keeps year and month;
  otherwise the full date. Year 1900 to next year.
- **Storage key:** `images/{env}/gallery/{image|video}/{timestamp}-{title}-{date}.ext`, `PublicRead`.
- **Tags:** members who are not expelled, by nickname or name. Existing tags are kept as they are.
- **Notification:** on upload only, one push to the people tagged ("Foste marcado numa nova foto ou vídeo", link `/gallery`).
  Edits send nothing. The upload form says who will be notified; the edit form says nothing is sent.
- **Edit:** title, date, members-only flag, tags.
- **Delete:** stored file first, then the row (hard; tags cascade). A storage error answers `500` and keeps the row and tags.
  A URL this environment does not own is refused by the storage guard with no remote call, and the row is removed. A DEV copy of production rows
  therefore never touches the production bucket. Production's own rows take the real delete path.
- **Event link:** items uploaded with an event are used by `/pending-actions` → Galeria ([react-pending-actions](react-pending-actions.md)). Older items are not linked.

## Timeline UI

- Years as large serif markers on a sticky rail; months as small subheads; photos in a column masonry that keeps each photo's own proportions (no crops).
- Members-only items carry a small "Membros" badge.
- Search by title, year filter and, for members, "who appears".
- "Mostrar mais" loads 24 at a time.
- Lightbox: native `<dialog>`, the photo or video (`controls`) on black, date, tagged members, "Abrir original", previous and next buttons and arrow keys, Esc.
  `?item=<id>` opens one directly; members-only items only when signed in.
- Missing or broken files show a placeholder.
- Members get "Carregar foto ou vídeo" in the header. Whoever may edit an item gets "Editar" in its lightbox, which opens the edit modal with **Apagar**.
- Visitors see the public gallery only.

## Cloudflare R2

- Uploads and deletes go through `CloudflareGalleryMediaStorageService`. Tests replace it with a fake (`EventsApiFactory.GalleryStorage`) and never reach R2.
- Objects are `PublicRead`. URLs are absolute public URLs, so no object key or credential reaches the browser.
- CSP allows the R2 public origin for `img-src` and `media-src`.

## Follow-ups

- Move private gallery media to signed or private delivery (private R2 objects and signed URLs). Today a members-only file is reachable by anyone who has its URL.
- Uploads go through the app (one request, up to 100 MB). Direct-to-R2 presigned uploads would spare the server.
- Thumbnails and responsive sizes: today the full file is the tile image, lazy-loaded.
