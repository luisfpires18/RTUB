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

    public async Task<Report?> GetByIdWithActivitiesAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<Report>()
            .Include(r => r.Activities)
                .ThenInclude(a => a.Transactions)
            .FirstOrDefaultAsync(r => r.Id == id);
    }
}
