# React Loja (`/shop`)

"Loja RTUB": `portal/src/Shop.tsx`, `ShopPromotions.tsx` over `/api/shop` (`Endpoints/ShopEndpoints.cs` > `IProductShopService`, over `IProductService` and `IProductReservationService`; `IShopPromotionService`). Not the removed MyTuno in-game shop.
Code: `Helpers/ShopAuthorization.cs`, `ProductShopService`, `ShopPromotionService`.

| Route | Who |
| --- | --- |
| `/shop` | open page; visitors get the public catalogue read-only (`GET /api/shop` and `/promotions` allow anonymous); member actions are service-authorized |

## Data

- `Products`: name (required, 200 max), type (free text, required, 50), description (1000), price (> 0), stock (>= 0), `IsAvailable` (set from stock on every save), `IsPublic` ("Público (não membros)", default on), one image (`ImageUrl`), `AvailableAllYears`.
- `ProductReservations`: unique per product and member, cascade on product and member delete. Stores product id, username, size, display name, date; no status, quantity or fiscal year. Reserving never changes stock. A reservation snapshots no product name or price.
- List: by type then name; search (name, type, case-insensitive); type filter; "Mostrar mais" 12 at a time. Image: cropped square WebP to R2 `.../products`, 5 MB max, WebP / JPEG / PNG by content; a new image deletes the previous one; a new product is saved first and its image right after. Deleting a product deletes its image and, by cascade, its reservations.
- "Público (não membros)" decides visibility to visitors; reservations are offered only on members-only products in stock.
- A member's own reservation stays visible and cancellable after the product sells out.

## Fiscal years

Fiscal year runs 1 September to 31 August, years from 2025-2026; `?fiscalYear=` absent = the current year, empty = every year ("Todos os anos").

`Products.AvailableAllYears` (migration `AddProductAvailableAllYears`, `INTEGER NOT NULL DEFAULT 0`): a year-specific product belongs to the fiscal year of its `CreatedAt`; an all-years product (permanent stock, e.g. CDs) shows in every year. No sentinel year.

- One rule, `ProductShopService.InFiscalYear`: "" shows every product, a year shows all-years products and those created in it. Stock, permissions, price and ordering apply to both; a sold-out all-years product shows "Esgotado" and is not reservable.
- API: `ShopProductInput.availableAllYears` (`bool?`: absent = year-specific on create, unchanged on edit); cards and details return it; details also return `fiscalYear` (the creation year).
- UI: checkbox "Disponível em todos os anos letivos" (off for a new product), with a hint naming the fallback year. Admin / Owner cards show a "Todos os anos" badge.
- Rollback: existing products and any inserted by the previous app version stay year-specific; the column is ignored by it. `Down` drops the column (loses only the flag).

## Permissions

Enforced server-side by `ShopAuthorization`.

| Who | Products, details, reserve, cancel own | Add product (+ first image) | Edit, change image, list and delete any reservation | Delete product |
| --- | --- | --- | --- | --- |
| Visitor | public catalogue only, read-only; the rest `401` | `401` | `401` | `401` |
| Member | yes | `403` | `403` | `403` |
| Mod | yes | yes | yes | `403` |
| Admin, Owner | yes | yes | yes | yes |

Mod, Admin and Owner also create, edit and delete promotions. Reserving is refused on public or sold-out products, without a size when sizes are ticked, with a size off the list (XS-XXXL), a display name over 200 characters, or a second reservation.

## API (GET `no-store`; writes need `X-CSRF-TOKEN`)

| Endpoint | Does |
| --- | --- |
| `GET /api/shop?fiscalYear=&q=&type=` | `{ fiscalYears, fiscalYear, types, products, sizes, canCreate, canManage }` |
| `GET /api/shop/products/{id}` | details |
| `POST /api/shop/products` / `PUT` / `DELETE /api/shop/products/{id}` | create, edit (Mod+) / delete (Admin, Owner) |
| `POST /api/shop/products/{id}/image` | multipart `image` (WebP, JPEG or PNG, 5 MB max, cropped square) |
| `POST /api/shop/products/{id}/reservations` | `{ hasSizes, size, displayName }`; the reservation |
| `GET /api/shop/products/{id}/reservations` | every reservation of the product (Mod+); username, never the member id |
| `DELETE /api/shop/reservations/{id}` | own, or any for Mod / Admin / Owner |
| `GET /api/shop/promotions` | `{ promotions: [{ id, name, price, normalPrice, inStock, items: [{ productId, name, type, quantity, unitPrice, imageUrl }] }], canManage }` |
| `POST` (201) / `PUT /{id}` / `DELETE /{id}` (204) `/api/shop/promotions` | `{ name, price, items: [{ productId, quantity }] }` (Mod+) |

Tests use the recording storage fake; nothing reaches R2. Product images stay in the public R2 bucket.

## Delete warning

The list carries `reservationCount` for Admin / Owner (one grouped count query) and `null` for everyone else: a number only, never who reserved. "Eliminar produto" adds "Este produto tem N reservas, que também são apagadas." when N > 0. Delete still needs confirmation.

## Promoções (bundle offers)

A grid above the catalogue: a bundle of products, each in a quantity, sold for less than the sum of their prices (badge PROMOÇÃO, normal price struck through, "Poupa X €"). Hidden while there are none and the viewer cannot add one.

- Schema (migration `AddShopPromotions`, additive): `ShopPromotions` (`Name` 100 max, `Price`) and `ShopPromotionItems` (`PromotionId`, `ProductId`, `Quantity`; unique per promotion and product; both FKs cascade, so deleting a product removes it from every bundle and a bundle left empty is not shown).
- The normal price is never stored: it is the sum of current product prices at read time.
- Rules (`ShopPromotionService`): 1 to 10 distinct products, quantity 1 to 99, price above 0 and below the normal price, name required. `inStock` is false when any product has less stock than the bundle needs (shown "Esgotado", still listed).
- Visibility: members see every bundle; visitors only bundles made entirely of public products.
- No reservation on bundles: information only; reservations stay per members-only product.
