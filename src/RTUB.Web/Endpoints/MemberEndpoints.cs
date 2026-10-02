using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React members area (React track 017, docs/react-members.md; was the Blazor /members and /hierarchy). Read-only
/// and thin: every rule lives in <see cref="IMemberDirectoryService"/>, decided from the session. Signed-in members
/// only, as the Blazor pages were. The admin tools stay on the Blazor /members/manage for now.
/// </summary>
public static class MemberEndpoints
{
    public static void MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var members = app.MapGroup("/api/members").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);

        // ?q=&category=&subCategory=&instrument=&activeOnly=true
        members.MapGet("/", async (string? q, string? category, string? subCategory, string? instrument, bool? activeOnly,
                HttpContext http, IMemberDirectoryService directory) =>
            ToResult(await directory.GetDirectoryAsync(new MemberDirectoryQuery(q, category, subCategory, instrument, activeOnly == true), http.User)));

        members.MapGet("/active", async (string? status, string? q, HttpContext http, IMemberDirectoryService directory) =>
            ToResult(await directory.GetActiveMembersAsync(status, q, http.User)));

        members.MapGet("/birthdays", async (string? q, HttpContext http, IMemberDirectoryService directory) =>
            ToResult(await directory.GetBirthdaysAsync(q, http.User)));

        members.MapGet("/hierarchy", async (HttpContext http, IMemberDirectoryService directory) =>
            ToResult(await directory.GetHierarchyAsync(http.User)));

        members.MapGet("/{id}", async (string id, HttpContext http, IMemberDirectoryService directory) =>
            ToResult(await directory.GetMemberAsync(id, http.User)));
    }

    private static IResult ToResult<T>(EventResult<T> result) => result.Status switch
    {
        EventResultStatus.Ok => Results.Ok(result.Value),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.", statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: "Membro não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
