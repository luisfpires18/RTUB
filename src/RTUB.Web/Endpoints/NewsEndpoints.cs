using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The public "Novidades" feed on /news (React track 025, docs/react-news.md). Thin: every rule lives in
/// <see cref="INewsService"/>, decided from the session. Reading is open to visitors; every write is Admin/Owner and needs
/// the antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class NewsEndpoints
{
    // A 5000-character text is at most ~20 KB of UTF-8 inside the JSON.
    private const long JsonLimit = 32 * 1024;

    public static void MapNewsEndpoints(this IEndpointRouteBuilder app)
    {
        var news = app.MapGroup("/api/news").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = news.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // ?page=1&pageSize=10 (the home preview asks for 3).
        news.MapGet("/", async (int? page, int? pageSize, HttpContext http, INewsService service) =>
            ToResult(await service.GetFeedAsync(page ?? 1, pageSize ?? NewsService.DefaultPageSize, http.User)));

        writes.MapPost("/", async (NewsPostInput input, HttpContext http, INewsService service) =>
                ToResult(await service.CreateAsync(input, http.User), created => Results.Created($"/api/news/{created.Id}", created)))
            .WithMetadata(new RequestSizeLimitAttribute(JsonLimit));

        writes.MapPut("/{id:int}", async (int id, NewsPostInput input, HttpContext http, INewsService service) =>
                ToResult(await service.UpdateAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(JsonLimit));

        writes.MapPost("/{id:int}/publish", async (int id, HttpContext http, INewsService service) =>
            ToResult(await service.PublishAsync(id, http.User)));

        writes.MapPost("/{id:int}/unpublish", async (int id, HttpContext http, INewsService service) =>
            ToResult(await service.UnpublishAsync(id, http.User)));

        writes.MapDelete("/{id:int}", async (int id, HttpContext http, INewsService service) =>
            ToResult(await service.DeleteAsync(id, http.User), _ => Results.NoContent()));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Entre para continuar.", statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Só Admin ou Owner gerem as Novidades.", statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: "Publicação não encontrada.", statusCode: StatusCodes.Status404NotFound),
    };
}
