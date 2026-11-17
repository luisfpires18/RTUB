// Event admin: videos (012C).
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public sealed partial class EventAdminService
{
    // ---------- videos (012C) ----------

    public const long MaxVideoBytes = 100 * 1024 * 1024;

    public const int MaxVideoTitleLength = 200;

    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".m4v", ".webm", ".3gp", ".mkv", ".avi" };

    public async Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> GetVideosAsync(int id, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventManagedVideoDto>>(user) is { } refused)
        {
            return refused;
        }

        return await FindAsync(id) is null
            ? EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.NotFound)
            : EventResult<IReadOnlyList<EventManagedVideoDto>>.Ok(await VideosAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> AddVideoAsync(int id, EventVideoUpload upload, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventManagedVideoDto>>(user) is { } refused)
        {
            return refused;
        }

        var e = await FindAsync(id);
        if (e is null)
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.NotFound);
        }

        // The old page offered uploads only on past events (the videos button of the archive cards).
        if (!IsPast(e, Today))
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.Closed);
        }

        var extension = Path.GetExtension(upload.FileName ?? string.Empty).ToLowerInvariant();
        var isVideo = upload.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) || VideoExtensions.Contains(extension);
        if (upload.Length <= 0 || !isVideo)
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("file", "Escolha um ficheiro de vídeo.");
        }

        if (upload.Length > MaxVideoBytes)
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("file", "O vídeo não pode exceder 100 MB.");
        }

        // The old form required a title before it would send.
        var title = upload.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("title", "Indique o título do vídeo.");
        }

        if (title.Length > MaxVideoTitleLength)
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("title", $"O título não pode ter mais de {MaxVideoTitleLength} caracteres.");
        }

        // Storage key events/{env}/videos/{eventId}_{timestamp}_{file}, the next sort position and the
        // push to the other members all come from the existing EventService, as on the old page.
        await _events.AddVideoAsync(id, upload.Content, upload.FileName!, upload.ContentType, EventsAuthorization.UserId(user)!, title);
        return EventResult<IReadOnlyList<EventManagedVideoDto>>.Ok(await VideosAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> RenameVideoAsync(int id, int videoId, EventVideoTitleInput input, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventManagedVideoDto>>(user) is { } refused)
        {
            return refused;
        }

        if (!await VideoOfEventAsync(id, videoId))
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.NotFound);
        }

        var title = input.Title?.Trim();
        if (title is { Length: > MaxVideoTitleLength })
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("title", $"O título não pode ter mais de {MaxVideoTitleLength} caracteres.");
        }

        await _events.UpdateVideoTitleAsync(videoId, string.IsNullOrEmpty(title) ? null : title, EventsAuthorization.UserId(user)!, isAdmin: true);
        return EventResult<IReadOnlyList<EventManagedVideoDto>>.Ok(await VideosAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> ReorderVideosAsync(int id, EventVideoOrderInput input, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventManagedVideoDto>>(user) is { } refused)
        {
            return refused;
        }

        if (await FindAsync(id) is null)
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.NotFound);
        }

        // The whole list, each video once: a stale or partial order is refused rather than half applied.
        var current = (await VideosAsync(id)).Select(v => v.Id).ToHashSet();
        var order = input.VideoIds ?? Array.Empty<int>();
        if (order.Count != current.Count || order.Distinct().Count() != order.Count || !order.All(current.Contains))
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Invalid("videoIds", "A lista de vídeos mudou entretanto. Recarregue e tente outra vez.");
        }

        await _events.UpdateVideoOrderAsync(id, order.ToList());
        return EventResult<IReadOnlyList<EventManagedVideoDto>>.Ok(await VideosAsync(id));
    }

    public async Task<EventResult<IReadOnlyList<EventManagedVideoDto>>> DeleteVideoAsync(int id, int videoId, ClaimsPrincipal user)
    {
        if (Refusal<IReadOnlyList<EventManagedVideoDto>>(user) is { } refused)
        {
            return refused;
        }

        if (!await VideoOfEventAsync(id, videoId))
        {
            return EventResult<IReadOnlyList<EventManagedVideoDto>>.Fail(EventResultStatus.NotFound);
        }

        // Deletes the stored file first (only what this environment owns; a storage failure is logged
        // and the row still goes), then the row - EventService.DeleteVideoAsync, unchanged.
        await _events.DeleteVideoAsync(videoId, EventsAuthorization.UserId(user)!, isAdmin: true);
        return EventResult<IReadOnlyList<EventManagedVideoDto>>.Ok(await VideosAsync(id));
    }

    private async Task<bool> VideoOfEventAsync(int id, int videoId)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.EventVideos.AnyAsync(v => v.Id == videoId && v.EventId == id);
    }

    /// <summary>The page's order: SortOrder, then id.</summary>
    private async Task<IReadOnlyList<EventManagedVideoDto>> VideosAsync(int id)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.EventVideos.AsNoTracking().Where(v => v.EventId == id)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
            .Select(v => new EventManagedVideoDto(v.Id, v.Title))
            .ToListAsync();
    }
}
