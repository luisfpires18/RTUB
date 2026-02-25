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
        return await _dbSet
            .AsNoTracking()
            .Where(a => !a.IsPrivate)
            .OrderByDescending(a => a.Year)
            .ToListAsync();
    }

    public async Task<IEnumerable<Album>> GetAlbumsWithSongsAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(a => a.Songs)
            .OrderByDescending(a => a.Year)
            .ToListAsync();
    }

    public async Task<Album?> GetAlbumWithSongsAsync(int id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(a => a.Songs.OrderBy(s => s.TrackNumber))
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<IEnumerable<Album>> GetAlbumsForUserAsync(string userId, bool isOwner = false)
    {
        // Owners can see all albums
        if (isOwner)
        {
            return await _dbSet
                .AsNoTracking()
                .OrderByDescending(a => a.Year)
                .ToListAsync();
        }

        // Regular users can see non-exclusive albums + exclusive albums where they are in the access list
        return await _dbSet
            .AsNoTracking()
            .Where(a => !a.IsExclusive || a.AlbumAccesses.Any(aa => aa.UserId == userId))
            .OrderByDescending(a => a.Year)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetAuthorizedUserIdsAsync(int albumId)
    {
        return await _context.AlbumAccesses
            .AsNoTracking()
            .Where(aa => aa.AlbumId == albumId)
            .Select(aa => aa.UserId)
            .ToListAsync();
    }

    public async Task AddAlbumAccessAsync(int albumId, string userId, bool saveChanges = true)
    {
        var existingAccess = await _context.AlbumAccesses
            .FirstOrDefaultAsync(aa => aa.AlbumId == albumId && aa.UserId == userId);

        if (existingAccess == null)
        {
            var albumAccess = AlbumAccess.Create(albumId, userId);
            await _context.AlbumAccesses.AddAsync(albumAccess);

            if (saveChanges)
            {
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task RemoveAlbumAccessAsync(int albumId, string userId, bool saveChanges = true)
    {
        var albumAccess = await _context.AlbumAccesses
            .FirstOrDefaultAsync(aa => aa.AlbumId == albumId && aa.UserId == userId);

        if (albumAccess != null)
        {
            _context.AlbumAccesses.Remove(albumAccess);

            if (saveChanges)
            {
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task RemoveAllAlbumAccessAsync(int albumId, bool saveChanges = true)
    {
        var accessEntries = await _context.AlbumAccesses
            .Where(aa => aa.AlbumId == albumId)
            .ToListAsync();

        if (accessEntries.Count > 0)
        {
            _context.AlbumAccesses.RemoveRange(accessEntries);

            if (saveChanges)
            {
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task<bool> HasAccessAsync(int albumId, string userId)
    {
        var album = await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == albumId);

        if (album == null)
            return false;

        // If album is not exclusive, everyone has access
        if (!album.IsExclusive)
            return true;

        // Check if user is in access list
        return await _context.AlbumAccesses
            .AsNoTracking()
            .AnyAsync(aa => aa.AlbumId == albumId && aa.UserId == userId);
    }
}
