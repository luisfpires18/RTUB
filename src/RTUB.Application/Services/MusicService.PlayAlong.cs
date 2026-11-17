// Music: tocar junto (play-along charts, stars, notices).
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class MusicService
{
    // ---------- tocar junto (094) ----------

    public async Task<MusicResult<MusicLearnListDto>> GetPlayAlongSongsAsync(ClaimsPrincipal user)
    {
        if (!MusicAuthorization.IsMember(user))
        {
            return MusicResult<MusicLearnListDto>.Fail(MusicResultStatus.SignInRequired);
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        var userId = MusicAuthorization.UserId(user);
        var albums = Visible(ctx, user).Select(a => a.Id);
        var songs = await ctx.Songs.AsNoTracking()
            .Where(s => albums.Contains(s.AlbumId) && ctx.SongPlayAlongs.Any(p => p.SongId == s.Id))
            .Select(s => new
            {
                s.Id, s.Title, s.TrackNumber, s.AlbumId, AlbumTitle = s.Album!.Title, s.Album.Year,
                Views = ctx.SongPlayAlongs.Where(p => p.SongId == s.Id).Select(p => p.Views).FirstOrDefault(),
                Starred = ctx.SongPlayAlongFavorites.Any(f => f.SongId == s.Id && f.UserId == userId)
            })
            .ToListAsync();

        // Newest album first, then the album's own order.
        return MusicResult<MusicLearnListDto>.Ok(new MusicLearnListDto(songs
            .OrderByDescending(s => s.Year ?? int.MaxValue).ThenBy(s => s.AlbumId)
            .ThenBy(s => s.TrackNumber is null).ThenBy(s => s.TrackNumber).ThenBy(s => s.Id)
            .Select(s => new MusicLearnSongDto(s.Id, s.Title, s.AlbumId, s.AlbumTitle, s.Year, s.Views, s.Starred))
            .ToList(), MusicAuthorization.CanManagePlayAlong(user)));
    }

    /// <summary>Counts one open of the song's Aprender page (098).</summary>
    public async Task<MusicResult<bool>> RecordPlayAlongViewAsync(int songId, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.IsMember(user))
        {
            return MusicResult<bool>.Fail(MusicResultStatus.SignInRequired);
        }

        var (status, _) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<bool>.Fail(status);
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        var changed = await ctx.SongPlayAlongs.Where(p => p.SongId == songId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Views, p => p.Views + 1));
        return changed == 0 ? MusicResult<bool>.Fail(MusicResultStatus.NotFound) : MusicResult<bool>.Ok(true);
    }

    /// <summary>Stars or un-stars an Aprender song for the member (098).</summary>
    public async Task<MusicResult<bool>> SetPlayAlongStarAsync(int songId, bool starred, ClaimsPrincipal user)
    {
        var userId = MusicAuthorization.UserId(user);
        if (userId is null)
        {
            return MusicResult<bool>.Fail(MusicResultStatus.SignInRequired);
        }

        var (status, _) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<bool>.Fail(status);
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        var row = await ctx.SongPlayAlongFavorites.FirstOrDefaultAsync(f => f.SongId == songId && f.UserId == userId);
        if (starred && row is null)
        {
            if (!await ctx.SongPlayAlongs.AnyAsync(p => p.SongId == songId))
            {
                return MusicResult<bool>.Fail(MusicResultStatus.NotFound);
            }

            ctx.SongPlayAlongFavorites.Add(new SongPlayAlongFavorite { SongId = songId, UserId = userId });
            await ctx.SaveChangesAsync();
        }
        else if (!starred && row is not null)
        {
            ctx.SongPlayAlongFavorites.Remove(row);
            await ctx.SaveChangesAsync();
        }

        return MusicResult<bool>.Ok(starred);
    }

    public async Task<MusicResult<bool>> NotifyPlayAlongAsync(int songId, MusicLearnNoticeInput? input, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanManagePlayAlong(user))
        {
            return MusicResult<bool>.Fail(MusicResultStatus.Forbidden);
        }

        var (status, song) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<bool>.Fail(status);
        }

        var message = input?.Message?.Trim();
        if (message?.Length > MaxLearnNoticeLength)
        {
            return MusicResult<bool>.Invalid(new Dictionary<string, string[]> { ["message"] = new[] { $"Máximo de {MaxLearnNoticeLength} caracteres." } });
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        if (!await ctx.SongPlayAlongs.AnyAsync(p => p.SongId == songId))
        {
            return MusicResult<bool>.Fail(MusicResultStatus.NotFound);
        }

        // As the "Novo vídeo" push: everyone, or only the members on the list of an exclusive album.
        var notification = _pushFactory.CreateSongLearnNotification(song!.Id, song.Title, message);
        if (await ctx.Albums.AnyAsync(a => a.Id == song.AlbumId && a.IsExclusive))
        {
            var listed = await ctx.AlbumAccesses.AsNoTracking().Where(x => x.AlbumId == song.AlbumId).Select(x => x.UserId).ToListAsync();
            await _push.SendToSelectedUsersAsync(listed, notification);
        }
        else
        {
            await _push.BroadcastAsync(notification);
        }

        return MusicResult<bool>.Ok(true);
    }

    public const int MaxLearnNoticeLength = 300;

    public async Task<MusicResult<MusicPlayAlongDto>> GetPlayAlongAsync(int songId, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.IsMember(user))
        {
            return MusicResult<MusicPlayAlongDto>.Fail(MusicResultStatus.SignInRequired);
        }

        var (status, song) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<MusicPlayAlongDto>.Fail(status);
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        var json = await ctx.SongPlayAlongs.AsNoTracking().Where(p => p.SongId == songId).Select(p => p.Chart).FirstOrDefaultAsync();
        var chart = json is null ? null : PlayAlongChart.Deserialize(json);
        return chart is null
            ? MusicResult<MusicPlayAlongDto>.Fail(MusicResultStatus.NotFound)
            : MusicResult<MusicPlayAlongDto>.Ok(new MusicPlayAlongDto(song!.Id, song.Title, song.AlbumId, song.AlbumTitle, song.HasMusic, chart));
    }

    public async Task<MusicResult<bool>> SavePlayAlongAsync(int songId, PlayAlongChartDto? chart, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanManagePlayAlong(user))
        {
            return MusicResult<bool>.Fail(MusicResultStatus.Forbidden);
        }

        var (status, _) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<bool>.Fail(status);
        }

        if (PlayAlongChart.Validate(chart) is { } problem)
        {
            return MusicResult<bool>.Invalid(new Dictionary<string, string[]> { ["chart"] = new[] { problem } });
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        var row = await ctx.SongPlayAlongs.FirstOrDefaultAsync(p => p.SongId == songId);
        if (row is null)
        {
            row = new SongPlayAlong { SongId = songId };
            ctx.SongPlayAlongs.Add(row);
        }

        row.Chart = PlayAlongChart.Serialize(chart!);
        await ctx.SaveChangesAsync();
        return MusicResult<bool>.Ok(true);
    }

    public async Task<MusicResult<bool>> DeletePlayAlongAsync(int songId, ClaimsPrincipal user)
    {
        if (!MusicAuthorization.CanManagePlayAlong(user))
        {
            return MusicResult<bool>.Fail(MusicResultStatus.Forbidden);
        }

        var (status, _) = await FindSongAsync(songId, user);
        if (status != MusicResultStatus.Ok)
        {
            return MusicResult<bool>.Fail(status);
        }

        await using var ctx = await _contexts.CreateDbContextAsync();
        var row = await ctx.SongPlayAlongs.FirstOrDefaultAsync(p => p.SongId == songId);
        if (row is not null)
        {
            ctx.SongPlayAlongs.Remove(row);
            await ctx.SaveChangesAsync();
        }

        return MusicResult<bool>.Ok(true);
    }
}
