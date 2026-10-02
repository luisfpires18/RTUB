using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React /gallery timeline (React track 009, docs/react-gallery.md). Read-only and
/// viewer-aware: anonymous callers are allowed and get public items only; the service decides from
/// the session, never from anything the browser sends. Since 015 it also takes the members' uploads, edits,
/// deletes and person tags (<see cref="IGalleryManagementService"/>); every write needs the antiforgery token in
/// the X-CSRF-TOKEN header.
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

        // Management (React track 015). Answers reuse the Events result mapping.
        var manage = app.MapGroup("/api/gallery").AllowAnonymous().AddEndpointFilter(EventEndpoints.NoStore);
        var writes = manage.MapGroup(string.Empty).AddEndpointFilter(EventEndpoints.RequireAntiforgery);

        manage.MapGet("/people", async (string? q, ClaimsPrincipal user, IGalleryManagementService gallery) =>
            ToResult(await gallery.SearchPeopleAsync(q, user)));

        manage.MapGet("/items/{id:int}/edit", async (int id, ClaimsPrincipal user, IGalleryManagementService gallery) =>
            ToResult(await gallery.GetForEditAsync(id, user)));

        // Multipart: "file", "title", "date" (yyyy-MM-dd), "membersOnly" (true/false) and "personIds" (repeated).
        var maxVideo = app.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<long?>("GalleryMedia:MaxVideoSize") ?? 100 * 1024 * 1024;
        writes.MapPost("/", async (IFormCollection form, ClaimsPrincipal user, IGalleryManagementService gallery) =>
            {
                if (form.Files.GetFile("file") is not { } file)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = new[] { "Escolha uma foto ou um vídeo." } },
                        title: "Há campos por corrigir.");
                }

                await using var content = file.OpenReadStream();
                return ToResult(await gallery.UploadAsync(new GalleryUpload(content, file.FileName, file.ContentType ?? string.Empty, file.Length,
                    form["title"].ToString(), form["date"].ToString(), !string.Equals(form["membersOnly"], "false", StringComparison.OrdinalIgnoreCase),
                    form["personIds"].Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!).ToList()), user),
                    created => Results.Created($"/api/gallery/items/{created.Id}", created));
            })
            .WithMetadata(new RequestSizeLimitAttribute(maxVideo + 64 * 1024));

        writes.MapPut("/items/{id:int}", async (int id, GalleryEditInput input, ClaimsPrincipal user, IGalleryManagementService gallery) =>
                ToResult(await gallery.UpdateAsync(id, input, user)))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));

        writes.MapDelete("/items/{id:int}", async (int id, ClaimsPrincipal user, IGalleryManagementService gallery) =>
            ToResult(await gallery.DeleteAsync(id, user), _ => Results.NoContent()));
    }

    private static IResult ToResult<T>(EventResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        EventResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        EventResultStatus.Invalid => Results.ValidationProblem(
            (result.Errors ?? new Dictionary<string, string[]>()).ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir."),
        EventResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        EventResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.", statusCode: StatusCodes.Status403Forbidden),
        _ => Results.Problem(title: "Media não encontrada.", statusCode: StatusCodes.Status404NotFound),
    };

    // The answer depends on the session: never cached, not after sign-out, not for another user.
    private static void NoStore(HttpContext context) => context.Response.Headers.CacheControl = "no-store";
}
