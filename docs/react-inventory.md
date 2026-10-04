# React Inventário: Instrumentos (`/inventory`, React track 020)

`/inventory` ("Instrumentos") is React (`portal/src/Inventory.tsx`) over `/api/inventory`
(`Endpoints/InventoryEndpoints.cs` → `IInstrumentInventoryService`, over the existing `IInstrumentService`), replacing
the Blazor `Pages/Inventory/Inventory.razor`. DEV only; **no schema change**, no migration.

| Route | Owner after 020 |
| --- | --- |
| `/inventory` | React, signed-in members (visitors: 302 to `/login?returnUrl=%2Finventory`) |
| `/shop` ("Loja RTUB") | a separate module, React since 021: `docs/react-shop.md` |

Links: the Blazor `MainLayout` "Inventário" menu (Instrumentos → `/inventory`, Loja → `/shop`) and the React
`/profile` actions.

## Audit

The "Inventário" menu has two pages:

- **`/inventory`** (`Inventory.razor`, 749 lines, `[Authorize]`): the tuna's instruments (`Instruments` table,
  `Instrument` entity). Fields: name (required, ≤ 100), type (`Category`, stored as an `InstrumentType` name, required,
  ≤ 50), brand (≤ 100), serial number (≤ 100), condition (`InstrumentCondition`: Óptimo, Bom, Velho, Precisa
  Manutenção, Perdido), location (≤ 200), last maintenance date, maintenance notes (≤ 500), image and thumbnail URLs.
  No quantity, owner/responsible member, loans, requests, reservations, barcode or history.
  - Any member: the list (by name, 6 per page), counters (total, then the first three conditions present in enum
    order, over every instrument), search (name, stored type, brand; case-insensitive), type and condition filters,
    the details modal (every field, notes included).
  - "Admin,Mod" (UI only, `AuthorizeView`): add, edit, delete. Edit never changed the type (`Instrument.Update`
    ignores it, although the form offered it). Image: the original file (≤ 10 MB) to R2 `…/instruments` plus a cropped
    1:1 WebP thumbnail to `…/instruments/thumbnails`; a new image deleted the previous image and thumbnail. Delete: hard
    delete that also deletes the image from R2, but not the thumbnail. No notifications. Old tests: bUnit
    `InventoryPageTests` (rendering only, retired with the page); `InstrumentServiceTests` unchanged.
- **`/shop`** (`Shop.razor`, 1232 lines): "Loja RTUB", the products (`Product`: price, stock, public/members-only, type,
  image) and members' **reservations** (`ProductReservation`: size, display name; own cancel; Admin views and deletes
  every reservation), filtered by fiscal year.

## Scope decision

- **Moved**: all of `/inventory` (list, counters, filters, details, create, edit, image, delete). The Blazor page is
  retired; no duplicate UI is left.
- **Not moved**: `/shop`. It is a shop with its own reservation workflow (products, stock, sizes, per-member
  reservations, Admin reservation lists), not the instruments inventory; it needs its own task. It stays Blazor,
  reachable from the same menu.

## Rules now (`InventoryAuthorization`, server-side)

| Who | List, counters, details | Create, edit, image, delete |
| --- | --- | --- |
| Visitor | `401` (page: 302 to sign in) | `401` |
| Member | yes | `403` |
| Mod, Admin, Owner | yes | yes (Owner inherits Admin) |

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/inventory?q=&category=&condition=` | `{ total, stats, instruments, categories, conditions, canManage }`; unknown filter → 400 |
| `GET /api/inventory/{id}` | the details; 404 when unknown |
| `POST /api/inventory` / `PUT /api/inventory/{id}` | create (201) / edit (type ignored); 400 with per-field errors |
| `POST /api/inventory/{id}/image` | multipart `image` + `thumbnail` (WebP, JPEG or PNG, ≤ 10 MB each), replaces both |
| `DELETE /api/inventory/{id}` | 204; the old hard delete (image deleted, thumbnail left) |

Tests use the recording storage fake only; nothing reaches R2. Images stay in the public R2 bucket as before.

## Changed on purpose

- Types show their display names ("Acordeão") instead of the stored enum names; the stored value is unchanged.
- The edit form shows the type as fixed (the old form offered a change that was never saved).
- A new instrument is saved first and its image right after (the old page uploaded first); the image accepts WebP,
  JPEG or PNG only, checked by content.
- "Mostrar mais" (12 at a time) instead of numbered pages; blank optional fields are stored as empty (null).

## Follow-ups

- (021) `/shop` (Loja + reservations) is React: `docs/react-shop.md`.
- Deleting an instrument leaves its thumbnail in R2 (pre-existing).
- `InstrumentCircle` (Shared) and its CSS have no caller since the page went.
