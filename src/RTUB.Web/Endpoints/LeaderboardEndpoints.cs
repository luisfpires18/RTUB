using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /leaderboard ("Tabela de Classificação", React track 019, docs/react-leaderboard.md; was the Blazor page).
/// Thin: every rule lives in <see cref="ILeaderboardService"/>, decided from the session. Signed-in members only, as
/// before. Every write needs the antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        var leaderboard = app.MapGroup("/api/leaderboard").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = leaderboard.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // ?fiscalYear=2025-2026&q=
        leaderboard.MapGet("/", async (string? fiscalYear, string? q, HttpContext http, ILeaderboardService service) =>
            ToResult(await service.GetAsync(fiscalYear, q, http.User)));

        leaderboard.MapGet("/members/{id}", async (string id, string? fiscalYear, HttpContext http, ILeaderboardService service) =>
            ToResult(await service.GetMemberAsync(id, fiscalYear, http.User)));

        leaderboard.MapGet("/members/{id}/comments", async (string id, HttpContext http, ILeaderboardService service) =>
            ToResult(await service.GetCommentsAsync(id, http.User)));

        writes.MapPost("/members/{id}/comments", async (string id, LeaderboardCommentInput input, HttpContext http, ILeaderboardService service) =>
                ToResult(await service.AddCommentAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapPost("/comments/{commentId:int}/like", async (int commentId, HttpContext http, ILeaderboardService service) =>
            ToResult(await service.ToggleLikeAsync(commentId, http.User), liked => Results.Ok(new { liked })));

        writes.MapDelete("/comments/{commentId:int}", async (int commentId, HttpContext http, ILeaderboardService service) =>
            ToResult(await service.DeleteCommentAsync(commentId, http.User), _ => Results.NoContent()));

        writes.MapPut("/story", async (LeaderboardStoryInput input, HttpContext http, ILeaderboardService service) =>
                ToResult(await service.UpdateStoryAsync(input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(32 * 1024));
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
