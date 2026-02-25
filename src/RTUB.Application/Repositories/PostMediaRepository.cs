using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for PostMedia entity
/// </summary>
public class PostMediaRepository : Repository<PostMedia>, IPostMediaRepository
{
    public PostMediaRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<PostMedia>> GetByPostIdAsync(int postId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(pm => pm.PostId == postId)
            .OrderBy(pm => pm.SortOrder)
            .ThenBy(pm => pm.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteByPostIdAsync(int postId)
    {
        var mediaItems = await _dbSet
            .Where(pm => pm.PostId == postId)
            .ToListAsync();

        _dbSet.RemoveRange(mediaItems);
        await _context.SaveChangesAsync();
    }
}
