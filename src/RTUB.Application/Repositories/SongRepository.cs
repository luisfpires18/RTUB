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
    public SongRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Song?> GetSongByIdWithUrlsAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<Song>()
            .AsNoTracking()
            .Include(s => s.YouTubeUrls)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task<IEnumerable<Song>> GetAllSongsWithAlbumAsync()
    {
        using var context = CreateContext();
        return await context.Set<Song>()
            .AsNoTracking()
            .Include(s => s.Album)
            .Include(s => s.YouTubeUrls)
            .ToListAsync();
    }

    public async Task<IEnumerable<Song>> GetSongsByAlbumIdAsync(int albumId)
    {
        using var context = CreateContext();
        return await context.Set<Song>()
            .AsNoTracking()
            .Include(s => s.YouTubeUrls)
            .Where(s => s.AlbumId == albumId)
            .OrderBy(s => s.TrackNumber)
            .ToListAsync();
    }

    public async Task<Song?> GetSongForUpdateAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<Song>()
            .Include(s => s.YouTubeUrls)
            .FirstOrDefaultAsync(s => s.Id == id);
    }

    public async Task AddYouTubeUrlAsync(SongYouTubeUrl youtubeUrl)
    {
        using var context = CreateContext();
        await context.Set<SongYouTubeUrl>().AddAsync(youtubeUrl);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Overrides base delete to include YouTubeUrls so InMemory cascade delete works.
    /// </summary>
    public override async Task DeleteAsync(Song entity)
    {
        using var context = CreateContext();
        var tracked = await context.Set<Song>()
            .Include(s => s.YouTubeUrls)
            .FirstOrDefaultAsync(s => s.Id == entity.Id);
        if (tracked != null)
        {
            context.Set<Song>().Remove(tracked);
            await context.SaveChangesAsync();
        }
    }

    public async Task DeleteYouTubeUrlAsync(SongYouTubeUrl youtubeUrl)
    {
        using var context = CreateContext();
        context.Set<SongYouTubeUrl>().Attach(youtubeUrl);
        context.Set<SongYouTubeUrl>().Remove(youtubeUrl);
        await context.SaveChangesAsync();
    }
}
