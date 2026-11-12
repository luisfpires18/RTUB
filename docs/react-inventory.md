# React Inventário: Instrumentos (`/inventory`)

The tuna's instruments: `portal/src/Inventory.tsx` over `/api/inventory` (`Endpoints/InventoryEndpoints.cs` > `IInstrumentInventoryService`, over `IInstrumentService`). Table `Instruments`, entity `Instrument`; no quantity, owner, loans, reservations or history.
Code: `Helpers/InventoryAuthorization.cs`. The shop is a separate module: [react-shop](react-shop.md).

| Route | Who |
| --- | --- |
| `/inventory` | signed-in members; visitors `302` to `/login?returnUrl=%2Finventory` |

## Data

Name (required, 100 max), type (`Category`, stored as an `InstrumentType` name, required, 50 max), brand (100), serial number (100), condition (`InstrumentCondition`: Óptimo, Bom, Velho, Precisa Manutenção, Perdido), location (200), last maintenance date, maintenance notes (500), image and thumbnail URLs.

- The type is fixed after creation: `Instrument.Update` ignores it and the edit form shows it read-only. The UI shows display names ("Acordeão"); the stored value is the enum name.
- Counters (total, then the first three conditions present in enum order) cover every instrument. List ordered by name; "Mostrar mais" adds 12 at a time. Search: name, stored type, brand, case-insensitive; filters: type, condition.
- Blank optional fields are stored as null.
- Image: original (10 MB max) to R2 `.../instruments` plus a cropped 1:1 WebP thumbnail to `.../instruments/thumbnails`; WebP, JPEG or PNG only, checked by content. A new image replaces both. A new instrument is saved first, its image right after. Images stay in the public R2 bucket.
- Delete is a hard delete and removes the image from R2 (not the thumbnail).

## Permissions

Enforced server-side by `InventoryAuthorization`.

| Who | List, counters, details | Create, edit, image, delete |
| --- | --- | --- |
| Visitor | `401` | `401` |
| Member | yes | `403` |
| Mod, Admin, Owner | yes | yes |
| Leitão (any role) | yes | yes (by category; keeping the inventory up to date is their job) |

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/inventory?q=&category=&condition=` | `{ total, stats, instruments, categories, conditions, canManage }`; unknown filter 400 |
| `GET /api/inventory/{id}` | the details; 404 when unknown |
| `POST /api/inventory` / `PUT /api/inventory/{id}` | create (201) / edit (type ignored); 400 with per-field errors |
| `POST /api/inventory/{id}/image` | multipart `image` + `thumbnail` (WebP, JPEG or PNG, 10 MB each max), replaces both |
| `DELETE /api/inventory/{id}` | 204 |

Tests use the recording storage fake; nothing reaches R2.

## Follow-ups

- Deleting an instrument leaves its thumbnail in R2.
