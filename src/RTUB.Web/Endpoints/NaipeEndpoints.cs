using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /naipes and /naipes/config (task 033, docs/react-naipes.md; were the Blazor pages). Thin: every rule lives
/// in <see cref="INaipeBoardService"/>, decided from the session. Signed-in members only, as the Blazor pages were;
/// the settings are Admin / Owner. Every write needs the antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class NaipeEndpoints
{
    public static void MapNaipeEndpoints(this IEndpointRouteBuilder app)
    {
        var naipes = app.MapGroup("/api/naipes").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = naipes.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // ?instrument=<InstrumentType>&q=
        naipes.MapGet("/", async (string? instrument, string? q, HttpContext http, INaipeBoardService service) =>
            ToResult(await service.GetAsync(instrument, q, http.User)));

        naipes.MapGet("/{id:int}/comments", async (int id, HttpContext http, INaipeBoardService service) =>
            ToResult(await service.GetCommentsAsync(id, http.User)));

        naipes.MapGet("/config", async (HttpContext http, INaipeBoardService service) =>
            ToResult(await service.GetSettingsAsync(http.User)));

        // Multipart: "file" plus instrument, kind (video | image), title, description, sortOrder.
        writes.MapPost("/", async (IFormCollection form, HttpContext http, INaipeBoardService service) =>
                {
                    var file = form.Files.GetFile("file");
                    await using var content = file?.OpenReadStream() ?? Stream.Null;
                    var upload = new NaipeUpload(content, file?.FileName, file?.ContentType ?? string.Empty, file?.Length ?? 0,
                        form["instrument"].ToString(), form["kind"].ToString() == "video", form["title"].ToString(),
                        form["description"].ToString(), form["sortOrder"].ToString());
                    return ToResult(await service.CreateAsync(upload, http.User), created => Results.Created($"/api/naipes/{created.Id}", created));
                })
            .WithMetadata(new RequestSizeLimitAttribute(NaipeBoardService.MaxVideoBytes + 64 * 1024));

        writes.MapPut("/{id:int}", async (int id, NaipeItemInput input, HttpContext http, INaipeBoardService service) =>
                ToResult(await service.UpdateAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        writes.MapDelete("/{id:int}", async (int id, HttpContext http, INaipeBoardService service) =>
            ToResult(await service.DeleteAsync(id, http.User), _ => Results.NoContent()));

        writes.MapPost("/{id:int}/plays", async (int id, HttpContext http, INaipeBoardService service) =>
            ToResult(await service.PlayedAsync(id, http.User), _ => Results.NoContent()));

        writes.MapPost("/{id:int}/comments", async (int id, NaipeCommentInput input, HttpContext http, INaipeBoardService service) =>
                ToResult(await service.AddCommentAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        writes.MapDelete("/{id:int}/comments/{commentId:int}", async (int id, int commentId, HttpContext http, INaipeBoardService service) =>
            ToResult(await service.DeleteCommentAsync(id, commentId, http.User)));

        writes.MapPut("/config/{id:int}", async (int id, NaipeTypeSettingInput input, HttpContext http, INaipeBoardService service) =>
                ToResult(await service.UpdateSettingAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        // Multipart: "picture".
        writes.MapPost("/config/{id:int}/picture", async (int id, IFormCollection form, HttpContext http, INaipeBoardService service) =>
                form.Files.GetFile("picture") is { } picture
                    ? ToResult(await service.SetPictureAsync(id,
                        new NaipePictureUpload(picture.OpenReadStream(), picture.FileName, picture.ContentType ?? string.Empty, picture.Length), http.User))
                    : Results.ValidationProblem(new Dictionary<string, string[]> { ["picture"] = new[] { "Escolha uma imagem." } },
                        title: "Há campos por corrigir."))
            .WithMetadata(new RequestSizeLimitAttribute(NaipeBoardService.MaxPictureBytes + 64 * 1024));

        writes.MapDelete("/config/{id:int}/picture", async (int id, HttpContext http, INaipeBoardService service) =>
            ToResult(await service.RemovePictureAsync(id, http.User)));
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
