using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Bet entity
/// Provides bet-specific data access operations
/// </summary>
public class BetRepository : Repository<Bet>, IBetRepository
{
    public BetRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Bet>> GetFutureBetsAsync()
    {
        // Using local server time for consistency with Events
        var now = DateTime.Now;
        return await _dbSet
            .AsNoTracking()
            .Where(b => b.DateTime > now && !b.IsCancelled)
            .OrderBy(b => b.DateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Bet>> GetPastBetsAsync()
    {
        // Using local server time for consistency with Events
        var now = DateTime.Now;
        return await _dbSet
            .AsNoTracking()
            .Where(b => b.DateTime <= now)
            .OrderByDescending(b => b.DateTime)
            .ToListAsync();
    }

    public async Task<Bet?> GetBetWithOptionsAsync(int id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(b => b.Options)
                .ThenInclude(o => o.MemberA)
            .Include(b => b.Options)
                .ThenInclude(o => o.MemberB)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Bet?> GetBetWithDetailsAsync(int id)
    {
        // Not using AsNoTracking() because the Bet entity will be modified in ResolveBetAsync
        return await _dbSet
            .Include(b => b.Options)
                .ThenInclude(o => o.MemberA)
            .Include(b => b.Options)
                .ThenInclude(o => o.MemberB)
            .Include(b => b.UserBets)
                .ThenInclude(ub => ub.User)
            .Include(b => b.UserBets)
                .ThenInclude(ub => ub.BetOption)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task DeleteByIdDirectAsync(int id)
    {
        // Use ExecuteDeleteAsync to bypass change tracker and delete directly
        // EF Core handles the SQL generation properly
        await _dbSet
            .Where(b => b.Id == id)
            .ExecuteDeleteAsync();
    }
}
