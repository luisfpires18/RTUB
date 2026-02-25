using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository for Survive Mode progress data access.
/// </summary>
public class SurviveModeProgressRepository : Repository<SurviveModeProgress>, ISurviveModeProgressRepository
{
    public SurviveModeProgressRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory) { }

    public async Task<SurviveModeProgress?> GetByUserIdAsync(string userId)
    {
        return await _context.SurviveModeProgresses
            .Include(sp => sp.User)
            .FirstOrDefaultAsync(sp => sp.UserId == userId);
    }
}
