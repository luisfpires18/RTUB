using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React Rehearsals API (React track 014, docs/react-rehearsals.md). Thin by design: every rule lives in
/// <see cref="IRehearsalAgendaService"/> (members) and <see cref="IRehearsalAdminService"/> (Admin/Owner),
/// decided from the session. Signed-in members only, as the Blazor page was. Every write needs the antiforgery
/// token in the X-CSRF-TOKEN header (GET /api/public/antiforgery-token). Rehearsals speak of presença
/// (attendance); events keep inscrição (enrollment).
/// </summary>
public static class RehearsalEndpoints
{
    public static void MapRehearsalEndpoints(this IEndpointRouteBuilder app)
    {
        var rehearsals = app.MapGroup("/api/rehearsals").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = rehearsals.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        rehearsals.MapGet("/", async (HttpContext http, IRehearsalAgendaService agenda) =>
            ToResult(await agenda.GetAgendaAsync(http.User)));

        // ?from=&to= as yyyy-MM-dd; the current season by default.
        rehearsals.MapGet("/stats", async (DateOnly? from, DateOnly? to, HttpContext http, IRehearsalAgendaService agenda) =>
            ToResult(await agenda.GetStatsAsync(from, to, http.User)));

        rehearsals.MapGet("/{id:int}", async (int id, HttpContext http, IRehearsalAgendaService agenda) =>
            ToResult(await agenda.GetAsync(id, http.User)));

        // The caller's own presença.
        rehearsals.MapGet("/{id:int}/attendance", async (int id, HttpContext http, IRehearsalAgendaService agenda) =>
            ToResult(await agenda.GetMyAttendanceAsync(id, http.User)));

        writes.MapPut("/{id:int}/attendance", async (int id, RehearsalAttendanceInput input, HttpContext http, IRehearsalAgendaService agenda) =>
                ToResult(await agenda.SaveMyAttendanceAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/{id:int}/attendance", async (int id, HttpContext http, IRehearsalAgendaService agenda) =>
            ToResult(await agenda.RemoveMyAttendanceAsync(id, http.User)));

        // Admin/Owner: the rehearsals themselves.
        writes.MapPost("/", async (RehearsalInput input, HttpContext http, IRehearsalAdminService admin) =>
                ToResult(await admin.CreateAsync(input, http.User), created => Results.Created($"/api/rehearsals/{created.Id}", created)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapPost("/range", async (RehearsalRangeInput input, HttpContext http, IRehearsalAdminService admin) =>
                ToResult(await admin.CreateRangeAsync(input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapPut("/{id:int}", async (int id, RehearsalInput input, HttpContext http, IRehearsalAdminService admin) =>
                ToResult(await admin.UpdateAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/{id:int}", async (int id, HttpContext http, IRehearsalAdminService admin) =>
            ToResult(await admin.DeleteAsync(id, http.User), _ => Results.NoContent()));

        writes.MapPost("/{id:int}/cancel", async (int id, RehearsalCancelInput input, HttpContext http, IRehearsalAdminService admin) =>
                ToResult(await admin.CancelAsync(id, input, http.User), _ => Results.NoContent()))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        writes.MapPost("/{id:int}/reactivate", async (int id, HttpContext http, IRehearsalAdminService admin) =>
            ToResult(await admin.ReactivateAsync(id, http.User), _ => Results.NoContent()));

        rehearsals.MapGet("/{id:int}/notice", async (int id, HttpContext http, IRehearsalAdminService admin) =>
            ToResult(await admin.GetNoticeAudienceAsync(id, http.User)));

        writes.MapPost("/{id:int}/notice", async (int id, RehearsalNoticeInput input, HttpContext http, IRehearsalAdminService admin) =>
                ToResult(await admin.SendNoticeAsync(id, input, http.User, $"{http.Request.Scheme}://{http.Request.Host}")))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        // Admin/Owner: everyone's presenças (a member removes their own here too).
        writes.MapPost("/{id:int}/attendances/{attendanceId:int}/approve", async (int id, int attendanceId, HttpContext http, IRehearsalAdminService admin) =>
            ToResult(await admin.ApproveAsync(id, attendanceId, http.User), _ => Results.NoContent()));

        writes.MapDelete("/{id:int}/attendances/{attendanceId:int}", async (int id, int attendanceId, HttpContext http, IRehearsalAdminService admin) =>
            ToResult(await admin.RemoveAttendanceAsync(id, attendanceId, http.User), _ => Results.NoContent()));

        rehearsals.MapGet("/{id:int}/attendances/members", async (int id, string? q, HttpContext http, IRehearsalAdminService admin) =>
            ToResult(await admin.SearchMembersAsync(id, q, http.User)));

        writes.MapPost("/{id:int}/attendances", async (int id, RehearsalAttendeeInput input, HttpContext http, IRehearsalAdminService admin) =>
                ToResult(await admin.AddAttendeeAsync(id, input, http.User), _ => Results.NoContent()))
            .WithMetadata(new RequestSizeLimitAttribute(1024));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Closed => Results.Problem(title: "Este ensaio já não permite esta ação.",
            statusCode: StatusCodes.Status409Conflict),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.",
            statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: "Ensaio não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
