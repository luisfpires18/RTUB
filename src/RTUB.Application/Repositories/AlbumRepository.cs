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
    public AlbumRepository(ApplicationDbContext context) : base(context)
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
}
