# Permissions: /shop (Loja)

Source: `ShopAuthorization`. Page doc: [react-shop](../react-shop.md).

| Who | What |
| --- | --- |
| Visitor | the public product catalogue only (read-only); details, reservations and management need sign-in |
| Any signed-in member | every product; reserve members-only products in stock; cancel their own reservation |
| Mod, Admin, Owner | add products (with the first image); create, edit, delete promotions (bundle offers) |
| Admin, Owner | edit and delete products, change images, see and delete every reservation |

Promotions: visitors see only bundles made of public products; members see all.
