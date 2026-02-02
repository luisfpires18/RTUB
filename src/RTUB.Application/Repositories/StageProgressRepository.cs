using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for StageProgress entity
/// </summary>
public class StageProgressRepository : Repository<StageProgress>, IStageProgressRepository
{
    public StageProgressRepository(ApplicationDbContext context) : base(context) { }

    public async Task<StageProgress?> GetByUserIdAsync(string userId)
    {
        return await _context.StageProgresses
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.UserId == userId);
    }
}
