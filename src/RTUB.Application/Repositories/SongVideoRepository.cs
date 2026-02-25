using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for SongVideo entity
/// </summary>
public class SongVideoRepository : Repository<SongVideo>, ISongVideoRepository
{
    public SongVideoRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<SongVideo>> GetBySongIdAsync(int songId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(sv => sv.CreatedByUser)
            .Where(sv => sv.SongId == songId)
            .OrderBy(sv => sv.SortOrder)
            .ThenBy(sv => sv.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetCountBySongIdAsync(int songId)
    {
        return await _dbSet
            .Where(sv => sv.SongId == songId)
            .CountAsync();
    }
}
