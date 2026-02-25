using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Report entity
/// </summary>
public class ReportRepository : Repository<Report>, IReportRepository
{
    public ReportRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Report>> GetPublishedWithActivitiesAsync()
    {
        return await _dbSet
            .Include(r => r.Activities)
                .ThenInclude(a => a.Transactions)
            .Where(r => r.IsPublished)
            .OrderByDescending(r => r.Year)
            .ToListAsync();
    }

    public async Task<Report?> GetByIdWithActivitiesAsync(int id)
    {
        return await _dbSet
            .Include(r => r.Activities)
                .ThenInclude(a => a.Transactions)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Report>> GetAllWithActivitiesAsync()
    {
        return await _dbSet
            .Include(r => r.Activities)
                .ThenInclude(a => a.Transactions)
            .ToListAsync();
    }
}
