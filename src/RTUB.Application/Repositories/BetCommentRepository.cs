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
    public BetCommentRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
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

    public async Task<Dictionary<int, int>> GetCountsByBetIdsAsync(IEnumerable<int> betIds)
    {
        var betIdsList = betIds.ToList();
        if (!betIdsList.Any())
        {
            return new Dictionary<int, int>();
        }

        var counts = await _dbSet
            .Where(c => betIdsList.Contains(c.BetId) && c.DeletedAt == null)
            .GroupBy(c => c.BetId)
            .Select(g => new { BetId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BetId, x => x.Count);

        // Ensure all bet IDs are in the dictionary (with count 0 if no comments)
        var result = new Dictionary<int, int>();
        foreach (var betId in betIdsList)
        {
            result[betId] = counts.GetValueOrDefault(betId, 0);
        }

        return result;
    }

    public async Task DeleteByBetIdAsync(int betId)
    {
        // Use ExecuteDeleteAsync to bypass change tracker and avoid FK issues
        await _dbSet
            .Where(c => c.BetId == betId)
            .ExecuteDeleteAsync();
    }
}
