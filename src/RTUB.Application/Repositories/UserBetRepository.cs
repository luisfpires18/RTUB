using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for UserBet entity
/// Provides user bet-specific data access operations
/// </summary>
public class UserBetRepository : Repository<UserBet>, IUserBetRepository
{
    public UserBetRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<UserBet>> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(ub => ub.UserId == userId)
            .Include(ub => ub.Bet)
            .Include(ub => ub.BetOption)
            .OrderByDescending(ub => ub.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserBet>> GetByBetIdAsync(int betId)
    {
        // Not using AsNoTracking() because these entities will be modified in BetService
        return await _dbSet
            .Where(ub => ub.BetId == betId)
            .Include(ub => ub.User)
            .Include(ub => ub.BetOption)
            .ToListAsync();
    }

    public async Task<UserBet?> GetUserBetForBetAsync(string userId, int betId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ub => ub.BetOption)
            .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BetId == betId);
    }

    public async Task<IEnumerable<UserBet>> GetByBetOptionIdAsync(int betOptionId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(ub => ub.BetOptionId == betOptionId)
            .Include(ub => ub.User)
            .ToListAsync();
    }

    public async Task DeleteByBetIdAsync(int betId)
    {
        // Use ExecuteDeleteAsync to bypass change tracker and avoid FK issues
        await _dbSet
            .Where(ub => ub.BetId == betId)
            .ExecuteDeleteAsync();
    }
}
