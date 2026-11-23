using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using static RTUB.Web.Endpoints.EndpointResults;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /trintuna (docs/react-trintuna.md). Thin: every rule lives in <see cref="ITrintunaService"/>, decided from
/// the session. Anonymous 401; a member not on the access list 404; a listed member on an Owner-only call 403; a
/// settled or past gathering 409. Every write needs the antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class TrintunaEndpoints
{
    public static void MapTrintunaEndpoints(this IEndpointRouteBuilder app)
    {
        var trintuna = app.MapGroup("/api/trintuna").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = trintuna.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        trintuna.MapGet("/", async (HttpContext http, ITrintunaService service) =>
            Answer(await service.GetAsync(http.User)));

        writes.MapPost("/gatherings", async (GatheringInput input, HttpContext http, ITrintunaService service) =>
            Answer(await service.CreateAsync(input, http.User)));

        writes.MapPut("/gatherings/{id:int}", async (int id, GatheringInput input, HttpContext http, ITrintunaService service) =>
            Answer(await service.UpdateAsync(id, input, http.User)));

        writes.MapDelete("/gatherings/{id:int}", async (int id, HttpContext http, ITrintunaService service) =>
            Answer(await service.DeleteAsync(id, http.User), _ => Results.NoContent()));

        writes.MapPost("/gatherings/{id:int}/settle", async (int id, GatheringOptionInput input, HttpContext http, ITrintunaService service) =>
            Answer(await service.SettleAsync(id, input, http.User)));

        writes.MapPut("/gatherings/{id:int}/vote", async (int id, GatheringOptionInput input, HttpContext http, ITrintunaService service) =>
            Answer(await service.VoteAsync(id, input, http.User)));

        writes.MapPut("/gatherings/{id:int}/presence", async (int id, GatheringPresenceInput input, HttpContext http, ITrintunaService service) =>
            Answer(await service.SetPresenceAsync(id, input, http.User)));

        writes.MapDelete("/gatherings/{id:int}/presence", async (int id, HttpContext http, ITrintunaService service) =>
            Answer(await service.ClearPresenceAsync(id, http.User), _ => Results.NoContent()));

        trintuna.MapGet("/access", async (HttpContext http, ITrintunaService service) =>
            Answer(await service.GetAccessAsync(http.User)));

        writes.MapPut("/access", async (TrintunaAccessInput input, HttpContext http, ITrintunaService service) =>
            Answer(await service.UpdateAccessAsync(input, http.User)));
    }

    private static IResult Answer<T>(EventResult<T> result, Func<T, IResult>? ok = null) =>
        ToResult(result, ok, special: status => status == EventResultStatus.Closed
            ? Results.Problem(title: "Este almoço já não aceita esta resposta.", statusCode: StatusCodes.Status409Conflict, type: "trintuna:closed")
            : null);
}
