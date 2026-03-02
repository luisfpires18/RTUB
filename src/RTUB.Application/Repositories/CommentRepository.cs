using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Comment entity
/// </summary>
public class CommentRepository : Repository<Comment>, ICommentRepository
{
    public CommentRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Comment>> GetByPostIdAsync(int postId, int page, int pageSize)
    {
        using var context = CreateContext();
        return await context.Set<Comment>()
            .AsNoTracking()
            .Include(c => c.Author)
            .Include(c => c.Images)
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .PaginateAsync(page, pageSize);
    }

    public async Task<int> GetCountByPostIdAsync(int postId)
    {
        using var context = CreateContext();
        return await context.Set<Comment>()
            .AsNoTracking()
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .CountAsync();
    }

    public async Task<Dictionary<int, int>> GetCountsByPostIdsAsync(IEnumerable<int> postIds)
    {
        var postIdsList = postIds.ToList();
        if (!postIdsList.Any())
        {
            return new Dictionary<int, int>();
        }

        using var context = CreateContext();
        var counts = await context.Set<Comment>()
            .AsNoTracking()
            .Where(c => postIdsList.Contains(c.PostId) && !c.IsDeleted)
            .GroupBy(c => c.PostId)
            .Select(g => new { PostId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PostId, x => x.Count);

        // Ensure all post IDs are in the dictionary (with count 0 if no comments)
        var result = new Dictionary<int, int>();
        foreach (var postId in postIdsList)
        {
            result[postId] = counts.GetValueOrDefault(postId, 0);
        }

        return result;
    }
}
