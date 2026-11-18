// Music: album management (edit, create, update, delete).
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class MusicService
{
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
        // Only an Owner changes exclusivity; everyone else keeps the album's current setting.
        var exclusive = owner ? input.IsExclusive : existing.IsExclusive;

        if (cover is null || exclusive != existing.IsExclusive)
        {
            await _albums.UpdateAlbumAsync(albumId, title, input.Year, description, input.IsPrivate, exclusive);
        }

        if (cover is not null)
        {
            // Keeps the exclusivity just saved, replaces the previous cover in storage.
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
}
