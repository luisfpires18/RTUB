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
    public PostRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<Post?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(p => p.Author)
            .Include(p => p.Media)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IEnumerable<Post>> GetByDiscussionIdAsync(int discussionId, int page, int pageSize, string? searchTerm)
    {
        var query = _dbSet
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
        var query = _dbSet
            .Where(p => p.DiscussionId == discussionId && !p.IsDeleted)
            .WhereIf(!string.IsNullOrWhiteSpace(searchTerm),
                p => p.Title.Contains(searchTerm!, StringComparison.OrdinalIgnoreCase) ||
                     p.Body.Contains(searchTerm!, StringComparison.OrdinalIgnoreCase));

        return await query.CountAsync();
    }
}
