using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for BetOption entity
/// Provides bet option-specific data access operations
/// </summary>
public class BetOptionRepository : Repository<BetOption>, IBetOptionRepository
{
    public BetOptionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<BetOption>> GetByBetIdAsync(int betId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(o => o.BetId == betId)
            .Include(o => o.MemberA)
            .Include(o => o.MemberB)
            .ToListAsync();
    }

    public async Task<BetOption?> GetWithMembersAsync(int id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(o => o.MemberA)
            .Include(o => o.MemberB)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task DeleteByBetIdAsync(int betId)
    {
        // Use ExecuteDeleteAsync to bypass change tracker and avoid FK issues
        await _dbSet
            .Where(o => o.BetId == betId)
            .ExecuteDeleteAsync();
    }
}
