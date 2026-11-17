// Music: song management (edit, create, update, delete).
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;

namespace RTUB.Application.Services;

public sealed partial class MusicService
{
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
            song.YouTubeUrls.OrderBy(y => y.Id).Select(y => y.Url).ToList(), song.Lyrics,
            await ctx.SongPlayAlongs.AnyAsync(p => p.SongId == songId)));
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
}
