# React Naipes (`/naipes`) and their settings (`/naipes/config`), task 033

Both pages are React (`portal/src/Naipes.tsx`, `portal/src/NaipesConfig.tsx`, client `portal/src/naipesApi.ts`) over
`Endpoints/NaipeEndpoints.cs` → `INaipeBoardService` (`NaipeBoardService`, rules in `Helpers/NaipesAuthorization.cs`),
which keeps the old `INaipeService` underneath (R2 media storage, audit log, push to everyone on a new item) and the old
`INaipeContentFilterService`. They replace the Blazor `Pages/Activities/Naipes.razor` and `NaipesConfig.razor`. DEV
only; **no schema change**, no migration.

| Route | Owner after 033 |
| --- | --- |
| `/naipes` | React, signed-in members (visitors: 302 to `/login?returnUrl=%2Fnaipes`); Leitões included |
| `/naipes/config` | React, signed-in members (visitors: 302 to sign in); Admin and Owner edit, any other member reads a refusal |

Links: the member menu (`MemberShell.tsx`, Tuna group: "Naipes"); `/naipes` → "Configurar" for Admin / Owner;
`/naipes/config` → back to `/naipes`. The Blazor `MainLayout` links `/naipes` as a plain full-page link.

## Audit (old pages)

**`/naipes`** (809 lines + `NaipeCard`, `NaipeCommentItem`, `DetailsModal`, `InfoSection`, `ProfileField`,
`[Authorize]`):
- The visible instruments (`NaipeTypeConfigs`, created for every `InstrumentType` on first load, by display order) as a
  picker with each picture; nothing listed until one is picked.
- The picked instrument's videos and images, searched over title and description (case-insensitive), ordered by
  display order then creation (`NaipeContentFilterService`); 10 / 20 / 30 / 40 / 50 per page.
- A card per item (video preview or image, title, description, plays, comments, author); edit / delete shown to the
  author or an Admin.
- Detail modal: the player or the image, description, plays and author, and the comments. Any member comments; the
  author or an Admin deletes a comment. A video play counted once per modal opening.
- "Adicionar vídeo" / "Adicionar imagem": instrument (the picked one or Guitarra), file (`video/*` up to 100 MB,
  `image/*` up to 10 MB, size checked in the browser only), title (a blank one became `<Instrumento>_<Video|Imagem>_<n>`),
  description, order (max + 1). Saved through `NaipeService.CreateContentAsync`: R2 upload, audit log, push to
  everyone. Delete removes the R2 file.
- Rules lived in the page (buttons hidden) and `NaipeService` (author or `isAdmin`, which the page set from the Admin
  role only, so an Owner without Admin was a plain member).

**`/naipes/config`** (337 lines, `[Authorize(Roles = "Admin")]`): every instrument as a card (picture, name, order,
Visível / Oculto); an edit modal with picture upload (JPG / PNG / WebP up to 5 MB, the old picture deleted from R2),
"Remover imagem" (clears the reference), order 0-999 and visibility.

## Rules now (server-side)

| Who | Read `/naipes` | Add, comment | Edit / delete content | Delete a comment | Settings |
| --- | --- | --- | --- | --- | --- |
| Visitor | 401 | 401 | 401 | 401 | 401 |
| Any signed-in member (Leitões included) | yes | yes | what they added | their own | 403 |
| Admin, Owner | yes | yes | all | all | yes |

Writes need `X-CSRF-TOKEN` (400 without).

- **Board** (`GetAsync`): the visible instruments by display order; `?instrument=` must be an `InstrumentType` name
  (400 otherwise); a hidden instrument shows nothing, as the old picker never offered it; `?q=` searches as before.
- **Add** (`CreateAsync`, multipart): instrument required; a video must be MP4 / M4V, MOV, WebM or 3GP by its
  extension (or, without one, its declared type) **and** its container bytes; an image must be JPEG, PNG, WebP or GIF
  by its bytes; at most 100 MB / 10 MB; title ≤ 200 (blank → the old default name), description ≤ 1000, order
  0.1-999.9 (a comma counts as the decimal point). The **stored type and file extension are the server's**, never the
  browser's header. Then the old path: R2, audit log, push to everyone.
- **Edit**: title required (≤ 200), description ≤ 1000, order 0.1-999.9. **Delete**: the R2 file goes with it.
- **Plays** (`POST /{id}/plays`): videos only (400 for an image); the page counts one per opening, as before.
- **Comments**: 1-1000 characters, trimmed, newest first; no author id or email in the answer.
- **Settings**: order 0-999; picture JPEG / PNG / WebP by type **and** bytes, ≤ 5 MB, the old picture deleted from
  R2; "Remover imagem" clears the reference only, as before.

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/naipes?instrument=&q=` | `{ instruments, instrument, items, totalForInstrument, instrumentOptions, nextSortOrder, canConfigure }` |
| `POST /api/naipes` | multipart `file`, `instrument`, `kind` (`video` / `image`), `title`, `description`, `sortOrder` → 201 + the item |
| `PUT /api/naipes/{id}` | `{ title, description, sortOrder }` → the item |
| `DELETE /api/naipes/{id}` | → 204 |
| `POST /api/naipes/{id}/plays` | → 204 |
| `GET /api/naipes/{id}/comments` | → the comments |
| `POST /api/naipes/{id}/comments` | `{ text }` → the comments |
| `DELETE /api/naipes/{id}/comments/{commentId}` | → the comments |
| `GET /api/naipes/config` | → every instrument's setting `{ id, instrument, label, pictureUrl, isVisible, sortOrder }` |
| `PUT /api/naipes/config/{id}` | `{ isVisible, sortOrder }` → the settings |
| `POST /api/naipes/config/{id}/picture` | multipart `picture` → the settings |
| `DELETE /api/naipes/config/{id}/picture` | → the settings |

## Changed on purpose

- **Owner inherits Admin** (content, comments and settings), as everywhere on the React track; before, only the Admin
  role counted.
- **Rules are enforced by the server**: file type and size, lengths and order are checked there (before: the browser
  size check and the entity annotations); a refusal comes with a message (before: `alert()`s or nothing).
- **Uploads are an allowlist checked by bytes**, and the stored type is the server's: SVG, HTML or a renamed file are
  refused (before: any `video/*` or `image/*` header was stored as sent). MKV and AVI are no longer accepted (browsers
  do not play them); GIF images are.
- **A hidden instrument stays hidden** even when asked for by URL. The picked instrument and the search are in the URL
  (`?instrument=&q=`), so a link opens the same view.
- After a save, the board shows the page holding that item (a new one goes last, by its order).
- `/naipes/config` is open to every signed-in member as a page; non-managers get a clear refusal (before: the Blazor
  access-denied redirect).

## Retired

Blazor: `Naipes.razor`, `NaipesConfig.razor` (+ CSS); `NaipeCard`, `NaipeCommentItem`, `DetailsModal`, `InfoSection`,
`ProfileField` (+ CSS); `css/3-components/naipe-card.css`, `css/3-components/details-modal.css`,
`css/4-pages/naipes-config.css` (and their `site.css` imports). Tests: their bUnit tests (`NaipesPageTests`,
`NaipesConfigPageTests`, `DetailsModalTests`, `ProfileFieldTests`). Kept: `INaipeService` / `NaipeService` (+ tests),
`INaipeContentFilterService`, the entities, repositories and R2 storage service.

## Data audit

No schema change. Writes touch `NaipeContents`, `NaipeComments` (soft delete, as before), `NaipePlayCounts`,
`NaipeTypeConfigs` and `AuditLogs` (as before); R2: `naipes/{env}/videos|images/<instrument>/…` and
`naipes/{env}/images/typeconfig_<instrument>/…`, written and deleted exactly as before.

## Tests

`Integration.Tests/Api/NaipesApiTests.cs` (fake media storage and push): visitors (401 on every endpoint, 302 on both
pages), the React pages for members, antiforgery, the board (visible instruments, nothing until picked, invalid
instrument), sorting and search, adding (stored as before, push, default titles, server-decided type), upload
validation (empty, type, bytes, SVG, HTML, sizes, lengths, order), edit / delete permissions (author, other member,
Admin, Owner; R2 delete), comments (validation, who deletes, wrong item, no ids), plays, settings (403 for members,
Admin / Owner read, order range, hide / show and the board following, picture type / bytes / size and old-picture
deletion) and the retirement sweep. Route ownership in `PortalRouteTests` and `MemberPagesTests`; menu contract in
`MemberShellTests`.

## Follow-ups (not done here)

- "Remover imagem" leaves the R2 object behind (as before).
- `INaipeAuthorizationService` / `NaipeAuthorizationService` and `INaipeConfigService` / `NaipeConfigService` are still
  registered but have no caller now.
- The `.modal-info-section--danger` rule (`css/9-overrides/dynamic-style-classes.css`) had no user even before.
- Media files are public-read R2 objects, as the Gallery's: members-only means "not listed", not "not reachable".
