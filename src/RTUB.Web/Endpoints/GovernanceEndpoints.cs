using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// Órgãos Sociais for the React /roles. Public and read-only (React track 008): <see cref="IGovernanceService"/>
/// decides the mandate and maps only public fields. Members' side (React track 016, was the Blazor /member/roles,
/// docs/react-governance.md): the RGI, fiscal years and position assignments, every rule in
/// <see cref="IGovernanceManagementService"/>, decided from the session. Every write needs the antiforgery token in the
/// X-CSRF-TOKEN header.
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

        var governance = app.MapGroup("/api/governance").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = governance.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // Member, Mod, Admin, Owner.
        governance.MapGet("/rgi", async (HttpContext http, IGovernanceManagementService manage) =>
            ToResult(await manage.GetRgiAsync(http.User)));

        // Mod, Admin, Owner from here on.
        governance.MapGet("/manage", async (string? fiscalYear, HttpContext http, IGovernanceManagementService manage) =>
            ToResult(await manage.GetAsync(fiscalYear, http.User)));

        governance.MapGet("/members", async (string? q, HttpContext http, IGovernanceManagementService manage) =>
            ToResult(await manage.SearchMembersAsync(q, http.User)));

        writes.MapPost("/years", async (GovernanceFiscalYearInput input, HttpContext http, IGovernanceManagementService manage) =>
                ToResult(await manage.CreateFiscalYearAsync(input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        writes.MapPost("/assignments", async (GovernanceAssignmentInput input, HttpContext http, IGovernanceManagementService manage) =>
                ToResult(await manage.AssignAsync(input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        writes.MapDelete("/assignments/{id:int}", async (int id, HttpContext http, IGovernanceManagementService manage) =>
            ToResult(await manage.RemoveAsync(id, http.User)));
    }

    private static IResult ToResult<T>(EventResult<T> result) => result.Status switch
    {
        EventResultStatus.Ok => Results.Ok(result.Value),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.", statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: "Cargo não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
