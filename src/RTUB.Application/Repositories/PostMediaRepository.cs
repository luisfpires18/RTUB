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
        using var context = CreateContext();
        return await context.Set<PostMedia>()
            .AsNoTracking()
            .Where(pm => pm.PostId == postId)
            .OrderBy(pm => pm.SortOrder)
            .ThenBy(pm => pm.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteByPostIdAsync(int postId)
    {
        using var context = CreateContext();
        var mediaItems = await context.Set<PostMedia>()
            .Where(pm => pm.PostId == postId)
            .ToListAsync();

        context.Set<PostMedia>().RemoveRange(mediaItems);
        await context.SaveChangesAsync();
    }
}
