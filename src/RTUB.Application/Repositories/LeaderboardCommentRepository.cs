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
}
