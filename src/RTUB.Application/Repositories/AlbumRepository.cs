using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Album entity
/// </summary>
public class AlbumRepository : Repository<Album>, IAlbumRepository
{
    public AlbumRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Album>> GetPublicAlbumsAsync()
    {
        using var context = CreateContext();
        return await context.Set<Album>()
            .AsNoTracking()
            .Where(a => !a.IsPrivate)
            .OrderByDescending(a => a.Year)
            .ToListAsync();
    }

    public async Task<IEnumerable<Album>> GetAlbumsWithSongsAsync()
    {
        using var context = CreateContext();
        return await context.Set<Album>()
            .AsNoTracking()
            .Include(a => a.Songs)
            .OrderByDescending(a => a.Year)
            .ToListAsync();
    }

    public async Task<Album?> GetAlbumWithSongsAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<Album>()
            .AsNoTracking()
            .Include(a => a.Songs.OrderBy(s => s.TrackNumber))
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<Album>> GetAlbumsForUserAsync(string userId, bool isOwner = false)
    {
        using var context = CreateContext();
        // Owners can see all albums
        if (isOwner)
        {
            return await context.Set<Album>()
                .AsNoTracking()
                .OrderByDescending(a => a.Year)
                .ToListAsync();
        }

        // Regular users can see non-exclusive albums + exclusive albums where they are in the access list
        return await context.Set<Album>()
            .AsNoTracking()
            .Where(a => !a.IsExclusive || a.AlbumAccesses.Any(aa => aa.UserId == userId))
            .OrderByDescending(a => a.Year)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetAuthorizedUserIdsAsync(int albumId)
    {
        using var context = CreateContext();
        return await context.AlbumAccesses
            .AsNoTracking()
            .Where(aa => aa.AlbumId == albumId)
            .Select(aa => aa.UserId)
            .ToListAsync();
    }

    public async Task AddAlbumAccessAsync(int albumId, string userId, bool saveChanges = true)
    {
        using var context = CreateContext();
        var existingAccess = await context.AlbumAccesses
            .AsNoTracking()
            .AnyAsync(aa => aa.AlbumId == albumId && aa.UserId == userId);

        if (!existingAccess)
        {
            var albumAccess = AlbumAccess.Create(albumId, userId);
            await context.AlbumAccesses.AddAsync(albumAccess);
            await context.SaveChangesAsync();
        }
    }

    public async Task RemoveAlbumAccessAsync(int albumId, string userId, bool saveChanges = true)
    {
        using var context = CreateContext();
        var albumAccess = await context.AlbumAccesses
            .FirstOrDefaultAsync(aa => aa.AlbumId == albumId && aa.UserId == userId);

        if (albumAccess != null)
        {
            context.AlbumAccesses.Remove(albumAccess);
            await context.SaveChangesAsync();
        }
    }

    public async Task RemoveAllAlbumAccessAsync(int albumId, bool saveChanges = true)
    {
        using var context = CreateContext();
        var accessEntries = await context.AlbumAccesses
            .Where(aa => aa.AlbumId == albumId)
            .ToListAsync();

        if (accessEntries.Count > 0)
        {
            context.AlbumAccesses.RemoveRange(accessEntries);
            await context.SaveChangesAsync();
        }
    }

    public async Task<bool> HasAccessAsync(int albumId, string userId)
    {
        using var context = CreateContext();
        var album = await context.Set<Album>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == albumId);

        if (album == null)
            return false;

        // If album is not exclusive, everyone has access
        if (!album.IsExclusive)
            return true;

        // Check if user is in access list
        return await context.AlbumAccesses
            .AsNoTracking()
            .AnyAsync(aa => aa.AlbumId == albumId && aa.UserId == userId);
    }
}
