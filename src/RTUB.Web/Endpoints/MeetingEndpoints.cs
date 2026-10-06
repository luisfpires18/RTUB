using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /meetings (task 034, docs/react-meetings.md; was the Blazor page). Thin: every rule lives in
/// <see cref="IMeetingBoardService"/>, decided from the session. Signed-in members only, as the Blazor page was
/// (<c>[Authorize]</c>); a Leitão is refused everything (403) and a meeting the member does not see is a 404. Nothing is
/// cached (no-store) and every write needs the antiforgery token in the X-CSRF-TOKEN header.
/// </summary>
public static class MeetingEndpoints
{
    public static void MapMeetingEndpoints(this IEndpointRouteBuilder app)
    {
        var meetings = app.MapGroup("/api/meetings").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = meetings.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        // ---------- meetings ----------

        // ?fy=2025-2026 | all (missing = the current fiscal year) &q=
        meetings.MapGet("/", async (string? fy, string? q, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetBoardAsync(fy, q, http.User)));

        meetings.MapGet("/form", async (HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetFormAsync(http.User)));

        meetings.MapGet("/{id:int}", async (int id, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetMeetingAsync(id, http.User)));

        writes.MapPost("/", async (MeetingInput input, HttpContext http, IMeetingBoardService service) =>
                ToResult(await service.CreateAsync(input, http.User), saved => Results.Created($"/api/meetings/{saved.Id}", saved)))
            .WithMetadata(new RequestSizeLimitAttribute(32 * 1024));

        writes.MapPut("/{id:int}", async (int id, MeetingInput input, HttpContext http, IMeetingBoardService service) =>
                ToResult(await service.UpdateAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(32 * 1024));

        writes.MapDelete("/{id:int}", async (int id, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.DeleteAsync(id, http.User), _ => Results.NoContent()));

        meetings.MapGet("/{id:int}/cancel", async (int id, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetCancelDraftAsync(id, http.User)));

        writes.MapPost("/{id:int}/cancel", async (int id, MeetingCancelInput input, HttpContext http, IMeetingBoardService service) =>
                ToResult(await service.CancelAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        writes.MapPost("/{id:int}/uncancel", async (int id, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.UncancelAsync(id, http.User)));

        meetings.MapGet("/{id:int}/email", async (int id, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetEmailDraftAsync(id, http.User)));

        writes.MapPost("/{id:int}/email/preview", async (int id, MeetingEmailPreviewInput input, HttpContext http, IMeetingBoardService service) =>
                ToResult(await service.PreviewEmailAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024));

        writes.MapPost("/{id:int}/email", async (int id, MeetingEmailInput input, HttpContext http, IMeetingBoardService service) =>
                ToResult(await service.SendEmailAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(64 * 1024));

        meetings.MapGet("/{id:int}/push", async (int id, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetPushDraftAsync(id, http.User)));

        writes.MapPost("/{id:int}/push", async (int id, MeetingPushInput input, HttpContext http, IMeetingBoardService service) =>
                ToResult(await service.SendPushAsync(id, input, http.User, BaseUrl(http))))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        // ---------- participations ----------

        writes.MapPut("/{id:int}/participation", async (int id, MeetingParticipationInput input, HttpContext http, IMeetingBoardService service) =>
                ToResult(await service.RespondAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(8 * 1024));

        meetings.MapGet("/{id:int}/participants", async (int id, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetParticipantsAsync(id, http.User)));

        meetings.MapGet("/{id:int}/participants/candidates", async (int id, string? q, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetCandidatesAsync(id, q, http.User)));

        writes.MapPost("/{id:int}/participants", async (int id, MeetingAddParticipantInput input, HttpContext http, IMeetingBoardService service) =>
                ToResult(await service.AddParticipantAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        writes.MapDelete("/{id:int}/participants/{participationId:int}", async (int id, int participationId, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.RemoveParticipationAsync(id, participationId, http.User)));

        // ---------- atas ----------

        meetings.MapGet("/{id:int}/ata", async (int id, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetAtaAsync(id, http.User)));

        meetings.MapGet("/{id:int}/ata/edit", async (int id, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetAtaEditorAsync(id, http.User)));

        writes.MapPut("/{id:int}/ata", async (int id, MeetingAtaInput input, HttpContext http, IMeetingBoardService service) =>
                ToResult(await service.SaveAtaAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(2 * 1024 * 1024));

        writes.MapPost("/{id:int}/ata/publish", async (int id, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.PublishAtaAsync(id, http.User)));

        meetings.MapGet("/{id:int}/ata/pdf", async (int id, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetAtaPdfAsync(id, http.User), pdf => Results.File(pdf.Content, "application/pdf", pdf.FileName)));

        writes.MapPost("/{id:int}/ata/confirmation", async (int id, MeetingAtaConfirmationInput input, HttpContext http, IMeetingBoardService service) =>
                ToResult(await service.ConfirmAtaAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        // ---------- meeting requests ("Pedidos de Reuniões") ----------

        // ?status=Pending|Confirmed|Rejected &fy=2025-2026|all &page= &pageSize=4|8|12|16|20
        meetings.MapGet("/requests", async (string? status, string? fy, int? page, int? pageSize, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.GetRequestsAsync(status, fy, page, pageSize, http.User)));

        writes.MapPost("/requests", async (MeetingRequestInput input, HttpContext http, IMeetingBoardService service) =>
                ToResult(await service.ProposeAsync(input, http.User), id => Results.Created($"/api/meetings/requests/{id}", new { id })))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapPost("/requests/{requestId:int}/accept", async (int requestId, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.AcceptRequestAsync(requestId, http.User)));

        writes.MapPost("/requests/{requestId:int}/reject", async (int requestId, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.RejectRequestAsync(requestId, http.User), _ => Results.NoContent()));

        writes.MapDelete("/requests/{requestId:int}", async (int requestId, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.DeleteRequestAsync(requestId, http.User), _ => Results.NoContent()));

        writes.MapPost("/requests/{requestId:int}/reminder", async (int requestId, HttpContext http, IMeetingBoardService service) =>
            ToResult(await service.RemindRequestAsync(requestId, http.User, BaseUrl(http)), _ => Results.NoContent()));
    }

    private static string BaseUrl(HttpContext http) => $"{http.Request.Scheme}://{http.Request.Host}";

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.", statusCode: StatusCodes.Status403Forbidden),
        EventResultStatus.Closed => Results.Problem(title: "Esta reunião já não permite esta ação.", statusCode: StatusCodes.Status409Conflict),
        _ => Results.Problem(title: "Não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
