using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The next public events for the React home (React track 010). Read-only and anonymous; the
/// full agenda is still the Blazor /events. Same selection as that page (not yet ended, by date).
/// </summary>
public static class PublicEventEndpoints
{
    public const int UpcomingCount = 3;

    public static void MapPublicEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/events/upcoming", async (HttpContext context, IEventService events) =>
            {
                // An event added or cancelled must show on the next visit.
                context.Response.Headers.CacheControl = "no-cache";
                var upcoming = await events.GetUpcomingEventsAsync(UpcomingCount);
                return Results.Json(upcoming.Select(UpcomingEventDto.From).ToList());
            })
            .AllowAnonymous();
    }
}
