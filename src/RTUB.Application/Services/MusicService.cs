using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// The React Music area (React track 006, docs/react-music.md): viewer-aware reads, the play
/// cooldown, and writes that go through the existing <see cref="IAlbumService"/> and
/// <see cref="ISongService"/> so audit logging, storage clean-up and video push notifications stay
/// exactly where they were. No schema change: everything maps onto the existing Music tables.
/// </summary>
public sealed class MusicService : IMusicService
{
    public const int DefaultCooldownSeconds = 30;
    public const long MaxCoverBytes = 5 * 1024 * 1024;
    public const long MaxVideoBytes = 100 * 1024 * 1024;
    private const int MaxYouTubeUrls = 10;

    private static readonly string[] CoverTypes = { "image/webp", "image/jpeg", "image/png" };
    private static readonly string[] VideoExtensions = { ".mp4", ".mov", ".m4v", ".webm", ".3gp", ".mkv", ".avi" };

    // ponytail: one in-process lock and IMemoryCache, so the cooldown is per app instance; a
    // scaled-out App Service would need a shared cache to stop a listener spreading plays across instances.
    private static readonly object CooldownGate = new();

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IAlbumService _albums;
    private readonly ISongService _songs;
    private readonly IAudioStorageService _audio;
    private readonly ILyricStorageService _lyrics;
    private readonly IImageStorageService _images;
    private readonly ISongContentService _songContent;
    private readonly IAuditLogService _audit;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MusicService> _logger;

    public MusicService(
        IDbContextFactory<ApplicationDbContext> contexts,
        IAlbumService albums,
        ISongService songs,
        IAudioStorageService audio,
        ILyricStorageService lyrics,
        IImageStorageService images,
        ISongContentService songContent,
        IAuditLogService audit,
        IMemoryCache cache,
        ILogger<MusicService> logger)
    {
        _contexts = contexts;
        _albums = albums;
        _songs = songs;
        _audio = audio;
        _lyrics = lyrics;
        _images = images;
        _songContent = songContent;
        _audit = audit;
        _cache = cache;
        _logger = logger;
    }

    // ---------- reads ----------

    public async Task<MusicAlbumListDto> GetAlbumsAsync(ClaimsPrincipal user)
    {
        await using var ctx = await _contexts.CreateDbContextAsync();
        var albums = await Visible(ctx, user)
            .Select(a => new MusicAlbumSummaryDto(a.Id, a.Title, a.Year, a.Description, a.ImageUrl,
                a.IsPrivate, a.IsExclusive, a.Songs.Count))
            .ToListAsync();

        return new MusicAlbumListDto(albums.Select(CleanAlbum).OrderBy(a => a.Year is null).ThenBy(a => a.Year).ThenBy(a => a.Id).ToList(),
            Permissions(user));
    }

    public async Task<MusicResult<MusicAlbumDetailDto>> GetAlbumAsync(int albumId, ClaimsPrincipal user)
    {
        await using var ctx = await _contexts.CreateDbContextAsync();
        var access = await AlbumAccessAsync(ctx, albumId, user);
        if (access != MusicResultStatus.Ok)
        {
            return MusicResult<MusicAlbumDetailDto>.Fail(access);
        }

        var album = await ctx.Albums.AsNoTracking().Where(a => a.Id == albumId)
            .Select(a => new MusicAlbumSummaryDto(a.Id, a.Title, a.Year, a.Description, a.ImageUrl,
                a.IsPrivate, a.IsExclusive, a.Songs.Count))
            .FirstAsync();

        var songs = await ctx.Songs.AsNoTracking().Where(s => s.AlbumId == albumId)
            .Select(s => new
            {
                s.Id, s.Title, s.TrackNumber, s.LyricAuthor, s.MusicAuthor, s.Adaptation, s.HasMusic, s.SpotifyUrl,
                YouTube = s.YouTubeUrls.OrderBy(y => y.Id).Select(y => y.Url).ToList(),
                Plays = s.PlayCounts.Count(),
                Videos = s.Videos.Count(),
            })
            .ToListAsync();

        var member = MusicAuthorization.IsMember(user);
        var dto = songs
            .OrderBy(s => s.TrackNumber is null).ThenBy(s => s.TrackNumber).ThenBy(s => s.Id)
            .Select(s => new MusicSongDto(s.Id, s.Title, s.TrackNumber, Blank(s.LyricAuthor), Blank(s.MusicAuthor),
                Blank(s.Adaptation), s.HasMusic, s.Plays, Links(s.SpotifyUrl, s.YouTube), member ? s.Videos : null))
            .ToList();

        return MusicResult<MusicAlbumDetailDto>.Ok(new MusicAlbumDetailDto(CleanAlbum(album), dto, Permissions(user)));
    }

    public async Task<MusicResult<MusicLyricsDto>> GetLyricsAsync(int songId, ClaimsPrincipal user)
    {
        var (status, song) = await FindSongAsync(songId, user, includeLyrics: true);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<MusicLyricsDto>.Fail(status);
        }

        string? pdf = null;
        try
        {
            pdf = await _lyrics.GetLyricPdfUrlAsync(song!.AlbumTitle, song.Title);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lyric PDF lookup failed for song {SongId}", songId);
        }

        return MusicResult<MusicLyricsDto>.Ok(new MusicLyricsDto(Blank(pdf), Blank(song!.Lyrics)));
    }

    public async Task<MusicResult<string>> GetAudioUrlAsync(int songId, ClaimsPrincipal user)
    {
        var (status, song) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<string>.Fail(status);
        }

        var url = await AudioUrlAsync(song!);
        return url is null ? MusicResult<string>.Fail(MusicResultStatus.Unavailable) : MusicResult<string>.Ok(url);
    }

    public async Task<MusicResult<MusicPlayDto>> PlayAsync(int songId, ClaimsPrincipal user, string listenerKey)
    {
        var (status, song) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<MusicPlayDto>.Fail(status);
        }

        var url = await AudioUrlAsync(song!);
        if (url is null)
        {
            return MusicResult<MusicPlayDto>.Fail(MusicResultStatus.Unavailable);
        }

        var userId = MusicAuthorization.UserId(user);
        var listener = userId ?? $"anon:{listenerKey}";
        var counted = TryStartCooldown($"music:play:{listener}:{song!.Id}",
            TimeSpan.FromSeconds(song.Duration is > 0 ? song.Duration.Value : DefaultCooldownSeconds));

        if (counted)
        {
            await _songs.IncrementPlayCountAsync(song.Id, userId);
            await AuditAsync(user, "Song", song.Id, $"{song.Title} ({song.AlbumTitle})",
                $"Played \"{song.Title}\" from album \"{song.AlbumTitle}\".");
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        var plays = await ctx.SongPlayCounts.CountAsync(p => p.SongId == song.Id);
        return MusicResult<MusicPlayDto>.Ok(new MusicPlayDto(url, counted, plays));
    }

    public async Task<MusicResult<IReadOnlyList<MusicVideoDto>>> GetVideosAsync(int songId, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.IsMember(user))
        {
            return MusicResult<IReadOnlyList<MusicVideoDto>>.Fail(MusicResultStatus.SignInRequired);
        }

        var (status, song) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<IReadOnlyList<MusicVideoDto>>.Fail(status);
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        var videos = await ctx.SongVideos.AsNoTracking().Where(v => v.SongId == songId)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Id).ToListAsync();

        var userId = MusicAuthorization.UserId(user);
        var admin = MusicAuthorization.IsAdmin(user);
        var forTitles = new Song { Title = song!.Title };
        return MusicResult<IReadOnlyList<MusicVideoDto>>.Ok(videos
            .Where(v => IsWebUrl(v.Url))
            .Select((v, i) => new MusicVideoDto(v.Id, _songContent.GetCleanVideoTitle(v, forTitles, i + 1), v.Url,
                v.MimeType, admin || v.CreatedByUserId == userId))
            .ToList());
    }

    public async Task<MusicResult<bool>> RecordVideoPlayAsync(int videoId, ClaimsPrincipal user)
    {
        var (status, video, song) = await FindVideoAsync(videoId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<bool>.Fail(status);
        }

        var counted = TryStartCooldown($"music:video:{MusicAuthorization.UserId(user)}:{videoId}",
            TimeSpan.FromSeconds(DefaultCooldownSeconds));
        if (counted)
        {
            await AuditAsync(user, "SongVideo", video!.Id, $"{video.Title ?? "Video"} ({song!.Title})",
                $"Played video \"{video.Title ?? "Untitled"}\" from song \"{song.Title}\".");
        }

        return MusicResult<bool>.Ok(counted);
    }

    public async Task<MusicResult<MusicStatisticsDto>> GetStatisticsAsync(ClaimsPrincipal user)
    {
        if (!MusicAuthorization.IsMember(user))
        {
            return MusicResult<MusicStatisticsDto>.Fail(MusicResultStatus.SignInRequired);
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        var albums = await Visible(ctx, user).Select(a => new { a.Id, a.Title, a.Year }).ToDictionaryAsync(a => a.Id);
        var albumIds = albums.Keys.ToList();
        var songs = await ctx.Songs.AsNoTracking().Where(s => albumIds.Contains(s.AlbumId))
            .Select(s => new { s.Id, s.Title, s.AlbumId }).ToDictionaryAsync(s => s.Id);
        var songIds = songs.Keys.ToList();

        var perSong = await ctx.SongPlayCounts.AsNoTracking().Where(p => songIds.Contains(p.SongId))
            .GroupBy(p => p.SongId).Select(g => new { SongId = g.Key, Plays = g.Count() }).ToListAsync();

        var songRanks = perSong
            .Select(p => new MusicSongRankDto(p.SongId, songs[p.SongId].Title, songs[p.SongId].AlbumId,
                albums[songs[p.SongId].AlbumId].Title, p.Plays))
            .OrderByDescending(r => r.PlayCount).ThenBy(r => r.Title, StringComparer.Ordinal).ThenBy(r => r.SongId)
            .ToList();

        var albumRanks = songRanks.GroupBy(r => r.AlbumId)
            .Select(g => new MusicAlbumRankDto(g.Key, albums[g.Key].Title, albums[g.Key].Year, g.Sum(r => r.PlayCount)))
            .OrderByDescending(r => r.PlayCount).ThenBy(r => r.Title, StringComparer.Ordinal).ThenBy(r => r.AlbumId)
            .ToList();

        List<MusicMemberStatsDto>? members = null;
        if (MusicAuthorization.CanSeeDetailedStatistics(user))
        {
            var perMember = await ctx.SongPlayCounts.AsNoTracking()
                .Where(p => p.UserId != null && songIds.Contains(p.SongId))
                .GroupBy(p => new { p.UserId, p.SongId })
                .Select(g => new { g.Key.UserId, g.Key.SongId, Plays = g.Count() })
                .ToListAsync();
            var userIds = perMember.Select(p => p.UserId!).Distinct().ToList();
            var people = await ctx.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl })
                .ToDictionaryAsync(u => u.Id);

            members = perMember.Where(p => people.ContainsKey(p.UserId!))
                .GroupBy(p => p.UserId!)
                .Select(g =>
                {
                    var person = people[g.Key];
                    var fullName = Blank($"{person.FirstName} {person.LastName}".Trim());
                    var rows = g.Select(p => new MusicMemberSongDto(songs[p.SongId].Title, albums[songs[p.SongId].AlbumId].Title, p.Plays))
                        .OrderByDescending(s => s.PlayCount).ThenBy(s => s.SongTitle, StringComparer.Ordinal).ToList();
                    return new MusicMemberStatsDto(Blank(person.Nickname) ?? fullName ?? "Membro", fullName,
                        string.IsNullOrEmpty(person.ImageUrl) ? DefaultAvatar : person.ImageUrl, rows.Sum(s => s.PlayCount), rows);
                })
                .OrderByDescending(m => m.TotalPlays).ThenBy(m => m.DisplayName, StringComparer.Ordinal)
                .ToList();
        }

        return MusicResult<MusicStatisticsDto>.Ok(new MusicStatisticsDto(songRanks, albumRanks, members));
    }

    // ---------- album management ----------

    public async Task<MusicResult<MusicAlbumEditDto>> GetAlbumForEditAsync(int albumId, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanManage(user))
        {
            return MusicResult<MusicAlbumEditDto>.Fail(MusicResultStatus.Forbidden);
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        var access = await AlbumAccessAsync(ctx, albumId, user);
        if (access != MusicResultStatus.Ok)
        {
            return MusicResult<MusicAlbumEditDto>.Fail(access);
        }

        var album = await ctx.Albums.AsNoTracking().FirstAsync(a => a.Id == albumId);
        IReadOnlyList<MusicMemberDto> members = Array.Empty<MusicMemberDto>();
        if (MusicAuthorization.CanManageExclusive(user))
        {
            var ids = await ctx.AlbumAccesses.AsNoTracking().Where(x => x.AlbumId == albumId).Select(x => x.UserId).ToListAsync();
            members = await MembersAsync(ctx, ids);
        }

        return MusicResult<MusicAlbumEditDto>.Ok(new MusicAlbumEditDto(album.Id, album.Title, album.Year, album.Description,
            album.IsPrivate, album.IsExclusive, Blank(album.ImageUrl), members));
    }

    public async Task<MusicResult<IReadOnlyList<MusicMemberDto>>> GetMembersAsync(ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanManageExclusive(user))
        {
            return MusicResult<IReadOnlyList<MusicMemberDto>>.Fail(MusicResultStatus.Forbidden);
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        return MusicResult<IReadOnlyList<MusicMemberDto>>.Ok(await MembersAsync(ctx, null));
    }

    public async Task<MusicResult<MusicAlbumSummaryDto>> CreateAlbumAsync(MusicAlbumInput input, MusicCoverUpload? cover, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanManage(user))
        {
            return MusicResult<MusicAlbumSummaryDto>.Fail(MusicResultStatus.Forbidden);
        }

        var errors = ValidateAlbum(input, cover);
        if (errors.Count > 0)
        {
            return MusicResult<MusicAlbumSummaryDto>.Invalid(errors);
        }

        var title = input.Title!.Trim();
        var exclusive = MusicAuthorization.CanManageExclusive(user) && input.IsExclusive;
        string? imageUrl = null;
        if (cover is not null)
        {
            imageUrl = await _images.UploadImageAsync(cover.Content, CoverFileName(cover.ContentType), cover.ContentType,
                "albums", S3KeyNormalizer.NormalizeForS3Key(title));
        }

        var album = await _albums.CreateAlbumAsync(title, input.Year, Blank(input.Description?.Trim()), imageUrl, input.IsPrivate, exclusive);
        if (exclusive)
        {
            await _albums.UpdateAlbumAccessAsync(album.Id, await ExistingUserIdsAsync(input.AccessUserIds));
        }

        return MusicResult<MusicAlbumSummaryDto>.Ok(CleanAlbum(new MusicAlbumSummaryDto(album.Id, album.Title, album.Year,
            album.Description, album.ImageUrl, album.IsPrivate, album.IsExclusive, 0)));
    }

    public async Task<MusicResult<MusicAlbumSummaryDto>> UpdateAlbumAsync(int albumId, MusicAlbumInput input, MusicCoverUpload? cover, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanManage(user))
        {
            return MusicResult<MusicAlbumSummaryDto>.Fail(MusicResultStatus.Forbidden);
        }

        Album existing;
        await using (var ctx = await _contexts.CreateDbContextAsync())
        {
            var access = await AlbumAccessAsync(ctx, albumId, user);
            if (access != MusicResultStatus.Ok)
            {
                return MusicResult<MusicAlbumSummaryDto>.Fail(access);
            }

            existing = await ctx.Albums.AsNoTracking().FirstAsync(a => a.Id == albumId);
        }

        var errors = ValidateAlbum(input, cover);
        if (errors.Count > 0)
        {
            return MusicResult<MusicAlbumSummaryDto>.Invalid(errors);
        }

        var title = input.Title!.Trim();
        var description = Blank(input.Description?.Trim());
        var owner = MusicAuthorization.CanManageExclusive(user);
        // Only an Owner changes exclusivity; everyone else keeps the album's current setting (the
        // retired page reset it to false whenever a Mod or Admin saved an exclusive album).
        var exclusive = owner ? input.IsExclusive : existing.IsExclusive;

        if (cover is null || exclusive != existing.IsExclusive)
        {
            await _albums.UpdateAlbumAsync(albumId, title, input.Year, description, input.IsPrivate, exclusive);
        }

        if (cover is not null)
        {
            // Keeps the exclusivity just saved, replaces the old cover in storage.
            await _albums.UpdateAlbumWithCoverAsync(albumId, title, input.Year, description, input.IsPrivate,
                cover.Content, CoverFileName(cover.ContentType), cover.ContentType);
        }

        if (owner && exclusive)
        {
            await _albums.UpdateAlbumAccessAsync(albumId, await ExistingUserIdsAsync(input.AccessUserIds));
        }

        var list = await GetAlbumsAsync(user);
        var saved = list.Albums.FirstOrDefault(a => a.Id == albumId);
        return saved is null ? MusicResult<MusicAlbumSummaryDto>.Fail(MusicResultStatus.NotFound) : MusicResult<MusicAlbumSummaryDto>.Ok(saved);
    }

    public async Task<MusicResult<bool>> DeleteAlbumAsync(int albumId, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanDelete(user))
        {
            return MusicResult<bool>.Fail(MusicResultStatus.Forbidden);
        }

        await using (var ctx = await _contexts.CreateDbContextAsync())
        {
            var access = await AlbumAccessAsync(ctx, albumId, user);
            if (access != MusicResultStatus.Ok)
            {
                return MusicResult<bool>.Fail(access);
            }
        }

        // Cascades to its songs, links, videos and play history (EF configuration), and deletes the cover.
        await _albums.DeleteAlbumAsync(albumId);
        return MusicResult<bool>.Ok(true);
    }

    // ---------- song management ----------

    public async Task<MusicResult<MusicSongEditDto>> GetSongForEditAsync(int songId, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanManage(user))
        {
            return MusicResult<MusicSongEditDto>.Fail(MusicResultStatus.Forbidden);
        }

        var (status, _) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<MusicSongEditDto>.Fail(status);
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        var song = await ctx.Songs.AsNoTracking().Include(s => s.YouTubeUrls).FirstAsync(s => s.Id == songId);
        return MusicResult<MusicSongEditDto>.Ok(new MusicSongEditDto(song.Id, song.Title, song.TrackNumber, song.LyricAuthor,
            song.MusicAuthor, song.Adaptation, song.SpotifyUrl, song.HasMusic,
            song.YouTubeUrls.OrderBy(y => y.Id).Select(y => y.Url).ToList(), song.Lyrics));
    }

    public async Task<MusicResult<int>> CreateSongAsync(int albumId, MusicSongInput input, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanManage(user))
        {
            return MusicResult<int>.Fail(MusicResultStatus.Forbidden);
        }

        await using (var ctx = await _contexts.CreateDbContextAsync())
        {
            var access = await AlbumAccessAsync(ctx, albumId, user);
            if (access != MusicResultStatus.Ok)
            {
                return MusicResult<int>.Fail(access);
            }
        }

        var errors = ValidateSong(input);
        if (errors.Count > 0)
        {
            return MusicResult<int>.Invalid(errors);
        }

        var song = await _songs.CreateSongAsync(input.Title!.Trim(), albumId, input.TrackNumber, Blank(input.LyricAuthor?.Trim()),
            Blank(input.MusicAuthor?.Trim()), Blank(input.Adaptation?.Trim()), null, Blank(input.SpotifyUrl?.Trim()), input.HasAudio);

        if (Blank(input.Lyrics) is { } lyrics)
        {
            await _songs.SetSongLyricsAsync(song.Id, lyrics);
        }

        foreach (var url in CleanUrls(input.YouTubeUrls))
        {
            await _songs.AddYouTubeUrlAsync(song.Id, url);
        }

        return MusicResult<int>.Ok(song.Id);
    }

    public async Task<MusicResult<int>> UpdateSongAsync(int songId, MusicSongInput input, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanManage(user))
        {
            return MusicResult<int>.Fail(MusicResultStatus.Forbidden);
        }

        var (status, song) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<int>.Fail(status);
        }

        var errors = ValidateSong(input);
        if (errors.Count > 0)
        {
            return MusicResult<int>.Invalid(errors);
        }

        List<string> oldUrls;
        await using (var ctx = await _contexts.CreateDbContextAsync())
        {
            oldUrls = await ctx.SongYouTubeUrls.AsNoTracking().Where(y => y.SongId == songId).Select(y => y.Url).ToListAsync();
        }

        // Duration is not edited here, as before; it is carried over.
        await _songs.UpdateSongAsync(songId, input.Title!.Trim(), input.TrackNumber, Blank(input.LyricAuthor?.Trim()),
            Blank(input.MusicAuthor?.Trim()), Blank(input.Adaptation?.Trim()), song!.Duration);
        await _songs.SetSongSpotifyUrlAsync(songId, Blank(input.SpotifyUrl?.Trim()));
        await _songs.SetSongHasMusicAsync(songId, input.HasAudio);
        await _songs.SetSongLyricsAsync(songId, Blank(input.Lyrics));

        var newUrls = CleanUrls(input.YouTubeUrls);
        foreach (var old in oldUrls.Where(o => !newUrls.Contains(o, StringComparer.OrdinalIgnoreCase)))
        {
            await _songs.RemoveYouTubeUrlAsync(songId, old);
        }

        foreach (var url in newUrls.Where(n => !oldUrls.Contains(n, StringComparer.OrdinalIgnoreCase)))
        {
            await _songs.AddYouTubeUrlAsync(songId, url);
        }

        return MusicResult<int>.Ok(songId);
    }

    public async Task<MusicResult<bool>> DeleteSongAsync(int songId, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanDelete(user))
        {
            return MusicResult<bool>.Fail(MusicResultStatus.Forbidden);
        }

        var (status, _) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<bool>.Fail(status);
        }

        await _songs.DeleteSongAsync(songId);
        return MusicResult<bool>.Ok(true);
    }

    // ---------- videos ----------

    public async Task<MusicResult<MusicVideoDto>> UploadVideoAsync(int songId, MusicVideoUpload upload, ClaimsPrincipal user)
    {
        var userId = MusicAuthorization.UserId(user);
        if (userId is null)
        {
            return MusicResult<MusicVideoDto>.Fail(MusicResultStatus.SignInRequired);
        }

        var (status, song) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<MusicVideoDto>.Fail(status);
        }

        var errors = new Dictionary<string, string[]>();
        var extension = Path.GetExtension(upload.FileName ?? string.Empty).ToLowerInvariant();
        var isVideo = upload.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) || VideoExtensions.Contains(extension);
        if (upload.Length <= 0 || !isVideo)
        {
            errors["file"] = new[] { "Escolha um ficheiro de vídeo." };
        }
        else if (upload.Length > MaxVideoBytes)
        {
            errors["file"] = new[] { "O vídeo não pode exceder 100 MB." };
        }

        var title = upload.Title?.Trim();
        if (title is { Length: > 200 })
        {
            errors["title"] = new[] { "O título não pode exceder 200 caracteres." };
        }

        if (errors.Count > 0)
        {
            return MusicResult<MusicVideoDto>.Invalid(errors);
        }

        if (string.IsNullOrEmpty(title))
        {
            await using var ctx = await _contexts.CreateDbContextAsync();
            title = $"{song!.Title} - Vídeo {await ctx.SongVideos.CountAsync(v => v.SongId == songId) + 1}";
        }

        // Uploads to storage and notifies the members who can see the album, as before.
        var video = await _songs.AddVideoAsync(songId, upload.Content, upload.FileName!, upload.ContentType, userId, title);
        return MusicResult<MusicVideoDto>.Ok(new MusicVideoDto(video.Id, title, video.Url, video.MimeType, true));
    }

    public async Task<MusicResult<bool>> DeleteVideoAsync(int videoId, ClaimsPrincipal user)
    {
        var (status, video, _) = await FindVideoAsync(videoId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<bool>.Fail(status);
        }

        var userId = MusicAuthorization.UserId(user)!;
        var admin = MusicAuthorization.IsAdmin(user);
        if (!admin && video!.CreatedByUserId != userId)
        {
            return MusicResult<bool>.Fail(MusicResultStatus.Forbidden);
        }

        await _songs.DeleteVideoAsync(videoId, userId, admin);
        return MusicResult<bool>.Ok(true);
    }

    // ---------- visibility ----------

    /// <summary>
    /// Signed out: albums that are neither private nor exclusive. Signed in: every album except
    /// exclusive ones the member is not listed on. Owner: everything. Access rows of an album that is
    /// not exclusive are ignored.
    /// </summary>
    private static IQueryable<Album> Visible(ApplicationDbContext ctx, ClaimsPrincipal user)
    {
        var albums = ctx.Albums.AsNoTracking();
        if (!MusicAuthorization.IsMember(user))
        {
            return albums.Where(a => !a.IsPrivate && !a.IsExclusive);
        }

        if (MusicAuthorization.IsOwner(user))
        {
            return albums;
        }

        var userId = MusicAuthorization.UserId(user);
        return albums.Where(a => !a.IsExclusive || a.AlbumAccesses.Any(x => x.UserId == userId));
    }

    /// <summary>Ok, NotFound, or SignInRequired when signing in could reveal it.</summary>
    private static async Task<MusicResultStatus> AlbumAccessAsync(ApplicationDbContext ctx, int albumId, ClaimsPrincipal user)
    {
        if (await Visible(ctx, user).AnyAsync(a => a.Id == albumId))
        {
            return MusicResultStatus.Ok;
        }

        var exists = await ctx.Albums.AnyAsync(a => a.Id == albumId);
        return exists && !MusicAuthorization.IsMember(user) ? MusicResultStatus.SignInRequired : MusicResultStatus.NotFound;
    }

    private sealed record SongRef(int Id, string Title, int? TrackNumber, bool HasMusic, int? Duration, int AlbumId, string AlbumTitle, string? Lyrics);

    private async Task<(MusicResultStatus, SongRef?)> FindSongAsync(int songId, ClaimsPrincipal user, bool includeLyrics = false)
    {
        await using var ctx = await _contexts.CreateDbContextAsync();
        var song = await ctx.Songs.AsNoTracking().Where(s => s.Id == songId)
            .Select(s => new SongRef(s.Id, s.Title, s.TrackNumber, s.HasMusic, s.Duration, s.AlbumId, s.Album!.Title,
                includeLyrics ? s.Lyrics : null))
            .FirstOrDefaultAsync();
        if (song is null)
        {
            return (MusicResultStatus.NotFound, null);
        }

        var access = await AlbumAccessAsync(ctx, song.AlbumId, user);
        return access == MusicResultStatus.Ok ? (access, song) : (access, null);
    }

    private async Task<(MusicResultStatus, SongVideo?, SongRef?)> FindVideoAsync(int videoId, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.IsMember(user))
        {
            return (MusicResultStatus.SignInRequired, null, null);
        }

        SongVideo? video;
        await using (var ctx = await _contexts.CreateDbContextAsync())
        {
            video = await ctx.SongVideos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == videoId);
        }

        if (video is null)
        {
            return (MusicResultStatus.NotFound, null, null);
        }

        var (status, song) = await FindSongAsync(video.SongId, user);
        return status == MusicResultStatus.Ok ? (status, video, song) : (status, null, null);
    }

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
