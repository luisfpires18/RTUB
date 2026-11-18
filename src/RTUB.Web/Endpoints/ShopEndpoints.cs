using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using static RTUB.Web.Endpoints.EndpointResults;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /shop ("Loja RTUB", docs/react-shop.md). Thin: every rule lives in
/// <see cref="IProductShopService"/>, decided from the session. The catalogue is public and returns a public-only DTO
/// to visitors; reservation and management reads and every write remain service-authorized. Every write needs the
/// antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class ShopEndpoints
{
    public static void MapShopEndpoints(this IEndpointRouteBuilder app)
    {
        var shop = app.MapGroup("/api/shop").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = shop.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // ?fiscalYear= (absent: the current year; empty: every year)&q=&type=
        shop.MapGet("/", async (HttpContext http, IProductShopService service) =>
        {
            var query = http.Request.Query;
            var fiscalYear = query.ContainsKey("fiscalYear") ? query["fiscalYear"].ToString() : null;
            return http.User.Identity?.IsAuthenticated == true
                ? ToResult(await service.GetAsync(fiscalYear, query["q"].ToString(), query["type"].ToString(), http.User))
                : ToResult(await service.GetPublicAsync(fiscalYear, query["q"].ToString(), query["type"].ToString()));
        });

        // Bundle offers: public ones for visitors, all for members; Mod, Admin, Owner write.
        shop.MapGet("/promotions", async (HttpContext http, IShopPromotionService promotions) => Results.Ok(await promotions.GetAsync(http.User)));

        writes.MapPost("/promotions", async (ShopPromotionInput input, HttpContext http, IShopPromotionService promotions) =>
                ToResult(await promotions.CreateAsync(input, http.User), created => Results.Created($"/api/shop/promotions/{created.Id}", created)))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        writes.MapPut("/promotions/{id:int}", async (int id, ShopPromotionInput input, HttpContext http, IShopPromotionService promotions) =>
                ToResult(await promotions.UpdateAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        writes.MapDelete("/promotions/{id:int}", async (int id, HttpContext http, IShopPromotionService promotions) =>
            ToResult(await promotions.DeleteAsync(id, http.User), _ => Results.NoContent()));

        shop.MapGet("/products/{id:int}", async (int id, HttpContext http, IProductShopService service) =>
            ToResult(await service.GetProductAsync(id, http.User)));

        shop.MapGet("/products/{id:int}/reservations", async (int id, HttpContext http, IProductShopService service) =>
            ToResult(await service.GetReservationsAsync(id, http.User)));

        writes.MapPost("/products", async (ShopProductInput input, HttpContext http, IProductShopService service) =>
                ToResult(await service.CreateProductAsync(input, http.User), created => Results.Created($"/api/shop/products/{created.Id}", created)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapPut("/products/{id:int}", async (int id, ShopProductInput input, HttpContext http, IProductShopService service) =>
                ToResult(await service.UpdateProductAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/products/{id:int}", async (int id, HttpContext http, IProductShopService service) =>
            ToResult(await service.DeleteProductAsync(id, http.User), _ => Results.NoContent()));

        // Multipart "image", already cropped square in the browser.
        writes.MapPost("/products/{id:int}/image", async (int id, IFormCollection form, HttpContext http, IProductShopService service) =>
                form.Files.GetFile("image") is { } file
                    ? ToResult(await service.SetImageAsync(id, new ShopImageUpload(file.OpenReadStream(), file.FileName, file.ContentType ?? string.Empty, file.Length), http.User))
                    : EndpointResults.Invalid("image", "Escolha uma imagem."))
            .WithMetadata(new RequestSizeLimitAttribute(ProductShopService.MaxImageBytes + 16 * 1024));

        writes.MapPost("/products/{id:int}/reservations", async (int id, ShopReservationInput input, HttpContext http, IProductShopService service) =>
                ToResult(await service.ReserveAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        writes.MapDelete("/reservations/{id:int}", async (int id, HttpContext http, IProductShopService service) =>
            ToResult(await service.CancelReservationAsync(id, http.User), _ => Results.NoContent()));
    }
}
