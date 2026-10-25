# Permissions: /shop (Loja)

Source: `ShopAuthorization`. Page doc: [react-shop](../react-shop.md).

| Who | What |
| --- | --- |
| Visitor | `401` |
| Any signed-in member | every product; reserve members-only products in stock; cancel their own reservation |
| Mod, Admin, Owner | add products (with the first image) |
| Admin, Owner | edit and delete products, change images, see and delete every reservation |
