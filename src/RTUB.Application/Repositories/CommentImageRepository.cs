using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for CommentImage entity
/// </summary>
public class CommentImageRepository : Repository<CommentImage>, ICommentImageRepository
{
    public CommentImageRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<CommentImage>> GetByCommentIdAsync(int commentId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(ci => ci.CommentId == commentId)
            .OrderBy(ci => ci.SortOrder)
            .ThenBy(ci => ci.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteByCommentIdAsync(int commentId)
    {
        var images = await _dbSet
            .Where(ci => ci.CommentId == commentId)
            .ToListAsync();

        _dbSet.RemoveRange(images);
        await _context.SaveChangesAsync();
    }
}
