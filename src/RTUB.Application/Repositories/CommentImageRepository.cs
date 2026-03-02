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
        using var context = CreateContext();
        return await context.Set<CommentImage>()
            .AsNoTracking()
            .Where(ci => ci.CommentId == commentId)
            .OrderBy(ci => ci.SortOrder)
            .ThenBy(ci => ci.CreatedAt)
            .ToListAsync();
    }

    public async Task DeleteByCommentIdAsync(int commentId)
    {
        using var context = CreateContext();
        var images = await context.Set<CommentImage>()
            .Where(ci => ci.CommentId == commentId)
            .ToListAsync();

        context.Set<CommentImage>().RemoveRange(images);
        await context.SaveChangesAsync();
    }
}
