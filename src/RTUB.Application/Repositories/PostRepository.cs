using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Post entity
/// </summary>
public class PostRepository : Repository<Post>, IPostRepository
{
    public PostRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public override async Task<Post?> GetByIdAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<Post>()
            .Include(p => p.Author)
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Post>> GetByDiscussionIdAsync(int discussionId, int page, int pageSize, string? searchTerm)
    {
        using var context = CreateContext();
        var query = context.Set<Post>()
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Comments)
            .Include(p => p.Media)
            .Where(p => p.DiscussionId == discussionId && !p.IsDeleted)
            .WhereIf(!string.IsNullOrWhiteSpace(searchTerm),
                p => p.Title.Contains(searchTerm!, StringComparison.OrdinalIgnoreCase) ||
                     p.Body.Contains(searchTerm!, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(p => p.IsPinned)
            .ThenByDescending(p => p.LastActivityAt);

        return await query.PaginateAsync(page, pageSize);
    }

    public async Task<int> GetCountByDiscussionIdAsync(int discussionId, string? searchTerm)
    {
        using var context = CreateContext();
        var query = context.Set<Post>()
            .AsNoTracking()
            .Where(p => p.DiscussionId == discussionId && !p.IsDeleted)
            .WhereIf(!string.IsNullOrWhiteSpace(searchTerm),
                p => p.Title.Contains(searchTerm!, StringComparison.OrdinalIgnoreCase) ||
                     p.Body.Contains(searchTerm!, StringComparison.OrdinalIgnoreCase));

        return await query.CountAsync();
    }
}
