using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Activity entity
/// </summary>
public class ActivityRepository : Repository<Activity>, IActivityRepository
{
    public ActivityRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Activity?> GetWithReportAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<Activity>()
            .AsNoTracking()
            .Include(a => a.Report)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Activity?> GetWithTransactionsAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<Activity>()
            .AsNoTracking()
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == id);
    }
}
