# React Loja (`/shop`, React track 021)

`/shop` ("Loja RTUB") is React (`portal/src/Shop.tsx`) over `/api/shop` (`Endpoints/ShopEndpoints.cs` →
`IProductShopService`, over the existing `IProductService` and `IProductReservationService`), replacing the Blazor
`Pages/Inventory/Shop.razor`. Not the MyTuno in-game shop (`IShopService`, untouched). DEV only; **no schema change**.

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

## Follow-ups

- "Público (não membros)" only decides whether a product can be reserved; visitors still see no shop (unchanged).
- `ReservationCard` (Shared) has no caller since the page went.
