using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Song entity
/// Provides song-specific data access operations
/// </summary>
public class SongRepository : Repository<Song>, ISongRepository
{
    public SongRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Song?> GetSongByIdWithUrlsAsync(int id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(s => s.YouTubeUrls)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Song>> GetAllSongsWithAlbumAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(s => s.Album)
            .Include(s => s.YouTubeUrls)
            .ToListAsync();
    }

    public async Task<IEnumerable<Song>> GetSongsByAlbumIdAsync(int albumId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(s => s.YouTubeUrls)
            .Where(s => s.AlbumId == albumId)
            .OrderBy(s => s.TrackNumber)
            .ToListAsync();
    }

    public async Task<Song?> GetSongForUpdateAsync(int id)
    {
        return await _dbSet
            .Include(s => s.YouTubeUrls)
            .FirstOrDefaultAsync(s => s.Id == id);
    }
}
