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
        using var context = CreateContext();
        // Using local server time for consistency with Events
        var now = DateTime.Now;
        return await context.Set<Bet>()
            .AsNoTracking()
            .Where(b => b.DateTime > now && !b.IsCancelled)
            .OrderBy(b => b.DateTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Bet>> GetPastBetsAsync()
    {
        using var context = CreateContext();
        // Using local server time for consistency with Events
        var now = DateTime.Now;
        return await context.Set<Bet>()
            .AsNoTracking()
            .Where(b => b.DateTime <= now)
            .OrderByDescending(b => b.DateTime)
            .ToListAsync();
    }

    public async Task<Bet?> GetBetWithOptionsAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<Bet>()
            .AsNoTracking()
            .Include(b => b.Options)
                .ThenInclude(o => o.MemberA)
            .Include(b => b.Options)
                .ThenInclude(o => o.MemberB)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<Bet?> GetBetWithDetailsAsync(int id)
    {
        using var context = CreateContext();
        // Not using AsNoTracking() because the Bet entity will be modified in ResolveBetAsync
        return await context.Set<Bet>()
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
        using var context = CreateContext();
        await context.Set<Bet>()
            .Where(b => b.Id == id)
            .ExecuteDeleteAsync();
    }
}
