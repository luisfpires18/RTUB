// Music: reads (albums, lyrics, audio, play, videos, statistics).
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class MusicService
{
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
                PlayAlong = ctx.SongPlayAlongs.Any(p => p.SongId == s.Id),
            })
            .ToListAsync();

        var member = MusicAuthorization.IsMember(user);
        var dto = songs
            .OrderBy(s => s.TrackNumber is null).ThenBy(s => s.TrackNumber).ThenBy(s => s.Id)
            .Select(s => new MusicSongDto(s.Id, s.Title, s.TrackNumber, Blank(s.LyricAuthor), Blank(s.MusicAuthor),
                Blank(s.Adaptation), s.HasMusic, s.Plays, Links(s.SpotifyUrl, s.YouTube), member ? s.Videos : null,
                member && s.PlayAlong))
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
}
