using System.Security.Claims;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /gallery timeline (React track 009, docs/react-gallery.md). Read-only and
/// viewer-aware: anonymous callers are allowed and get public items only; the service decides from
/// the session, never from anything the browser sends. Uploads, tags, edits and deletes stay on the
/// members' Blazor /member/gallery.
/// </summary>
public static class GalleryEndpoints
{
    public static void MapGalleryEndpoints(this IEndpointRouteBuilder app)
    {
        var gallery = app.MapGroup("/api/gallery").AllowAnonymous();

        // ?public=true: what a visitor sees, whoever asks (the home preview).
        gallery.MapGet("/", async (int? page, int? pageSize, int? year, string? q, string? person, bool? @public,
                ClaimsPrincipal user, HttpContext context, IGalleryTimelineService timeline) =>
            {
                NoStore(context);
                var query = new GalleryQuery(page ?? 1, pageSize ?? 24, year, q, person, @public == true);
                return Results.Json(await timeline.GetTimelineAsync(user, query));
            });

        gallery.MapGet("/items/{id:int}", async (int id, ClaimsPrincipal user, HttpContext context, IGalleryTimelineService timeline) =>
            {
                NoStore(context);
                // A members-only item is answered exactly like a missing one: a visitor learns nothing.
                return await timeline.GetItemAsync(id, user) is { } item ? Results.Json(item) : Results.NotFound();
            });
    }

    // The answer depends on the session: never cached, not after sign-out, not for another user.
    private static void NoStore(HttpContext context) => context.Response.Headers.CacheControl = "no-store";
}
