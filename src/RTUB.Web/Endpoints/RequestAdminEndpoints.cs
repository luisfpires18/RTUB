using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using static RTUB.Web.Endpoints.EndpointResults;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /requests ("Gestão de Pedidos", docs/react-requests-questions.md). Thin: every
/// rule lives in <see cref="IRequestAdminService"/>, decided from the session. Signed-in members except Leitões;
/// Admin and Owner answer. Every write needs the antiforgery token in the X-CSRF-TOKEN header. The public
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
}
