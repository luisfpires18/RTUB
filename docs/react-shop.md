# React Loja (`/shop`, React track 021)

`/shop` ("Loja RTUB") is React (`portal/src/Shop.tsx`) over `/api/shop` (`Endpoints/ShopEndpoints.cs` →
`IProductShopService`, over the existing `IProductService` and `IProductReservationService`), replacing the Blazor
`Pages/Inventory/Shop.razor`. Not the MyTuno in-game shop (`IShopService`, removed with MyTuno in 026). No schema change in 021;
#221 added `Products.AvailableAllYears` (below).

| Route | Owner after 021 |
| --- | --- |
| `/shop` | React, signed-in members (visitors: 302 to `/login?returnUrl=%2Fshop`) |

Links: the Blazor `MainLayout` "Inventário" menu (Loja) and the React `/profile` actions.

## Audit (old page, 1232 lines, `[Authorize]`)

- **Products** (`Products`, `Product`): name (required, ≤ 200), type (free text, required, ≤ 50), description (≤ 1000),
  price (> 0), stock (≥ 0), `IsAvailable` (set from the stock on every save), `IsPublic` ("Público (não membros)",
  default on), one image (`ImageUrl`). No variants, quantity or hidden flag.
- **List**: every product to any signed-in member (the page required sign-in, so "public" never reached visitors), by
  type then name; fiscal-year filter on the product's creation date (1 September - 31 August, years from 2025-2026;
  **the current year by default**, "Todos os anos" optional), search (name, type; case-insensitive), type filter (the
  distinct types), 6 per page. Card: image, name, type, "N em stock" / "Esgotado", "Apenas Membros", price.
- **Details**: name, type, price, stock, "Disponível", visibility, description.
- **Reservations** (`ProductReservations`, `ProductReservation`; unique per product and member, cascade on product and
  member delete): offered only on **members-only products in stock**; one per member and product; the member ticks
  "Este produto tem tamanhos" (then a size XS-XXXL is required) and may give a display name; the username is stored.
  Reserving never changes the stock. "Ver Reserva" / "Anular" for the member's own; Admin's "Ver Reservas" on
  members-only products lists every reservation (username, display name, size, date, newest first) with delete. No
  status, quantity, notes or fiscal year on a reservation.
- **Management** (UI-only checks): "Adicionar Produto" for Admin and Mod; edit, delete and the reservation lists for
  Admin. Image: cropped square WebP to R2 `…/products`; a new image deletes the previous one; deleting a product
  deletes its image (and, by cascade, its reservations).
- **Notifications**: none. **Old tests**: bUnit `ShopPageTests` (rendering only, retired with the page).

## Scope

Everything moved: list, details, reservations, own cancellation, Admin reservation lists, product create / edit /
delete, image. The Blazor page is retired; no bridge.

## Rules now (`ShopAuthorization`, server-side)

| Who | Products, details, reserve, cancel own | Add product (+ its first image) | Edit, delete, change image, every reservation |
| --- | --- | --- | --- |
| Visitor | `401` (page: 302 to sign in) | `401` | `401` |
| Member | yes | `403` | `403` |
| Mod | yes | yes | `403` |
| Admin, Owner | yes | yes | yes (Owner inherits Admin) |

Reserving is refused server-side on public or sold-out products, without a size when sizes are ticked, with a size off
the list, a display name over 200 characters, or a second reservation.

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/shop?fiscalYear=&q=&type=` | `{ fiscalYears, fiscalYear, types, products, sizes, canCreate, canManage }`; no `fiscalYear` = the current year, empty = every year |
| `GET /api/shop/products/{id}` | details |
| `POST /api/shop/products` / `PUT` / `DELETE /api/shop/products/{id}` | create (Mod+) / edit, delete (Admin, Owner) |
| `POST /api/shop/products/{id}/image` | multipart `image` (WebP, JPEG or PNG, ≤ 5 MB, cropped square) |
| `POST /api/shop/products/{id}/reservations` | `{ hasSizes, size, displayName }` → the reservation |
| `GET /api/shop/products/{id}/reservations` | every reservation of the product (Admin, Owner); username, never the member id |
| `DELETE /api/shop/reservations/{id}` | own, or any for Admin / Owner |

Tests use the recording storage fake only; nothing reaches R2. Product images stay in the public R2 bucket, as before.

## Changed on purpose

- The member's own reservation stays visible and cancellable when the product sells out (the old page hid both
  buttons at stock 0).
- A Mod can give the product they add its first image, but not change images afterwards (editing was Admin only).
- Images are checked by content (WebP, JPEG or PNG); "Mostrar mais" (12 at a time) instead of numbered pages; a new
  product is saved first and its image right after.

## Products in every fiscal year (#221)

`Products.AvailableAllYears` (migration `AddProductAvailableAllYears`, `INTEGER NOT NULL DEFAULT 0`). A product has no
fiscal-year column: a **year-specific** product belongs to the fiscal year of its `CreatedAt` (1 September - 31 August),
unchanged; an **all-years** product (permanent stock, e.g. CDs) is shown in every year. No sentinel year.

- **One rule**: `ProductShopService.InFiscalYear` - "" (Todos os anos) shows every product, a year shows the all-years
  products and those created in it. Stock, `IsAvailable` (follows the stock), `IsPublic`, permissions, price and
  ordering are unchanged and apply to both kinds; a sold-out all-years product shows "Esgotado" and is not reservable.
- **API**: `ShopProductInput.availableAllYears` (`bool?`: absent = year-specific on create, unchanged on edit); cards and
  details return `availableAllYears`; details also return `fiscalYear`, the creation year a year-specific product is
  shown in.
- **UI**: one checkbox "Disponível em todos os anos letivos" (off for a new product); its hint names the year the
  product falls back to when off ("Aparece só no ano letivo 2025-2026, em que foi criado."). Turning it off always lands
  on a real year (the creation year is immutable), so there is no invalid "no year" state and no hidden year field.
  Admin / Owner cards show a "Todos os anos" badge; members see the year in "Detalhes do produto" only.
- **History**: reservations (the shop's only order record; no cart, checkout, order lines or receipts exist) store the
  product id, username, size, display name and date, never the year, name or price, so the option cannot change their
  meaning. Pre-existing: a reservation snapshots no product name or price, and deleting a product deletes its
  reservations (cascade) - unchanged, out of #221's scope.
- **Migration / rollback**: existing products and any product N-1 inserts after an app rollback stay year-specific
  (default 0); nothing else changes. App rollback to N-1: the column is ignored, so all-years products show only in their
  creation year until the release is back. `Down` drops the column (loses only the flag).

## Follow-ups

- "Público (não membros)" only decides whether a product can be reserved; visitors still see no shop (unchanged).
- `ReservationCard` (Shared) has no caller since the page went.

## Delete warning (#226 F20)

The shop list carries `reservationCount` for Admin / Owner (who can delete; one grouped count query) and `null` for
everyone else - a number only, never who reserved. "Eliminar produto" then adds "Este produto tem 1 reserva, que também
é apagada." / "Este produto tem N reservas, que também são apagadas." when there are any (nothing extra at zero). The
delete itself is unchanged: confirmation required, image deleted, reservations removed by cascade.
