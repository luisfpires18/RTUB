using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /pending-actions ("As minhas ações pendentes", task 071, docs/react-pending-actions.md). Thin: every rule is
/// in <see cref="IPendingActionsService"/>, decided from the database for the signed-in member. Never cached (no-store);
/// the dismissal and the answers need the antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class PendingActionsEndpoints
{
    public static void MapPendingActionsEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/pending-actions").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);

        api.MapGet("/", async (HttpContext http, IPendingActionsService s) => ToResult(await s.GetPageAsync(http.User)));
        api.MapGet("/panels/{panel}", async (string panel, string? filter, int? page, HttpContext http, IPendingActionsService s) =>
            ToResult(await s.GetPanelAsync(http.User, panel, filter, page ?? 1)));

        var writes = api.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);
        writes.MapPost("/dismissals", async (PendingActionDismissInput input, HttpContext http, IPendingActionsService s) =>
                ToResult(await s.DismissAsync(http.User, input), _ => Results.NoContent()))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));
        // Task 072: Vou / Não vou, Fui / Não fui on an activity pending for the caller.
        writes.MapPost("/responses", async (PendingActionResponseInput input, HttpContext http, IPendingActionsService s) =>
                ToResult(await s.RespondAsync(http.User, input), _ => Results.NoContent()))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Esta ação é obrigatória: não pode ser dispensada.",
            statusCode: StatusCodes.Status403Forbidden),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.Conflict => Results.Problem(title: "Já tinhas respondido entretanto.", statusCode: StatusCodes.Status409Conflict,
            type: "pending-actions:answered"),
        _ => Results.Problem(title: "Esta ação já não está pendente.", statusCode: StatusCodes.Status404NotFound),
    };
}
