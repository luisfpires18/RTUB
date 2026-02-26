using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for BossModeProgress entity.
/// </summary>
public class BossModeProgressRepository : Repository<BossModeProgress>, IBossModeProgressRepository
{
    public BossModeProgressRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory) { }

    public async Task<BossModeProgress?> GetByUserIdAsync(string userId)
    {
        using var context = CreateContext();
        return await context.BossModeProgresses
            .AsNoTracking()
            .Include(bp => bp.User)
            .FirstOrDefaultAsync(bp => bp.UserId == userId);
    }
}
