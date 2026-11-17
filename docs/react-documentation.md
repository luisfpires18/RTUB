# React Documentação (`/documentation`)

`portal/src/Documentation.tsx` over `/api/documentation`.
Code: `Endpoints/DocumentationEndpoints.cs` -> `IDocumentationService` / `DocumentationService` over
`IDocumentStorageService`, `DocumentationAuthorization`, `DocumentTreeCache`.
Tests: `tests/RTUB.Integration.Tests/Api/DocumentationApiTests.cs` (real host; storage is the in-memory
`FakeDocumentStorage`, nothing reaches R2), `DocumentTreeCacheTests`.

Route `/documentation`: signed-in members (visitors: 302 to `/login?returnUrl=%2Fdocumentation`; Leitões: a notice, API
`403`). Menu: Gestão and the `/profile` actions. No schema: documents have no table.

## Model

Storage only, in the private R2 documents bucket. Key = `docs/{environment}/{fiscal year}/{folder}/{file}`; a folder is
a `folder/` marker object. A document has file name, extension, size. No title, description, category, visibility flag,
order column or soft delete. Delete is a hard delete in storage; the storage service writes audit log rows (`Document` /
`Folder`). No notifications.

Other writers under `docs/`: Meetings saves ata PDFs into `Atas CV {year}`, `Atas AG {year}` or `Atas {year}`.
Logística writes every board file and card attachment into `Logistics/{board folder}` of the upload's fiscal year; each
board shows here as one folder (label = board name), except a private board's folder, hidden from members off its access
list, and a Logistics folder no board owns (Mod, Admin, Owner still see both). Same object in both places: deleting a card's file here also removes it from the
card. See [react-logistics](react-logistics.md) → *Files*.

The page lists the selected fiscal year (years from 2025-2026, current by default), folders in ordinal key order,
documents by file name, search by folder and file name (client-side), folders collapse. Download opens a pre-signed URL
(60 min, `Content-Disposition: attachment`) in the same tab; no preview.

## Permissions

Server-side in `DocumentationAuthorization`. "Atas CV" = Veterano, Tunossauro or Magister (by tuna category, whatever
the role). **Only Owner sees every folder.** Role and category combine: an Admin who is a Veterano sees Atas CV and
manages; a Leitão with any role is blocked (only Owner is exempt).

| Who | Page | Normal folders | Atas AG | Atas CV | Upload new file | Replace same name | Create/delete folder, delete document |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Visitor | `401` | - | - | - | - | - | - |
| Owner | yes | yes | yes | yes | yes | yes | yes |
| Admin, Mod | yes | yes | yes | only Vet/Tuno/Magister | yes | yes | yes |
| Member (Caloiro, Tuno, ...) | yes | yes | yes | only Vet/Tuno/Magister | yes | `400` | `403` |
| Leitão (not Owner) | `403` | no | no | no | no | no | no |

- A folder or document the caller cannot see is `404`, never `403`, so existence does not leak.
- A member uploading a name already in the folder is refused; Owner, Admin, Mod replace it. Upload names lose any `/` or
  `\` path part.
- Folder names: trimmed, <= 50 chars, `^[a-zA-Z0-9\s\-]+$`, duplicate check case-insensitive, created in the current
  fiscal year. Upload types: PDF, TXT, DOC(X), XLS(X), CSV, PPT(X), <= 50 MB.
- Deleting a folder deletes every object under it.

## API

GET responses are `no-store`; writes need `X-CSRF-TOKEN`.

| Endpoint | Does |
| --- | --- |
| `GET /api/documentation?fiscalYear=` | `{ fiscalYears, fiscalYear, folders[{ name, label, documents[{ name, extension, sizeBytes }] }], extensions, maxFileBytes, canManage }` |
| `GET /api/documentation/file?fiscalYear=&folder=&name=` | `{ url }`: pre-signed attachment URL |
| `POST /api/documentation/folders` | `{ name }` -> the folder (current fiscal year) |
| `DELETE /api/documentation/folders?fiscalYear=&folder=` | the folder and everything in it |
| `POST /api/documentation/documents` | multipart `fiscalYear`, `folder`, `file` |
| `DELETE /api/documentation/documents?fiscalYear=&folder=&name=` | one document |

Storage keys never leave the server. Every key is rebuilt from a validated fiscal year (current or a listed one), a
folder from that year's visible listing and a file name from that folder's listing. `..`, `/` and unknown names are
`404`.

## Storage cache

A fiscal year's folders, Logística boards and documents come from one R2 listing of `docs/{env}/{year}/`
(`IDocumentStorageService.ListTreeAsync`, every object at any depth), held 30 s by the singleton `DocumentTreeCache`.
Page load and download cost 1 listing cold, 0 warm.

- The cache holds storage metadata only: no filtered view, authorization decision or pre-signed URL. Every request still
  resolves the caller, validates the year and keeps only the folders `CanSeeFolder` allows.
- Every writer under `docs/` drops the written year (`DocumentTreeCache.Forget(path)`): Documentação (upload, delete,
  create/delete folder), Logística board files, the Meetings ata PDF. A listing that overlapped a write is returned but
  not kept (generation check). A failed listing is never kept: the page shows no folders and downloads are `404`. A file
  written straight to R2, outside the app, appears after at most 30 s.
- Logística boards keep their own lean path (board without files, then `/file-list`).

## As minhas ações pendentes

The current Secretário (position, not role) gets the Secretaria panel on `/pending-actions`: create the current year's
missing folders, taken from last year's top-level folders, through `CreateFolderAsync` with its usual permission (Mod,
Admin, Owner) and name rules; existing folders are never touched. See [react-pending-actions](react-pending-actions.md).
