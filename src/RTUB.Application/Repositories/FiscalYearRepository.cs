using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for FiscalYear entity
/// </summary>
public class FiscalYearRepository : Repository<FiscalYear>, IFiscalYearRepository
{
    public FiscalYearRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<FiscalYear?> GetByStartYearAsync(int startYear)
    {
        using var context = CreateContext();
        return await context.Set<FiscalYear>()
            .AsNoTracking()
            .FirstOrDefaultAsync(fy => fy.StartYear == startYear);
    }

    public async Task<IEnumerable<FiscalYear>> GetAllOrderedAsync()
    {
        using var context = CreateContext();
        return await context.Set<FiscalYear>()
            .AsNoTracking()
            .OrderByDescending(fy => fy.StartYear)
            .ToListAsync();
    }
}
