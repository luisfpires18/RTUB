// Music: videos and album/song visibility checks.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class MusicService
{
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
}
