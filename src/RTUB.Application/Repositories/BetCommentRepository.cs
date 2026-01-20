using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for BetComment entity
/// </summary>
public class BetCommentRepository : Repository<BetComment>, IBetCommentRepository
{
    public BetCommentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<BetComment>> GetCommentsForBetAsync(int betId)
    {
        return await _dbSet
            .Include(c => c.Author)
            .Where(c => c.BetId == betId && c.DeletedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<BetComment?> GetByIdWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task DeleteByBetIdAsync(int betId)
    {
        // Use ExecuteDeleteAsync to bypass change tracker and avoid FK issues
        await _dbSet
            .Where(c => c.BetId == betId)
            .ExecuteDeleteAsync();
    }
}
