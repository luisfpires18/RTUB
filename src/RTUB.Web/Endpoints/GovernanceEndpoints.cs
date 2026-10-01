using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// Public Órgãos Sociais for the React /roles (React track 008, docs/react-portal-pilot.md).
/// Anonymous and read-only; <see cref="IGovernanceService"/> decides the mandate and maps only
/// public fields. Managing fiscal years and positions stays on the members' Blazor /member/roles.
/// </summary>
public static class GovernanceEndpoints
{
    public static void MapGovernanceEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/governance", async (string? fiscalYear, HttpContext context, IGovernanceService governance) =>
            {
                // Changes when a Mod/Admin assigns a position; never serve a stale mandate.
                context.Response.Headers.CacheControl = "no-cache";
                return Results.Json(await governance.GetPublicGovernanceAsync(fiscalYear));
            })
            .AllowAnonymous();
    }
}
