using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /requests ("Gestão de Pedidos", task 031, docs/react-requests-questions.md; was the Blazor page). Thin: every
/// rule lives in <see cref="IRequestAdminService"/>, decided from the session. Signed-in members except Leitões, as
/// before; Admin and Owner answer. Every write needs the antiforgery token in the X-CSRF-TOKEN header. The public
/// form keeps its own POST /api/public/requests.
/// </summary>
public static class RequestAdminEndpoints
{
    public static void MapRequestAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var requests = app.MapGroup("/api/requests").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = requests.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // ?fiscalYear= (absent: the current year; empty: every year)&q=&status= (pending | confirmed | rejected)
        requests.MapGet("/", async (HttpContext http, IRequestAdminService service) =>
        {
            var query = http.Request.Query;
            return ToResult(await service.GetAsync(query.ContainsKey("fiscalYear") ? query["fiscalYear"].ToString() : null,
                query["q"].ToString(), query["status"].ToString(), http.User));
        });

        writes.MapPost("/{id:int}/approve", async (int id, HttpContext http, IRequestAdminService service) =>
            ToResult(await service.ApproveAsync(id, http.User)));

        writes.MapPost("/{id:int}/reject", async (int id, HttpContext http, IRequestAdminService service) =>
            ToResult(await service.RejectAsync(id, http.User)));

        writes.MapDelete("/{id:int}", async (int id, HttpContext http, IRequestAdminService service) =>
            ToResult(await service.DeleteAsync(id, http.User), _ => Results.NoContent()));
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
