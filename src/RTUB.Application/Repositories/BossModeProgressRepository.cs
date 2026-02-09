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
    public BossModeProgressRepository(ApplicationDbContext context) : base(context) { }

    public async Task<BossModeProgress?> GetByUserIdAsync(string userId)
    {
        return await _context.BossModeProgresses
            .Include(bp => bp.User)
            .FirstOrDefaultAsync(bp => bp.UserId == userId);
    }
}
