# React Gallery (`/gallery`, React track 009)

`/gallery` is a React timeline (`portal/src/Gallery.tsx`) over a read-only, viewer-aware API. The
old Blazor `Pages/Media/Gallery.razor` mixed viewing and management; only viewing was rebuilt. The
page moved, unchanged in function, to the members' **`/member/gallery`**
(`Pages/Members/MemberGallery.razor`, `[Authorize]`): upload to R2, person tags and their push
notification, edit and delete. DEV only; no schema change.

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
- **Writes:** none in the API. Upload: any signed-in member; edit/delete: uploader or the `Admin`
  role (unchanged, on `/member/gallery`). Note: that check is `IsInRole("Admin")` only, so an Owner
  without the Admin role cannot edit others' media - recorded, not changed.
- DTOs (`DTOs/GalleryDtos.cs`) carry title, type, url, date parts, `membersOnly` and tag names; never
  the uploader, audit fields, `TakenAt`, thumbnails or EF entities. Paging: 24 by default, 1-60.
  Order: year, month, day, `TakenAt ?? CreatedAt`, then `Id`, all newest first.

## Cloudflare R2

Files are uploaded with `PublicRead` to the public bucket (`CloudflareGalleryMediaStorageService`),
so a members-only item is protected by not being listed, not by storage: anyone who already has its
URL can open it. Unchanged here (it would need private objects plus signed URLs); the API simply
never sends that URL to a visitor. URLs are absolute public URLs, so no object key or credential
reaches the browser. The page is read-only: nothing is uploaded, replaced or deleted from React,
and tests use fake URLs and never call storage. CSP already allows the R2 public origin for
`img-src` and `media-src`.

## Timeline UI

Years as large serif markers on a thin rail (sticky while scrolling), months as small caps
subheads, photos in a column masonry that keeps each photo's own proportions (no crops). Members-only
items carry a small "Membros" badge. Search by title, year filter, and for members "who appears".
"Mostrar mais" pages by 24. Lightbox: native `<dialog>`, the photo or video (`controls`) on black,
date, tagged members, "Abrir original", previous/next buttons and arrow keys, Esc. `?item=<id>` opens
one item directly (members-only ones only when signed in). Missing or broken files show a
placeholder. Visitors simply see the public gallery (no members-only teaser, 010); members get
"Carregar ou editar" → `/member/gallery`. Search and filters use the shared `.control` field.

## Follow-ups

- Management (upload, tags, push, edit, delete) in React; then `/member/gallery` can go.
- Private media as private R2 objects with signed URLs, if members-only must survive a leaked link.
- Thumbnails/responsive sizes: today the full file is the tile image (12 items, lazy-loaded).
