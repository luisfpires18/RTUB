# React Naipes (`/naipes`) and settings (`/naipes/config`)

Per-instrument videos and images with search, comments and play counts; Admin / Owner configure the instruments.
Code: `portal/src/Naipes.tsx`, `NaipesConfig.tsx`, `naipesApi.ts`; `Endpoints/NaipeEndpoints.cs` > `INaipeBoardService` (`NaipeBoardService`, rules in `Helpers/NaipesAuthorization.cs`) over `INaipeService` (R2 media, audit log, push to everyone on a new item) and `INaipeContentFilterService`.
Tests: `Integration.Tests/Api/NaipesApiTests.cs` (fake media storage and push), `PortalRouteTests`, `MemberPagesTests`, `MemberShellTests`.

## Routes

| Route | Who |
| --- | --- |
| `/naipes` | signed-in members, Leitões included; visitors get `302` to `/login?returnUrl=%2Fnaipes` |
| `/naipes/config` | signed-in members reach the page; Admin and Owner edit, others see a refusal; visitors `302` to sign in |

Links: member menu (`MemberShell.tsx`, Tuna group "Naipes"); `/naipes` has "Configurar" for Admin / Owner; `/naipes/config` links back.

## Permissions

| Who | Read | Add, comment | Edit / delete content | Delete a comment | Settings |
| --- | --- | --- | --- | --- | --- |
| Visitor | 401 | 401 | 401 | 401 | 401 |
| Signed-in member (Leitões included; Mod has no extra rights) | yes | yes | what they added | their own | 403 |
| Admin, Owner | yes | yes | all | all | yes |

Writes need `X-CSRF-TOKEN` (400 without). Owner inherits Admin.

## Rules

- Board (`GetAsync`): visible instruments by display order (`NaipeTypeConfigs`, one per `InstrumentType`, created on first load). `?instrument=` must be an `InstrumentType` name (400 otherwise); a hidden instrument shows nothing even by URL; `?q=` searches title and description case-insensitively. Instrument and search live in the URL so a link opens the same view. Items order by display order, then creation (`NaipeContentFilterService`); paged 10-50.
- Add (multipart): instrument required; video MP4 / M4V, MOV, WebM or 3GP by extension (or declared type when none) and container bytes; image JPEG, PNG, WebP or GIF by bytes; 100 MB / 10 MB max; title 200 max (blank becomes `<Instrumento>_<Video|Imagem>_<n>`); description 1000 max; order 0.1-999.9 (a comma counts as the decimal point). The stored type and extension are the server's, never the browser's header (SVG, HTML, renamed files refused). Then R2 upload, audit log, push to everyone.
- Edit: title required (200 max), description 1000 max, order 0.1-999.9. Delete removes the R2 file.
- Plays: videos only (400 for an image); the page counts one per opening.
- Comments: 1-1000 characters, trimmed, newest first; no author id or email in the answer.
- Settings: order 0-999; picture JPEG / PNG / WebP by type and bytes, 5 MB max, old picture deleted from R2; "Remover imagem" clears the reference only.
- Audit: create, edit, delete leave one automatic tracing entry each (`Created`, `Modified` old > new, `Deleted`). "Video Played" / "Image Viewed" stay manual; plays are `NaipePlayCounts`, excluded from the automatic audit.

## API (GET `no-store`)

| Endpoint | Does |
| --- | --- |
| `GET /api/naipes?instrument=&q=` | `{ instruments, instrument, items, totalForInstrument, instrumentOptions, nextSortOrder, canConfigure }` |
| `POST /api/naipes` | multipart `file`, `instrument`, `kind` (`video` / `image`), `title`, `description`, `sortOrder`; 201 + the item |
| `PUT /api/naipes/{id}` | `{ title, description, sortOrder }`; the item |
| `DELETE /api/naipes/{id}` | 204 |
| `POST /api/naipes/{id}/plays` | 204 |
| `GET /api/naipes/{id}/comments` | the comments |
| `POST /api/naipes/{id}/comments` | `{ text }`; the comments |
| `DELETE /api/naipes/{id}/comments/{commentId}` | the comments |
| `GET /api/naipes/config` | every instrument `{ id, instrument, label, pictureUrl, isVisible, sortOrder }` |
| `PUT /api/naipes/config/{id}` | `{ isVisible, sortOrder }` |
| `POST /api/naipes/config/{id}/picture` | multipart `picture` |
| `DELETE /api/naipes/config/{id}/picture` | |

## Data and storage

Tables: `NaipeContents`, `NaipeComments` (soft delete), `NaipePlayCounts`, `NaipeTypeConfigs`, `AuditLogs`.
R2: `naipes/{env}/videos|images/<instrument>/...` and `naipes/{env}/images/typeconfig_<instrument>/...`.

## Follow-ups

- "Remover imagem" leaves the R2 object behind.
- Media files are public-read R2 objects, like the Gallery's: members-only means "not listed", not "not reachable".
