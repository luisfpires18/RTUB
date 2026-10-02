using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React Events area's API (React track 011, docs/react-events.md). Thin by design: visibility
/// and every rule live in <see cref="IEventAgendaService"/>, decided from the session. Reads are
/// open to anonymous callers and give them the public agenda only. Every write needs the
/// antiforgery token in the X-CSRF-TOKEN header (GET /api/public/antiforgery-token). Creating, editing
/// and deleting events is Admin/Owner (011.5), as are the image, cancel / reactivate and notices (012A,
/// <see cref="IEventAdminService"/>), prizes (012B), videos (012C), repertoire (012D) and other members' answers (012E). Members' statistics: GET /stats (012F).
/// </summary>
public static class EventEndpoints
{
    public const int UpcomingCount = 3;

    public static void MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        // The home preview (React track 010): the next three, visitors' fields only, whoever asks.
        app.MapGet("/api/public/events/upcoming", async (HttpContext context, IEventAgendaService events) =>
            {
                // An event added or cancelled must show on the next visit.
                context.Response.Headers.CacheControl = "no-cache";
                return Results.Json(await events.GetUpcomingPreviewAsync(UpcomingCount));
            })
            .AllowAnonymous();

        var events = app.MapGroup("/api/events").AllowAnonymous().AddEndpointFilter(NoStore);

        events.MapGet("/", async (HttpContext http, IEventAgendaService service) =>
            Results.Ok(await service.GetAgendaAsync(http.User)));

        // Members' enrollment statistics (012F; was the /member/events modal). ?from=&to= as yyyy-MM-dd.
        events.MapGet("/stats", async (DateOnly? from, DateOnly? to, HttpContext http, IEventAgendaService service) =>
            ToResult(await service.GetStatsAsync(from, to, http.User)));

        events.MapGet("/{id:int}", async (int id, HttpContext http, IEventAgendaService service) =>
            await service.GetEventAsync(id, http.User) is { } detail ? Results.Ok(detail) : NotFound());

        events.MapGet("/{id:int}/edit", async (int id, HttpContext http, IEventAgendaService service) =>
            ToResult(await service.GetEventForEditAsync(id, http.User)));

        events.MapGet("/{id:int}/enrollment", async (int id, HttpContext http, IEventAgendaService service) =>
            ToResult(await service.GetEnrollmentAsync(id, http.User)));

        var writes = events.MapGroup(string.Empty).AddEndpointFilter(RequireAntiforgery);

        writes.MapPost("/", async (EventInput input, HttpContext http, IEventAgendaService service) =>
                ToResult(await service.CreateEventAsync(input, http.User, BaseUrl(http)),
                    created => Results.Created($"/api/events/{created.Id}", created)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapPut("/{id:int}", async (int id, EventInput input, HttpContext http, IEventAgendaService service) =>
                ToResult(await service.UpdateEventAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/{id:int}", async (int id, HttpContext http, IEventAgendaService service) =>
            ToResult(await service.DeleteEventAsync(id, http.User), _ => Results.NoContent()));

        // Admin/Owner (012A). The image is multipart ("image", already cropped in the browser).
        writes.MapPost("/{id:int}/image", async (int id, IFormCollection form, HttpContext http, IEventAdminService admin) =>
                form.Files.GetFile("image") is { } file
                    ? ToResult(await admin.SetImageAsync(id, new EventImageUpload(file.OpenReadStream(), file.FileName, file.ContentType ?? string.Empty, file.Length), http.User),
                        _ => Results.NoContent())
                    : Results.ValidationProblem(new Dictionary<string, string[]> { ["image"] = new[] { "Escolha uma imagem." } }, title: "Há campos por corrigir."))
            .WithMetadata(new RequestSizeLimitAttribute(EventAdminService.MaxImageBytes + 16 * 1024));

        writes.MapDelete("/{id:int}/image", async (int id, HttpContext http, IEventAdminService admin) =>
            ToResult(await admin.RemoveImageAsync(id, http.User), _ => Results.NoContent()));

        writes.MapPost("/{id:int}/cancel", async (int id, EventCancelInput input, HttpContext http, IEventAdminService admin) =>
                ToResult(await admin.CancelAsync(id, input, http.User, BaseUrl(http))))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapPost("/{id:int}/reactivate", async (int id, HttpContext http, IEventAdminService admin) =>
            ToResult(await admin.ReactivateAsync(id, http.User), _ => Results.NoContent()));

        events.MapGet("/{id:int}/notices", async (int id, HttpContext http, IEventAdminService admin) =>
            ToResult(await admin.GetNoticeAudienceAsync(id, http.User)));

        writes.MapPost("/{id:int}/notices", async (int id, EventNoticeInput input, HttpContext http, IEventAdminService admin) =>
                ToResult(await admin.SendNoticeAsync(id, input, http.User, BaseUrl(http))))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        // Prizes (Admin/Owner, 012B). Visitors already read prize names in every event summary.
        events.MapGet("/{id:int}/prizes", async (int id, HttpContext http, IEventAdminService admin) =>
            ToResult(await admin.GetPrizesAsync(id, http.User)));

        writes.MapPost("/{id:int}/prizes", async (int id, EventPrizeInput input, HttpContext http, IEventAdminService admin) =>
                ToResult(await admin.AddPrizeAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        writes.MapPut("/{id:int}/prizes/{prizeId:int}", async (int id, int prizeId, EventPrizeInput input, HttpContext http, IEventAdminService admin) =>
                ToResult(await admin.UpdatePrizeAsync(id, prizeId, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        writes.MapDelete("/{id:int}/prizes/{prizeId:int}", async (int id, int prizeId, HttpContext http, IEventAdminService admin) =>
            ToResult(await admin.DeletePrizeAsync(id, prizeId, http.User)));

        // Videos (Admin/Owner, 012C). Everyone already reads an event's videos in GET /api/events/{id}.
        events.MapGet("/{id:int}/videos", async (int id, HttpContext http, IEventAdminService admin) =>
            ToResult(await admin.GetVideosAsync(id, http.User)));

        // Multipart: the file ("file", ≤100 MB) and its title ("title").
        writes.MapPost("/{id:int}/videos", async (int id, IFormCollection form, HttpContext http, IEventAdminService admin) =>
            {
                if (form.Files.GetFile("file") is not { } file)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = new[] { "Escolha um ficheiro de vídeo." } },
                        title: "Há campos por corrigir.");
                }

                await using var content = file.OpenReadStream();
                return ToResult(await admin.AddVideoAsync(id,
                    new EventVideoUpload(content, file.FileName, file.ContentType ?? string.Empty, file.Length, form["title"].ToString()), http.User));
            })
            .WithMetadata(new RequestSizeLimitAttribute(EventAdminService.MaxVideoBytes + 16 * 1024));

        writes.MapPut("/{id:int}/videos/{videoId:int}", async (int id, int videoId, EventVideoTitleInput input, HttpContext http, IEventAdminService admin) =>
                ToResult(await admin.RenameVideoAsync(id, videoId, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        writes.MapPost("/{id:int}/videos/reorder", async (int id, EventVideoOrderInput input, HttpContext http, IEventAdminService admin) =>
                ToResult(await admin.ReorderVideosAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/{id:int}/videos/{videoId:int}", async (int id, int videoId, HttpContext http, IEventAdminService admin) =>
            ToResult(await admin.DeleteVideoAsync(id, videoId, http.User)));

        // Repertoire (Admin/Owner, 012D). Members already read it in GET /api/events/{id}.
        events.MapGet("/{id:int}/repertoire", async (int id, HttpContext http, IEventRepertoireAdminService repertoire) =>
            ToResult(await repertoire.GetAsync(id, http.User)));

        events.MapGet("/{id:int}/repertoire/songs", async (int id, string? q, HttpContext http, IEventRepertoireAdminService repertoire) =>
            ToResult(await repertoire.SearchSongsAsync(id, q, http.User)));

        writes.MapPost("/{id:int}/repertoire", async (int id, EventRepertoireAddInput input, HttpContext http, IEventRepertoireAdminService repertoire) =>
                ToResult(await repertoire.AddAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        writes.MapPost("/{id:int}/repertoire/reorder", async (int id, EventRepertoireOrderInput input, HttpContext http, IEventRepertoireAdminService repertoire) =>
                ToResult(await repertoire.ReorderAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/{id:int}/repertoire/{itemId:int}", async (int id, int itemId, HttpContext http, IEventRepertoireAdminService repertoire) =>
            ToResult(await repertoire.RemoveAsync(id, itemId, http.User)));

        writes.MapDelete("/{id:int}/repertoire/days/{date}", async (int id, string date, HttpContext http, IEventRepertoireAdminService repertoire) =>
            ToResult(await repertoire.RemoveDayAsync(id, date, http.User)));

        // Other members' answers (Admin/Owner, 012E): the old /events/{id}/enrollments add / remove.
        // A member's own answer stays PUT/DELETE /{id}/enrollment below.
        events.MapGet("/{id:int}/enrollments", async (int id, HttpContext http, IEventParticipantsAdminService participants) =>
            ToResult(await participants.GetAsync(id, http.User)));

        events.MapGet("/{id:int}/enrollments/members", async (int id, string? q, HttpContext http, IEventParticipantsAdminService participants) =>
            ToResult(await participants.SearchMembersAsync(id, q, http.User)));

        writes.MapPost("/{id:int}/enrollments", async (int id, EventEnrollmentAddInput input, HttpContext http, IEventParticipantsAdminService participants) =>
                ToResult(await participants.AddAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(4 * 1024));

        writes.MapDelete("/{id:int}/enrollments/{enrollmentId:int}", async (int id, int enrollmentId, HttpContext http, IEventParticipantsAdminService participants) =>
            ToResult(await participants.RemoveAsync(id, enrollmentId, http.User)));

        writes.MapPut("/{id:int}/enrollment", async (int id, EventEnrollmentInput input, HttpContext http, IEventAgendaService service) =>
                ToResult(await service.SaveEnrollmentAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/{id:int}/enrollment", async (int id, HttpContext http, IEventAgendaService service) =>
            ToResult(await service.RemoveEnrollmentAsync(id, http.User)));

        writes.MapPost("/videos/{id:int}/plays", async (int id, HttpContext http, IEventAgendaService service) =>
                await service.RecordVideoPlayAsync(id, http.User) ? Results.NoContent() : NotFound())
            .WithMetadata(new RequestSizeLimitAttribute(1024));
    }

    /// <summary>
    /// The answers depend on the session: never cached, not after sign-out, not for another user.
    /// Failures answer as a JSON problem, never a page or a stack trace.
    /// </summary>
    private static async ValueTask<object?> NoStore(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try
        {
            return await next(context);
        }
        catch (Exception ex) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(EventEndpoints))
                .LogError(ex, "Events API {Method} {Path} failed", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            return Results.Problem(title: "Não foi possível concluir. Tente novamente daqui a pouco.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>Explicit CSRF check for every Events write: the X-CSRF-TOKEN header, bound to the antiforgery cookie.</summary>
    private static async ValueTask<object?> RequireAntiforgery(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var antiforgery = context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(title: "A sessão desta página expirou. Recarregue a página e tente de novo.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }

    /// <summary>The absolute site address the push and email links are built from.</summary>
    private static string BaseUrl(HttpContext http) => $"{http.Request.Scheme}://{http.Request.Host}";

    private static IResult NotFound() => Results.Problem(title: "Atuação não encontrada.", statusCode: StatusCodes.Status404NotFound);

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Closed => Results.Problem(title: "Esta atuação já não aceita respostas.",
            statusCode: StatusCodes.Status409Conflict),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.",
            statusCode: StatusCodes.Status403Forbidden),
        EventResultStatus.InUse => Results.Problem(title: "Esta atuação tem encomendas NERBA associadas e não pode ser apagada.",
            statusCode: StatusCodes.Status409Conflict, type: "events:in-use"),
        _ => NotFound(),
    };
}
