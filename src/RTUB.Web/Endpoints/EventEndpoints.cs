using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React Events area's API (React track 011, docs/react-events.md). Thin by design: visibility
/// and every rule live in <see cref="IEventAgendaService"/>, decided from the session. Reads are
/// open to anonymous callers and give them the public agenda only. Every write needs the
/// antiforgery token in the X-CSRF-TOKEN header (GET /api/public/antiforgery-token). Creating, editing
/// and deleting events is Admin/Owner (011.5); advanced management stays on the members' Blazor /member/events.
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

        events.MapGet("/{id:int}", async (int id, HttpContext http, IEventAgendaService service) =>
            await service.GetEventAsync(id, http.User) is { } detail ? Results.Ok(detail) : NotFound());

        events.MapGet("/{id:int}/edit", async (int id, HttpContext http, IEventAgendaService service) =>
            ToResult(await service.GetEventForEditAsync(id, http.User)));

        events.MapGet("/{id:int}/enrollment", async (int id, HttpContext http, IEventAgendaService service) =>
            ToResult(await service.GetEnrollmentAsync(id, http.User)));

        var writes = events.MapGroup(string.Empty).AddEndpointFilter(RequireAntiforgery);

        writes.MapPost("/", async (EventInput input, HttpContext http, IEventAgendaService service) =>
                ToResult(await service.CreateEventAsync(input, http.User, $"{http.Request.Scheme}://{http.Request.Host}"),
                    created => Results.Created($"/api/events/{created.Id}", created)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapPut("/{id:int}", async (int id, EventInput input, HttpContext http, IEventAgendaService service) =>
                ToResult(await service.UpdateEventAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/{id:int}", async (int id, HttpContext http, IEventAgendaService service) =>
            ToResult(await service.DeleteEventAsync(id, http.User), _ => Results.NoContent()));

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
