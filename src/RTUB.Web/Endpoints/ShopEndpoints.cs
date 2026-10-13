using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /shop ("Loja RTUB", React track 021, docs/react-shop.md; was the Blazor page). Thin: every rule lives in
/// <see cref="IProductShopService"/>, decided from the session. Signed-in members only, as before. Every write needs
/// the antiforgery token in the X-CSRF-TOKEN header.
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
            return ToResult(await service.GetAsync(query.ContainsKey("fiscalYear") ? query["fiscalYear"].ToString() : null,
                query["q"].ToString(), query["type"].ToString(), http.User));
        });

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
                    : Results.ValidationProblem(new Dictionary<string, string[]> { ["image"] = new[] { "Escolha uma imagem." } }, title: "Há campos por corrigir."))
            .WithMetadata(new RequestSizeLimitAttribute(ProductShopService.MaxImageBytes + 16 * 1024));

        writes.MapPost("/products/{id:int}/reservations", async (int id, ShopReservationInput input, HttpContext http, IProductShopService service) =>
                ToResult(await service.ReserveAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        writes.MapDelete("/reservations/{id:int}", async (int id, HttpContext http, IProductShopService service) =>
            ToResult(await service.CancelReservationAsync(id, http.User), _ => Results.NoContent()));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.", statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: "Não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
