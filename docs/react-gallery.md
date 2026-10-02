# React Gallery (`/gallery`, React track 009)

`/gallery` is a React timeline (`portal/src/Gallery.tsx`) over a viewer-aware API. **Since 015 it is the
whole Gallery:** members upload, and the uploader, Admin or Owner edit, tag and delete, in modals on the same
page (`GalleryManage.tsx` → `Endpoints/GalleryEndpoints.cs` → `IGalleryManagementService`). The members'
Blazor `/member/gallery` (009-014) is retired: a `302` to `/gallery`. DEV only; **no schema change**.

| Route | Owner after 015 |
| --- | --- |
| `/gallery` (`?item=` opens one) | React: timeline for everyone; management for members |
| `/member/gallery` | `302` → `/gallery` (GET/HEAD only) |

## Audit

| Table / entity | Field | Type, limit, null | Real data (read-only) | Old Blazor UI | React | Visibility | Schema change |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `GalleryMedia` | `Title` | text, ≤ 200, required | 12 rows, max 30 chars | card, lightbox, search | tile, lightbox, search | as the item | no |
| `GalleryMedia` | `MediaType` | int (0 image, 1 video) | 11 images, 1 video (`.mov`) | img / video | img / video | as the item | no |
| `GalleryMedia` | `MediaUrl` | text, required | 12, all on the public R2 host, `images/Production/gallery/...` | src, download | `url` if https/same-site, else placeholder | as the item | no |
| `GalleryMedia` | `ThumbnailUrl` | text, null | 0 set (generation was never built) | video poster | not used (first frame via `#t=0.1`) | – | no |
| `GalleryMedia` | `Year`, `Month`, `Day` | int; byte 1-12; byte 1-31, null | years 2014-2026; month always set; 5 without day | grouping, order, filter | grouping, order, filter, date label | as the item | no |
| `GalleryMedia` | `TakenAt` | datetime, null | 12 null | order tie-break | order tie-break (with `CreatedAt`, then `Id`) | internal | no |
| `GalleryMedia` | `IsPrivate` | bool, default true | 3 private (2 images, the video), 9 public | anonymous filter | `membersOnly` badge; server filter | – | no |
| `GalleryMedia` | `UploaderId`, `CreatedBy`, `UpdatedBy`, audit dates | text / datetime | 2 uploaders, no orphan | edit/delete rights | never sent | internal | no |
| `GalleryMediaPersonTags` | `GalleryMediaId`, `UserId` | int, text, required | 29 tags on 4 items, 23 people | lightbox chips (everyone), person filter, push on upload | chips and filter, members only | members | no |
| `AspNetUsers` | `Nickname` / first + last name | text | – | tag names | tag names (members only) | members | no |

Verdict: **no schema change, no migration.** The real `app.db` was only read (aggregates above).

## Visibility and permissions (server-side)

- `GET /api/gallery?page=&pageSize=&year=&q=&person=&public=` and `GET /api/gallery/items/{id}`
  (`Endpoints/GalleryEndpoints.cs` → `IGalleryTimelineService`). Anonymous allowed, `no-store`,
  GET only. The session decides, never a parameter; `public=true` (the home preview, 010) can only
  narrow a member's view to the visitors' one, never widen anyone's.
- **Visitor:** `IsPrivate = false` only - in the items, the count and the year list. A members-only
  item id answers `404`, like a missing one. No person tags, no person filter (`person` is ignored).
- **Signed-in member:** every item, members-only ones flagged `membersOnly`; person tags and the
  person filter. Same rule as the Blazor page (any signed-in user); expelled members arrive
  anonymous because the cookie validator drops their session.
- **Changed on purpose:** tags were visible to everyone in the old lightbox; they are member-only now.
- **Writes (015):** upload: any signed-in member; edit/delete: the uploader, Admin or Owner (Owner now
  inherits Admin; it was `IsInRole("Admin")` only); Mod nothing extra. Enforced in `GalleryManagementService`
  (`Helpers/GalleryAuthorization.cs`); items carry `canEdit` for the caller. Antiforgery on every write.
- DTOs (`DTOs/GalleryDtos.cs`) carry title, type, url, date parts, `membersOnly` and tag names; never
  the uploader, audit fields, `TakenAt`, thumbnails or EF entities. Paging: 24 by default, 1-60.
  Order: year, month, day, `TakenAt ?? CreatedAt`, then `Id`, all newest first.

## Management (015) - audited against the old `/member/gallery`

| | Old Blazor `/member/gallery` | React now |
| --- | --- | --- |
| Where | its own page (timeline copy + modals) | `/gallery`: **Carregar foto ou vídeo** in the header (members); **Editar** in the lightbox (`canEdit`) → edit modal with **Apagar** |
| Upload | file `image/*` or `video/*` (by its type), images ≤10 MB, videos ≤100 MB (`GalleryMedia:MaxVideoSize`); title required ≤200; a date; "Visível apenas para membros" (on by default); who appears. No crop, no thumbnail | same rules, checked again on the server (`POST /api/gallery`, multipart); preview, title suggested from the file name, progress bar (XHR upload progress) |
| Date | 1 January keeps only the year; the 1st of another month the year and month; else the full date | same rule (`GalleryManagementService.ParseDate`), explained under the field; year 1900..next year |
| Storage | `images/{env}/gallery/{image\|video}/{timestamp}-{title}-{date}.ext`, `PublicRead` | unchanged (`CloudflareGalleryMediaStorageService`) |
| Tags | any user, chosen from a list of everyone | members who are not expelled, by nickname or name (`GET /api/gallery/people`); existing tags kept as they are |
| Notification | on upload only: one push to the people tagged ("Foste marcado numa nova foto ou vídeo", link `/gallery`); edits send nothing | same; the upload form says who will be notified, the edit form says nothing is sent |
| Edit | title, date, members-only, tags | same (`PUT /api/gallery/items/{id}`) |
| Delete | stored file first, then the row (hard; tags cascade); a storage error keeps the row | same (`DELETE /api/gallery/items/{id}`): a storage **error** answers 500 and keeps the row and tags (tested); a URL this environment does **not own** is refused by the storage guard with no remote call and the row is removed - the unit-029 contract, so a DEV copy of production rows never touches the production bucket. Production's rows sit under its own origin and always take the real delete path |
| Rights | UI only: uploader or `Admin` | server-side: uploader, Admin or Owner |
| Audit | none | none |

Real data (009 audit): 12 items (11 images, 1 video), 3 members-only, 2 uploaders, 29 tags on 4 items.

## Cloudflare R2

Files are uploaded with `PublicRead` to the public bucket (`CloudflareGalleryMediaStorageService`),
so a members-only item is protected by not being listed, not by storage: anyone who already has its
URL can open it. Unchanged here (it would need private objects plus signed URLs); the API simply
never sends that URL to a visitor. **Members-only is a listing rule, not storage privacy** (unchanged by
015). URLs are absolute public URLs, so no object key or credential reaches the browser. Uploads and deletes go
through the existing storage service; tests replace it with a fake (`EventsApiFactory.GalleryStorage`) and never
reach R2. CSP already allows the R2 public origin for
`img-src` and `media-src`.

## Timeline UI

Years as large serif markers on a thin rail (sticky while scrolling), months as small caps
subheads, photos in a column masonry that keeps each photo's own proportions (no crops). Members-only
items carry a small "Membros" badge. Search by title, year filter, and for members "who appears".
"Mostrar mais" pages by 24. Lightbox: native `<dialog>`, the photo or video (`controls`) on black,
date, tagged members, "Abrir original", previous/next buttons and arrow keys, Esc. `?item=<id>` opens
one item directly (members-only ones only when signed in). Missing or broken files show a
placeholder. Visitors simply see the public gallery (no members-only teaser, 010); members get
"Carregar foto ou vídeo", and whoever may edit an item gets "Editar" in its lightbox (015). Search and filters use the shared `.control` field.

## Follow-ups

- **Move private gallery media to signed/private delivery** (private R2 objects + signed URLs): today a
  members-only file is reachable by anyone who already has its URL.
- Uploads go through the app (one request, up to 100 MB); direct-to-R2 presigned uploads would spare the server.
- Thumbnails/responsive sizes: today the full file is the tile image (12 items, lazy-loaded).
