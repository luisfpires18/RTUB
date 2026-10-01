using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React Events area's API (React track 011, docs/react-events.md). Thin by design: visibility
/// and every rule live in <see cref="IEventAgendaService"/>, decided from the session. Reads are
/// open to anonymous callers and give them the public agenda only. Every write needs the
/// antiforgery token in the X-CSRF-TOKEN header (GET /api/public/antiforgery-token). Managing events
/// stays on the members' Blazor /member/events.
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

        events.MapGet("/{id:int}/attendance", async (int id, HttpContext http, IEventAgendaService service) =>
            ToResult(await service.GetAttendanceAsync(id, http.User)));

        var writes = events.MapGroup(string.Empty).AddEndpointFilter(RequireAntiforgery);

        writes.MapPut("/{id:int}/attendance", async (int id, EventAttendanceInput input, HttpContext http, IEventAgendaService service) =>
                ToResult(await service.SaveAttendanceAsync(id, input, http.User)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/{id:int}/attendance", async (int id, HttpContext http, IEventAgendaService service) =>
            ToResult(await service.RemoveAttendanceAsync(id, http.User)));

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

    private static IResult ToResult<T>(EventResult<T> result) => result.Status switch
    {
        EventResultStatus.Ok => Results.Ok(result.Value),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Closed => Results.Problem(title: "Esta atuação já não aceita respostas.",
            statusCode: StatusCodes.Status409Conflict),
        _ => NotFound(),
    };
}
