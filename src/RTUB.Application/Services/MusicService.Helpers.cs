// Music: shared helpers (permissions, validation, cooldown, audit).
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class MusicService
{
    // ---------- helpers ----------

    private const string DefaultAvatar = "/images/default-avatar.webp";

    private static MusicPermissionsDto Permissions(ClaimsPrincipal user) => new(
        MusicAuthorization.IsMember(user),
        MusicAuthorization.CanManage(user),
        MusicAuthorization.CanDelete(user),
        MusicAuthorization.CanManageExclusive(user),
        MusicAuthorization.IsMember(user),
        MusicAuthorization.CanSeeDetailedStatistics(user));

    private static MusicAlbumSummaryDto CleanAlbum(MusicAlbumSummaryDto a) =>
        a with { Description = Blank(a.Description), CoverUrl = IsWebUrl(a.CoverUrl) ? a.CoverUrl : null };

    private bool TryStartCooldown(string key, TimeSpan window)
    {
        lock (CooldownGate)
        {
            if (_cache.TryGetValue(key, out _))
            {
                return false;
            }

            _cache.Set(key, true, window);
            return true;
        }
    }

    private async Task<string?> AudioUrlAsync(SongRef song)
    {
        if (!song.HasMusic)
        {
            return null;
        }

        try
        {
            return Blank(await _audio.GetAudioUrlAsync(song.AlbumTitle, song.TrackNumber, song.Title));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audio lookup failed for song {SongId}", song.Id);
            return null;
        }
    }

    private async Task AuditAsync(ClaimsPrincipal user, string entityType, int entityId, string displayName, string changes)
    {
        try
        {
            await _audit.AddAsync(new AuditLog
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = "Played",
                UserId = MusicAuthorization.UserId(user),
                UserName = user.Identity?.IsAuthenticated == true ? user.Identity.Name : null,
                Timestamp = DateTime.UtcNow,
                Changes = changes,
                EntityDisplayName = displayName,
                IsCriticalAction = false,
            });
        }
        catch (Exception ex)
        {
            // A play is never refused because its audit row could not be written.
            _logger.LogWarning(ex, "Failed to audit a {EntityType} play ({EntityId})", entityType, entityId);
        }
    }

    private static async Task<IReadOnlyList<MusicMemberDto>> MembersAsync(ApplicationDbContext ctx, IReadOnlyCollection<string>? only)
    {
        var query = ctx.Users.AsNoTracking();
        if (only is not null)
        {
            query = query.Where(u => only.Contains(u.Id));
        }

        var users = await query.Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl }).ToListAsync();
        return users
            .Select(u => new MusicMemberDto(u.Id,
                string.IsNullOrEmpty(u.Nickname) ? $"{u.FirstName} {u.LastName}".Trim() : $"{u.Nickname} ({u.FirstName} {u.LastName})".Trim(),
                string.IsNullOrEmpty(u.ImageUrl) ? DefaultAvatar : u.ImageUrl))
            .OrderBy(m => m.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<List<string>> ExistingUserIdsAsync(IReadOnlyList<string>? ids)
    {
        var wanted = ids?.Where(i => !string.IsNullOrWhiteSpace(i)).Distinct().ToList() ?? new List<string>();
        if (wanted.Count == 0)
        {
            return wanted;
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        return await ctx.Users.AsNoTracking().Where(u => wanted.Contains(u.Id)).Select(u => u.Id).ToListAsync();
    }

    private static Dictionary<string, string[]> ValidateAlbum(MusicAlbumInput input, MusicCoverUpload? cover)
    {
        var errors = new Dictionary<string, string[]>();
        var title = input.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            errors["title"] = new[] { "O título do álbum é obrigatório." };
        }
        else if (title.Length > 200)
        {
            errors["title"] = new[] { "O título do álbum não pode exceder 200 caracteres." };
        }

        if (input.Year is { } year && (year < 1900 || year > DateTime.UtcNow.Year))
        {
            errors["year"] = new[] { $"O ano deve estar entre 1900 e {DateTime.UtcNow.Year}." };
        }

        if (input.Description?.Trim() is { Length: > 1000 })
        {
            errors["description"] = new[] { "A descrição não pode exceder 1000 caracteres." };
        }

        if (cover is not null)
        {
            if (!CoverTypes.Contains(cover.ContentType, StringComparer.OrdinalIgnoreCase) || cover.Length <= 0)
            {
                errors["cover"] = new[] { "A capa tem de ser uma imagem WebP, JPEG ou PNG." };
            }
            else if (cover.Length > MaxCoverBytes)
            {
                errors["cover"] = new[] { "A capa não pode exceder 5 MB." };
            }
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateSong(MusicSongInput input)
    {
        var errors = new Dictionary<string, string[]>();
        var title = input.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            errors["title"] = new[] { "O título da música é obrigatório." };
        }
        else if (title.Length > 200)
        {
            errors["title"] = new[] { "O título da música não pode exceder 200 caracteres." };
        }

        if (input.TrackNumber is { } track && (track < 1 || track > 999))
        {
            errors["trackNumber"] = new[] { "O número da faixa deve estar entre 1 e 999." };
        }

        foreach (var (field, value) in new[] { ("lyricAuthor", input.LyricAuthor), ("musicAuthor", input.MusicAuthor), ("adaptation", input.Adaptation) })
        {
            if (value?.Trim() is { Length: > 200 })
            {
                errors[field] = new[] { "Não pode exceder 200 caracteres." };
            }
        }

        if (Blank(input.SpotifyUrl?.Trim()) is { } spotify && (spotify.Length > 500 || !IsWebUrl(spotify)))
        {
            errors["spotifyUrl"] = new[] { "Indique um endereço http(s) válido, até 500 caracteres." };
        }

        var youTube = input.YouTubeUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToList() ?? new List<string>();
        if (youTube.Count > MaxYouTubeUrls)
        {
            errors["youTubeUrls"] = new[] { $"No máximo {MaxYouTubeUrls} endereços do YouTube." };
        }
        else if (youTube.Any(u => u.Length > 500 || !IsWebUrl(u)))
        {
            errors["youTubeUrls"] = new[] { "Cada endereço do YouTube tem de ser http(s), até 500 caracteres." };
        }

        if (input.Lyrics is { Length: > 10000 })
        {
            errors["lyrics"] = new[] { "A letra não pode exceder 10000 caracteres." };
        }

        return errors;
    }

    private static List<string> CleanUrls(IReadOnlyList<string>? urls) =>
        urls?.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();

    /// <summary>Only absolute http(s) links reach the browser: a stored "javascript:" link is dropped.</summary>
    private static IReadOnlyList<MusicLinkDto> Links(string? spotify, IEnumerable<string> youTube)
    {
        var links = new List<MusicLinkDto>();
        if (IsWebUrl(spotify))
        {
            links.Add(new MusicLinkDto("spotify", spotify!.Trim()));
        }

        links.AddRange(youTube.Where(IsWebUrl).Select(u => new MusicLinkDto("youtube", u.Trim())));
        return links;
    }

    private static bool IsWebUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static string CoverFileName(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/jpeg" => "album-cover.jpg",
        "image/png" => "album-cover.png",
        _ => "album-cover.webp",
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
