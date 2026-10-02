using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React members area (React tracks 017 and 018, docs/react-members.md; was the Blazor /members, /hierarchy and
/// /members/manage). Thin: the read side lives in <see cref="IMemberDirectoryService"/>, the Admin/Owner tools in
/// <see cref="IMemberAdminService"/>, every rule decided from the session. Signed-in members only, as the Blazor pages
/// were. Every write needs the antiforgery token in the X-CSRF-TOKEN header.
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

        // Admin and Owner from here on (018).
        members.MapGet("/mentors", async (string? q, string? exclude, HttpContext http, IMemberAdminService admin) =>
            ToResult(await admin.SearchMentorsAsync(q, exclude, http.User)));

        members.MapGet("/{id}/edit", async (string id, HttpContext http, IMemberAdminService admin) =>
            ToResult(await admin.GetForEditAsync(id, http.User)));

        var writes = members.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        writes.MapPost("/", async (MemberInput input, HttpContext http, IMemberAdminService admin) =>
                ToResult(await admin.CreateAsync(input, http.User), created => Results.Created($"/api/members/{created.Id}", created)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapPut("/{id}", async (string id, MemberInput input, HttpContext http, IMemberAdminService admin) =>
                ToResult(await admin.UpdateAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/{id}", async (string id, HttpContext http, IMemberAdminService admin) =>
            ToResult(await admin.DeleteAsync(id, http.User), _ => Results.NoContent()));

        writes.MapPost("/{id}/instruments", async (string id, MemberInstrumentInput input, HttpContext http, IMemberAdminService admin) =>
                ToResult(await admin.AddInstrumentAsync(id, input.Instrument, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        writes.MapDelete("/{id}/instruments/{instrumentId:int}", async (string id, int instrumentId, HttpContext http, IMemberAdminService admin) =>
            ToResult(await admin.RemoveInstrumentAsync(id, instrumentId, http.User)));

        writes.MapPut("/{id}/instruments/{instrumentId:int}/primary", async (string id, int instrumentId, HttpContext http, IMemberAdminService admin) =>
            ToResult(await admin.SetPrimaryInstrumentAsync(id, instrumentId, http.User)));

        writes.MapPut("/{id}/nickname", async (string id, MemberNicknameInput input, HttpContext http, IMemberAdminService admin) =>
                ToResult(await admin.SetNicknameAsync(id, input, http.User), _ => Results.NoContent()))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        writes.MapPost("/{id}/expel", async (string id, HttpContext http, IMemberAdminService admin) =>
            ToResult(await admin.SetExpelledAsync(id, true, http.User), _ => Results.NoContent()));

        writes.MapPost("/{id}/reactivate", async (string id, HttpContext http, IMemberAdminService admin) =>
            ToResult(await admin.SetExpelledAsync(id, false, http.User), _ => Results.NoContent()));

        writes.MapPost("/{id}/activate", async (string id, HttpContext http, IMemberAdminService admin) =>
            ToResult(await admin.MakeActiveAsync(id, http.User), _ => Results.NoContent()));

        writes.MapPost("/{id}/reminder", async (string id, HttpContext http, IMemberAdminService admin) =>
            ToResult(await admin.SendReminderAsync(id, $"{http.Request.Scheme}://{http.Request.Host}", http.User), _ => Results.NoContent()));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.", statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: "Membro não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
