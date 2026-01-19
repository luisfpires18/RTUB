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
        var comments = await _dbSet
            .Where(c => c.BetId == betId)
            .ToListAsync();

        if (comments.Count > 0)
        {
            _dbSet.RemoveRange(comments);
            await _context.SaveChangesAsync();
        }
    }
}
