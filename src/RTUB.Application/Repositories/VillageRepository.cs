using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Village entity with eager loading of all children.
/// </summary>
public class VillageRepository : Repository<Village>, IVillageRepository
{
    public VillageRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory) { }

    public async Task<Village?> GetByUserIdAsync(string userId)
    {
        using var context = CreateContext();
        return await context.Villages
            .AsNoTracking()
            .Include(v => v.Fields)
            .Include(v => v.Buildings)
            .Include(v => v.Troops)
            .Include(v => v.Missions)
            .Include(v => v.ActiveBuildJob)
            .FirstOrDefaultAsync(v => v.UserId == userId);
    }

    public async Task<Village?> GetByUserIdTrackedAsync(string userId)
    {
        using var context = CreateContext();
        return await context.Villages
            .Include(v => v.Fields)
            .Include(v => v.Buildings)
            .Include(v => v.Troops)
            .Include(v => v.Missions)
            .Include(v => v.ActiveBuildJob)
            .FirstOrDefaultAsync(v => v.UserId == userId);
    }
}
