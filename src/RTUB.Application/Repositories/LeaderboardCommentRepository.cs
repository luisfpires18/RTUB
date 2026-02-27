using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for LeaderboardComment entity
/// </summary>
public class LeaderboardCommentRepository : Repository<LeaderboardComment>, ILeaderboardCommentRepository
{
    public LeaderboardCommentRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<LeaderboardComment>> GetCommentsForUserAsync(string targetUserId)
    {
        return await _dbSet
            .Include(c => c.Author)
            .Include(c => c.Likes)
                .ThenInclude(l => l.User)
            .Where(c => c.TargetUserId == targetUserId && c.DeletedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<LeaderboardComment?> GetByIdWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(c => c.Author)
            .Include(c => c.Likes)
                .ThenInclude(l => l.User)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>
    /// Toggles a like within a single tracked DbContext so navigation property
    /// changes (Likes collection) are properly persisted by SaveChangesAsync.
    /// The base UpdateAsync uses SetValues which only copies scalar properties.
    /// </summary>
    public async Task<bool?> ToggleLikeAsync(int commentId, string userId)
    {
        using var context = CreateContext();
        var comment = await context.Set<LeaderboardComment>()
            .Include(c => c.Likes)
            .FirstOrDefaultAsync(c => c.Id == commentId);

        if (comment == null)
            return null;

        var existingLike = comment.Likes.FirstOrDefault(l => l.UserId == userId);

        if (existingLike != null)
        {
            context.Set<LeaderboardCommentLike>().Remove(existingLike);
            await context.SaveChangesAsync();
            return false; // Unliked
        }
        else
        {
            var like = LeaderboardCommentLike.Create(commentId, userId);
            context.Set<LeaderboardCommentLike>().Add(like);
            await context.SaveChangesAsync();
            return true; // Liked
        }
    }
}
