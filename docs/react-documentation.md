# React Documentação (`/documentation`, React track 022)

`/documentation` ("Documentação") is React (`portal/src/Documentation.tsx`) over `/api/documentation`
(`Endpoints/DocumentationEndpoints.cs` → `IDocumentationService`, over the existing `IDocumentStorageService`), replacing
the Blazor `Pages/Media/Documentation.razor`. DEV only; **no schema change** (documents have no table).

| Route | Owner after 022 |
| --- | --- |
| `/documentation` | React, signed-in members (visitors: 302 to `/login?returnUrl=%2Fdocumentation`; Leitões: a notice, API 403) |

Links: the Blazor `MainLayout` "Gestão" menu (unchanged URL) and the React `/profile` actions. The old page had no other
routes (no detail, edit or category pages), so there is nothing to redirect.

## Audit (old page, 878 lines, `[Authorize]`)

- **Model**: storage only, in the private R2 documents bucket. Key = `docs/{environment}/{fiscal year}/{folder}/{file}`;
  a folder is a `folder/` marker object. Fields per document: file name, extension, size, last modified (not shown).
  No title, description, category table, visibility flag, order column or soft delete.
- **Who writes there besides this page**: Meetings saves ata PDFs into `Atas CV {year}`, `Atas AG {year}` or
  `Atas {year}`; Logistics card attachments go to `Logistics/{board}/`. Both untouched.
- **List**: the selected fiscal year's folders (years from 2025-2026, **current by default**; "Todos os anos" built an
  empty `docs/{env}//` prefix, so it never listed anything), in storage order (ordinal key order), the `Logistics` folder
  replaced by one folder per board (label = board name); documents by file name; 3 folders / 3 documents per page; first
  folder open. No search.
- **Visibility** (page-side only): Owner sees everything; `Atas CV…` folders: Veterano / Tunossauro (by `CurrentRole`)
  or Magister; `Atas AG…`: everyone but Leitões; any other folder: every member. Leitões (not Owner) were sent to `/`.
  Admin and Mod had no extra rights.
- **Download**: same visibility check, then a pre-signed URL (60 min, `Content-Disposition: attachment`), opened in the
  same tab. No preview.
- **Upload**: "Carregar" on every folder for every member who sees it (not only Owner, despite a code comment); PDF, TXT,
  DOC(X), XLS(X), CSV, PPT(X), ≤ 50 MB; the browser file name was used as is and a same name silently overwrote.
- **Owner only** (hidden buttons, not checked in handlers): create folder (current fiscal year, `^[a-zA-Z0-9\s\-]+$`,
  50-char input limit, duplicate check), delete document, delete folder (every object under it).
- **Delete**: hard delete in storage, nothing orphaned. Audit log rows (`Document` / `Folder`) written by the storage
  service. **Notifications**: none. **Old tests**: `DocumentationPageTests` (list arithmetic only, no page), retired.

## Scope

Everything moved: list, fiscal year, download, upload, create / delete folder, delete document. Replacing a file is the
Owner uploading the same name (the old overwrite). The Blazor page is retired; no bridge. Search is new (client-side,
folder and file names); "Mostrar mais"-style paging was not needed (folders collapse instead).

## Rules now (`DocumentationAuthorization`, server-side)

| Who | List, download (visible folders) | Upload into a visible folder | Replace (same name) | Create / delete folder, delete document |
| --- | --- | --- | --- | --- |
| Visitor | `401` (page: 302 to sign in) | `401` | `401` | `401` |
| Leitão (not Owner) | `403` | `403` | `403` | `403` |
| Member | yes (`Atas CV` only for Veterano, Tunossauro, Magister) | yes | `400` | `403` |
| Admin, Mod | as a member (`Atas CV` only for Veterano, Tunossauro, Magister) | yes | yes | yes |
| Owner | yes, every folder | yes | yes | yes |

Management (folders, delete, replace) is Owner, Admin or Mod; their folder visibility is a member's.
A folder or document the caller cannot see is `404`, never `403`, so its existence does not leak.

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/documentation?fiscalYear=` | `{ fiscalYears, fiscalYear, folders[{ name, label, documents[{ name, extension, sizeBytes }] }], extensions, maxFileBytes, canManage }` |
| `GET /api/documentation/file?fiscalYear=&folder=&name=` | `{ url }`: pre-signed attachment URL |
| `POST /api/documentation/folders` | `{ name }` → the folder (current fiscal year) |
| `DELETE /api/documentation/folders?fiscalYear=&folder=` | the folder and everything in it |
| `POST /api/documentation/documents` | multipart `fiscalYear`, `folder`, `file` (≤ 50 MB) |
| `DELETE /api/documentation/documents?fiscalYear=&folder=&name=` | one document |

Storage keys never leave the server. Every key is rebuilt from a validated fiscal year (current or a listed one), a
folder from that year's visible listing and a file name from that folder's listing; the uploaded name loses any path.

## Storage round trips (#226 F01)

A fiscal year's folders, Logistics boards and documents all come from **one** R2 listing of `docs/{env}/{year}/`
(`IDocumentStorageService.ListTreeAsync`, every object at any depth), held 30 s by the singleton `DocumentTreeCache`.

| Request | Before | Now |
| --- | --- | --- |
| page load | year subfolders, then Logistics subfolders (in a row), then one listing per folder | 1 listing cold, 0 warm |
| download | year subfolders, Logistics subfolders, the folder (3 in a row), then the pre-signed URL | 1 cold, 0 warm, then the URL |
| upload / delete / folder | the same lookups, then the write | 0-1 listing, the write, then the year's listing is dropped |

- The cache holds **storage metadata only**: no user's filtered view, no authorization decision, no pre-signed URL.
  Every request still resolves the caller, validates the year and keeps only the folders `CanSeeFolder` allows.
- Download stays safe: the folder must be one of the caller's visible folders and the name a document listed directly
  in it, so the signed key is always one storage itself listed; `..`, `/` and unknown names are 404.
- Every writer under `docs/` drops the written year (`DocumentTreeCache.Forget(path)`): Documentation (upload, delete,
  create / delete folder), Logistics board files, and the Meetings ata PDF. A listing that overlapped a write is returned
  but not kept (generation check). A failed listing is never kept: the page shows no folders and downloads are 404, as
  the per-folder listings did. A file written straight to R2, outside the app, appears after at most 30 s.
- Logistics boards keep their own lean path (board without files, then `/file-list`).

## Changed on purpose

- Rules are server-side (the old page only hid buttons and filtered in the circuit).
- A member uploading a name already in the folder is refused (it used to overwrite the existing document); Owner still
  replaces. Upload names lose any `/` or `\` path part.
- Folder names are trimmed and checked server-side (≤ 50, same pattern); a duplicate is case-insensitive.
- "Todos os anos" is gone (it never listed anything). Leitões see a notice instead of being sent to `/`.

## Tests

`tests/RTUB.Integration.Tests/Api/DocumentationApiTests.cs`: real host, login, antiforgery, SQLite; storage is the
in-memory `FakeDocumentStorage` (nothing reaches R2). Covers visitors, Leitões, folder visibility per role, order, the
response shape (no keys), fiscal years, download, upload rules, Owner management and the retired Blazor route, and
(#226 F01) the listing counts: one per year, reused across callers without widening what a member sees, dropped by each
write and per year, not kept on failure (`DocumentTreeCacheTests` for the cache itself).

## Follow-ups

- `FolderCard` and `DocumentCard` (Shared) and their bUnit tests have no page caller since the page went; the global
  `.folder-card` / `.document-card` CSS is unused.
