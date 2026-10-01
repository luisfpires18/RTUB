using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Web.Endpoints;

/// <summary>
/// The React Music area's API (React track 006, docs/react-music.md). Thin by design: every rule -
/// visibility, roles, the play cooldown, validation - lives in <see cref="IMusicService"/>.
/// Reads are open to anonymous callers and show them public albums only. Every write, plays
/// included, needs the antiforgery token in the X-CSRF-TOKEN header (from
/// GET /api/public/antiforgery-token). Nothing here is cached: answers depend on the session and
/// carry short-lived storage links.
/// </summary>
public static class MusicEndpoints
{
    private const long JsonLimit = 64 * 1024;

    public static void MapMusicEndpoints(this IEndpointRouteBuilder app)
    {
        var music = app.MapGroup("/api/music").AddEndpointFilter(NoStore);

        music.MapGet("/albums", async (HttpContext http, IMusicService service) =>
            Results.Ok(await service.GetAlbumsAsync(http.User)));

        music.MapGet("/albums/{id:int}", async (int id, HttpContext http, IMusicService service) =>
            ToResult(await service.GetAlbumAsync(id, http.User)));

        music.MapGet("/songs/{id:int}/lyrics", async (int id, HttpContext http, IMusicService service) =>
            ToResult(await service.GetLyricsAsync(id, http.User)));

        music.MapGet("/songs/{id:int}/audio", async (int id, HttpContext http, IMusicService service) =>
            ToResult(await service.GetAudioUrlAsync(id, http.User), url => Results.Ok(new { audioUrl = url })));

        music.MapGet("/songs/{id:int}/videos", async (int id, HttpContext http, IMusicService service) =>
            ToResult(await service.GetVideosAsync(id, http.User)));

        music.MapGet("/statistics", async (HttpContext http, IMusicService service) =>
            ToResult(await service.GetStatisticsAsync(http.User)));

        music.MapGet("/members", async (HttpContext http, IMusicService service) =>
            ToResult(await service.GetMembersAsync(http.User)));

        music.MapGet("/albums/{id:int}/edit", async (int id, HttpContext http, IMusicService service) =>
            ToResult(await service.GetAlbumForEditAsync(id, http.User)));

        music.MapGet("/songs/{id:int}/edit", async (int id, HttpContext http, IMusicService service) =>
            ToResult(await service.GetSongForEditAsync(id, http.User)));

        var writes = music.MapGroup(string.Empty).AddEndpointFilter(RequireAntiforgery);

        writes.MapPost("/songs/{id:int}/plays", async (int id, HttpContext http, IMusicService service) =>
            ToResult(await service.PlayAsync(id, http.User, http.Connection.RemoteIpAddress?.ToString() ?? "unknown")))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        writes.MapPost("/videos/{id:int}/plays", async (int id, HttpContext http, IMusicService service) =>
            ToResult(await service.RecordVideoPlayAsync(id, http.User), counted => Results.Ok(new { counted })))
            .WithMetadata(new RequestSizeLimitAttribute(1024));

        // Album writes are multipart: the fields plus an optional cropped cover ("cover").
        writes.MapPost("/albums", async (IFormCollection form, HttpContext http, IMusicService service) =>
            {
                var (input, cover, errors) = ReadAlbumForm(form);
                return errors.Count > 0 ? Invalid(errors) : ToResult(await service.CreateAlbumAsync(input, cover, http.User),
                    album => Results.Created($"/api/music/albums/{album.Id}", album));
            })
            .WithMetadata(new RequestSizeLimitAttribute(MusicService.MaxCoverBytes + JsonLimit));

        writes.MapPut("/albums/{id:int}", async (int id, IFormCollection form, HttpContext http, IMusicService service) =>
            {
                var (input, cover, errors) = ReadAlbumForm(form);
                return errors.Count > 0 ? Invalid(errors) : ToResult(await service.UpdateAlbumAsync(id, input, cover, http.User));
            })
            .WithMetadata(new RequestSizeLimitAttribute(MusicService.MaxCoverBytes + JsonLimit));

        writes.MapDelete("/albums/{id:int}", async (int id, HttpContext http, IMusicService service) =>
            ToResult(await service.DeleteAlbumAsync(id, http.User), _ => Results.NoContent()));

        writes.MapPost("/albums/{id:int}/songs", async (int id, MusicSongInput input, HttpContext http, IMusicService service) =>
                ToResult(await service.CreateSongAsync(id, input, http.User),
                    songId => Results.Created($"/api/music/songs/{songId}/edit", new { id = songId })))
            .WithMetadata(new RequestSizeLimitAttribute(JsonLimit));

        writes.MapPut("/songs/{id:int}", async (int id, MusicSongInput input, HttpContext http, IMusicService service) =>
                ToResult(await service.UpdateSongAsync(id, input, http.User), songId => Results.Ok(new { id = songId })))
            .WithMetadata(new RequestSizeLimitAttribute(JsonLimit));

        writes.MapDelete("/songs/{id:int}", async (int id, HttpContext http, IMusicService service) =>
            ToResult(await service.DeleteSongAsync(id, http.User), _ => Results.NoContent()));

        writes.MapPost("/songs/{id:int}/videos", async (int id, IFormCollection form, HttpContext http, IMusicService service) =>
            {
                var file = form.Files.GetFile("file");
                if (file is null)
                {
                    return Invalid(new Dictionary<string, string[]> { ["file"] = new[] { "Escolha um ficheiro de vídeo." } });
                }

                await using var content = file.OpenReadStream();
                return ToResult(await service.UploadVideoAsync(id,
                        new MusicVideoUpload(content, file.FileName, file.ContentType ?? string.Empty, file.Length, form["title"].ToString()),
                        http.User),
                    video => Results.Created($"/api/music/songs/{id}/videos", video));
            })
            .WithMetadata(new RequestSizeLimitAttribute(MusicService.MaxVideoBytes + JsonLimit));

        writes.MapDelete("/videos/{id:int}", async (int id, HttpContext http, IMusicService service) =>
            ToResult(await service.DeleteVideoAsync(id, http.User), _ => Results.NoContent()));
    }

    /// <summary>No caching, and storage or database failures answer as a JSON problem, never a page or a stack trace.</summary>
    private static async ValueTask<object?> NoStore(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        try
        {
            return await next(context);
        }
        catch (Exception ex) when (!context.HttpContext.RequestAborted.IsCancellationRequested)
        {
            context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(MusicEndpoints))
                .LogError(ex, "Music API {Method} {Path} failed", context.HttpContext.Request.Method, context.HttpContext.Request.Path);
            return Results.Problem(title: "Não foi possível concluir. Tente novamente daqui a pouco.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Explicit CSRF check for every Music write, JSON and multipart alike: the token travels in the
    /// X-CSRF-TOKEN header (AddAntiforgery), bound to the caller's antiforgery cookie.
    /// </summary>
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

    private static (MusicAlbumInput Input, MusicCoverUpload? Cover, Dictionary<string, string[]> Errors) ReadAlbumForm(IFormCollection form)
    {
        var errors = new Dictionary<string, string[]>();
        int? year = null;
        var rawYear = form["year"].ToString();
        if (!string.IsNullOrWhiteSpace(rawYear))
        {
            if (int.TryParse(rawYear, out var parsed))
            {
                year = parsed;
            }
            else
            {
                errors["year"] = new[] { "Ano inválido." };
            }
        }

        var file = form.Files.GetFile("cover");
        var cover = file is null ? null : new MusicCoverUpload(file.OpenReadStream(), file.FileName, file.ContentType ?? string.Empty, file.Length);
        var input = new MusicAlbumInput(
            form["title"].ToString(),
            year,
            form["description"].ToString(),
            IsChecked(form["isPrivate"]),
            IsChecked(form["isExclusive"]),
            form["accessUserIds"].Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToList());
        return (input, cover, errors);
    }

    private static bool IsChecked(string? value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);

    private static IResult Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        Results.ValidationProblem(errors.ToDictionary(e => e.Key, e => e.Value), title: "Há campos por corrigir.");

    private static IResult ToResult<T>(MusicResult<T> result, Func<T, IResult>? ok = null) => result.Status switch
    {
        MusicResultStatus.Ok => ok is null ? Results.Ok(result.Value) : ok(result.Value!),
        MusicResultStatus.Invalid => Invalid(result.Errors ?? new Dictionary<string, string[]>()),
        MusicResultStatus.SignInRequired => Results.Problem(title: "Reservado a membros da RTUB. Entre para continuar.",
            statusCode: StatusCodes.Status401Unauthorized),
        MusicResultStatus.Forbidden => Results.Problem(title: "Não tem permissão para esta ação.",
            statusCode: StatusCodes.Status403Forbidden),
        MusicResultStatus.Unavailable => Results.Problem(title: "O áudio desta música não está disponível.",
            statusCode: StatusCodes.Status404NotFound, type: "music:audio-unavailable"),
        _ => Results.Problem(title: "Não encontrado.", statusCode: StatusCodes.Status404NotFound),
    };
}
