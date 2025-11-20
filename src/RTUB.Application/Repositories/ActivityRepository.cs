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
    public ActivityRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Activity?> GetWithReportAsync(int id)
    {
        return await _dbSet
            .Include(a => a.Report)
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Activity?> GetWithTransactionsAsync(int id)
    {
        return await _dbSet
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == id);
    }
}
