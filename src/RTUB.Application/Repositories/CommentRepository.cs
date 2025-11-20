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
    public CommentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Comment>> GetByPostIdAsync(int postId, int page, int pageSize)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(c => c.Author)
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .PaginateAsync(page, pageSize);
    }

    public async Task<int> GetCountByPostIdAsync(int postId)
    {
        return await _dbSet
            .Where(c => c.PostId == postId && !c.IsDeleted)
            .CountAsync();
    }
}
