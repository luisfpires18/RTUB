using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// The old Blazor /member/gallery management behind the React /gallery (React track 015, docs/react-gallery.md).
/// Same behaviour, rules now server-side:
/// - any signed-in member uploads an image or a video (by its type; images ≤10 MB, videos ≤100 MB or
///   GalleryMedia:MaxVideoSize), with a title (≤200), a date, members-only (the default) or public, and who appears;
///   the people tagged at upload get a push ("Foste marcado..."), as before - later tag changes send nothing;
/// - the uploader, Admin or Owner (was Admin only) edit the title, date, members-only flag and tags, or delete:
///   the stored file first (never a file this environment does not own), then the row and its tags.
/// No crop, no thumbnail (the old flow had neither). No schema change.
/// </summary>
public sealed class GalleryManagementService : IGalleryManagementService
{
    public const int TitleMax = 200;
    public const int SearchLimit = 20;

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IGalleryMediaService _media;
    private readonly IGalleryMediaStorageService _storage;
    private readonly IGalleryTimelineService _timeline;
    private readonly IPushNotificationService _push;
    private readonly IPushNotificationFactory _pushFactory;
    private readonly ILogger<GalleryManagementService> _logger;

    public GalleryManagementService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IGalleryMediaService media,
        IGalleryMediaStorageService storage,
        IGalleryTimelineService timeline,
        IPushNotificationService push,
        IPushNotificationFactory pushFactory,
        ILogger<GalleryManagementService> logger)
    {
        _contexts = contexts;
        _media = media;
        _storage = storage;
        _timeline = timeline;
        _push = push;
        _pushFactory = pushFactory;
        _logger = logger;
    }

    public async Task<EventResult<IReadOnlyList<GalleryTaggableDto>>> SearchPeopleAsync(string? query, ClaimsPrincipal user)
    {
        if (!GalleryAuthorization.IsMember(user))
        {
            return EventResult<IReadOnlyList<GalleryTaggableDto>>.Fail(EventResultStatus.SignInRequired);
        }

        var q = EventParticipantsAdminService.Fold(query);
        if (q.Length == 0)
        {
            return EventResult<IReadOnlyList<GalleryTaggableDto>>.Ok(Array.Empty<GalleryTaggableDto>());
        }

        await using var db = await _contexts.CreateDbContextAsync();
        // ponytail: every member in memory (~100) to fold accents the Portuguese way; page it if it grows.
        var members = await db.Users.AsNoTracking()
            .Where(u => !u.IsExpelled)
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl })
            .ToListAsync();

        return EventResult<IReadOnlyList<GalleryTaggableDto>>.Ok(members
            .Select(u => (u, who: EventDiscussionBoardService.Author(u.Nickname, u.FirstName, u.LastName, u.ImageUrl, null, null)))
            .Where(x => EventParticipantsAdminService.Fold(x.u.Nickname).Contains(q)
                        || EventParticipantsAdminService.Fold($"{x.u.FirstName} {x.u.LastName}").Contains(q))
            .OrderBy(x => x.who.Name, StringComparer.Create(CultureInfo.GetCultureInfo("pt-PT"), true))
            .Take(SearchLimit)
            .Select(x => new GalleryTaggableDto(x.u.Id, x.who.Name, x.who.FullName, x.who.AvatarUrl))
            .ToList());
    }

    public async Task<EventResult<GalleryEditDto>> GetForEditAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<GalleryEditDto>(id, user) is { } refused)
        {
            return refused;
        }

        var m = (await _media.GetByIdAsync(id))!;
        var item = await _timeline.GetItemAsync(id, user);
        return EventResult<GalleryEditDto>.Ok(new GalleryEditDto(
            m.Id, m.Title, m.MediaType == MediaType.Video ? "video" : "image", item?.Url, DateText(m.Year, m.Month, m.Day), m.IsPrivate,
            item?.People ?? Array.Empty<GalleryPersonDto>()));
    }

    public async Task<EventResult<GalleryItemDto>> UploadAsync(GalleryUpload upload, ClaimsPrincipal user)
    {
        if (GalleryAuthorization.UserId(user) is not { } me)
        {
            return EventResult<GalleryItemDto>.Fail(EventResultStatus.SignInRequired);
        }

        var errors = new Dictionary<string, string[]>();
        var contentType = upload.ContentType ?? string.Empty;
        MediaType? type = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? MediaType.Image
            : contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) ? MediaType.Video
            : null;
        if (type is null)
        {
            errors["file"] = new[] { "Formato de ficheiro não suportado. Apenas imagens e vídeos são aceites." };
        }
        else if (upload.Length <= 0)
        {
            errors["file"] = new[] { "O ficheiro está vazio." };
        }
        else if (upload.Length > _storage.GetMaxFileSize(type.Value))
        {
            errors["file"] = new[] { $"O tamanho do ficheiro deve ser inferior a {_storage.GetMaxFileSize(type.Value) / (1024 * 1024)} MB." };
        }

        var details = await DetailErrorsAsync(upload.Title, upload.Date, upload.PersonIds, Array.Empty<string>());
        foreach (var (key, value) in details.Errors)
        {
            errors[key] = value;
        }

        if (errors.Count > 0)
        {
            return new EventResult<GalleryItemDto>(EventResultStatus.Invalid, Errors: errors);
        }

        var (year, month, day) = details.Date!.Value;
        var title = upload.Title!.Trim();
        var url = await _storage.UploadMediaAsync(upload.Content, upload.FileName, contentType, type!.Value, title, year, month, day);
        var media = await _media.CreateAsync(GalleryMedia.Create(me, title, type.Value, url, year, month, day, null, null, upload.MembersOnly));
        var tagged = upload.PersonIds.Distinct().ToList();
        if (tagged.Count > 0)
        {
            await _media.AddPersonTagsAsync(media.Id, tagged);
            try
            {
                // As before: only the people tagged on upload hear about it (the uploader too, if tagged).
                await _push.SendToSelectedUsersAsync(tagged, _pushFactory.CreateGalleryTagNotification("/"));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gallery tag notification failed for media {MediaId} (non-critical)", media.Id);
            }
        }

        return EventResult<GalleryItemDto>.Ok((await _timeline.GetItemAsync(media.Id, user))!);
    }

    public async Task<EventResult<GalleryItemDto>> UpdateAsync(int id, GalleryEditInput input, ClaimsPrincipal user)
    {
        if (await RefusalAsync<GalleryItemDto>(id, user) is { } refused)
        {
            return refused;
        }

        var media = (await _media.GetByIdAsync(id))!;
        var current = media.PeopleInMedia.Select(p => p.UserId).ToList();
        var wanted = (input.PersonIds ?? Array.Empty<string>()).Distinct().ToList();
        var details = await DetailErrorsAsync(input.Title, input.Date, wanted, current);
        if (details.Errors.Count > 0)
        {
            return new EventResult<GalleryItemDto>(EventResultStatus.Invalid, Errors: details.Errors);
        }

        var (year, month, day) = details.Date!.Value;
        media.UpdateDetails(input.Title!.Trim(), year, month, day);
        media.UpdatePrivacy(input.MembersOnly);
        await _media.UpdateAsync(media);

        var add = wanted.Except(current).ToList();
        var remove = current.Except(wanted).ToList();
        if (add.Count > 0)
        {
            await _media.AddPersonTagsAsync(id, add);
        }

        if (remove.Count > 0)
        {
            await _media.RemovePersonTagsAsync(id, remove);
        }

        return EventResult<GalleryItemDto>.Ok((await _timeline.GetItemAsync(id, user))!);
    }

    public async Task<EventResult<bool>> DeleteAsync(int id, ClaimsPrincipal user)
    {
        if (await RefusalAsync<bool>(id, user) is { } refused)
        {
            return refused;
        }

        var media = (await _media.GetByIdAsync(id))!;
        // As before: the stored file first; if storage fails the row stays, so nothing points at a lost file.
        await _storage.DeleteMediaAsync(media.MediaUrl);
        await _media.DeleteAsync(id);
        return EventResult<bool>.Ok(true);
    }

    // ---------- rules ----------

    private async Task<EventResult<T>?> RefusalAsync<T>(int id, ClaimsPrincipal user)
    {
        if (!GalleryAuthorization.IsMember(user))
        {
            return EventResult<T>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var uploader = await db.GalleryMedia.Where(m => m.Id == id).Select(m => m.UploaderId).FirstOrDefaultAsync();
        return uploader is null ? EventResult<T>.Fail(EventResultStatus.NotFound)
            : !GalleryAuthorization.CanEdit(user, uploader) ? EventResult<T>.Fail(EventResultStatus.Forbidden)
            : null;
    }

    private sealed record Details(Dictionary<string, string[]> Errors, (int Year, byte? Month, byte? Day)? Date);

    /// <summary>Title, date and tags; a new tag must be a member who is not expelled (existing tags are kept as they are).</summary>
    private async Task<Details> DetailErrorsAsync(string? title, string? date, IReadOnlyList<string> personIds, IReadOnlyList<string> existing)
    {
        var errors = new Dictionary<string, string[]>();
        var text = title?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            errors["title"] = new[] { "Indique um título." };
        }
        else if (text.Length > TitleMax)
        {
            errors["title"] = new[] { $"O título não pode exceder {TitleMax} caracteres." };
        }

        var parts = ParseDate(date);
        if (parts is null)
        {
            errors["date"] = new[] { "Indique a data." };
        }
        else if (parts.Value.Year < 1900 || parts.Value.Year > DateTime.UtcNow.Year + 1)
        {
            errors["date"] = new[] { $"O ano deve estar entre 1900 e {DateTime.UtcNow.Year + 1}." };
        }

        var fresh = personIds.Except(existing).Distinct().ToList();
        if (fresh.Count > 0)
        {
            await using var db = await _contexts.CreateDbContextAsync();
            var known = await db.Users.CountAsync(u => fresh.Contains(u.Id) && !u.IsExpelled);
            if (known != fresh.Count)
            {
                errors["personIds"] = new[] { "Escolha as pessoas da lista." };
            }
        }

        return new Details(errors, errors.ContainsKey("date") ? null : parts);
    }

    /// <summary>The old form's rule: 1 January keeps only the year; the 1st of another month the year and month.</summary>
    internal static (int Year, byte? Month, byte? Day)? ParseDate(string? text)
    {
        if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return null;
        }

        return d.Month == 1 && d.Day == 1 ? (d.Year, null, null)
            : d.Day == 1 ? (d.Year, (byte)d.Month, null)
            : (d.Year, (byte)d.Month, (byte)d.Day);
    }

    /// <summary>The form's date for an item: the missing day or month read as the 1st, as the old form did.</summary>
    private static string DateText(int year, byte? month, byte? day) =>
        new DateTime(year, month ?? 1, day is { } d && month is not null ? d : 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
