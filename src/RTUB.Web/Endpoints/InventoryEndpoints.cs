using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /inventory ("Instrumentos", React track 020, docs/react-inventory.md; was the Blazor page). Thin: every rule
/// lives in <see cref="IInstrumentInventoryService"/>, decided from the session. Signed-in members only, as before. Every
/// write needs the antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var inventory = app.MapGroup("/api/inventory").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = inventory.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // ?q=&category=&condition=
        inventory.MapGet("/", async (string? q, string? category, string? condition, HttpContext http, IInstrumentInventoryService service) =>
            ToResult(await service.GetAsync(new InstrumentQuery(q, category, condition), http.User)));

        inventory.MapGet("/{id:int}", async (int id, HttpContext http, IInstrumentInventoryService service) =>
            ToResult(await service.GetByIdAsync(id, http.User)));

        writes.MapPost("/", async (InstrumentInput input, HttpContext http, IInstrumentInventoryService service) =>
                ToResult(await service.CreateAsync(input, http.User), created => Results.Created($"/api/inventory/{created.Id}", created)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapPut("/{id:int}", async (int id, InstrumentInput input, HttpContext http, IInstrumentInventoryService service) =>
                ToResult(await service.UpdateAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/{id:int}", async (int id, HttpContext http, IInstrumentInventoryService service) =>
            ToResult(await service.DeleteAsync(id, http.User), _ => Results.NoContent()));

        // Multipart: "image" (the original) and "thumbnail" (cropped 1:1 in the browser), together.
        writes.MapPost("/{id:int}/image", async (int id, IFormCollection form, HttpContext http, IInstrumentInventoryService service) =>
                form.Files.GetFile("image") is { } image && form.Files.GetFile("thumbnail") is { } thumbnail
                    ? ToResult(await service.SetImageAsync(id, Upload(image), Upload(thumbnail), http.User))
                    : Results.ValidationProblem(new Dictionary<string, string[]> { ["image"] = new[] { "Escolha uma imagem e recorte a miniatura." } },
                        title: "Há campos por corrigir."))
            .WithMetadata(new RequestSizeLimitAttribute(2 * InstrumentInventoryService.MaxImageBytes + 64 * 1024));
    }

    private static InstrumentImageUpload Upload(IFormFile file) =>
        new(file.OpenReadStream(), file.FileName, file.ContentType ?? string.Empty, file.Length);

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.", statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: "Instrumento não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
