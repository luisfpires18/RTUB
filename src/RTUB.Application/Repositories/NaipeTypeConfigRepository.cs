using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for NaipeTypeConfig entity
/// </summary>
public class NaipeTypeConfigRepository : Repository<NaipeTypeConfig>, INaipeTypeConfigRepository
{
    public NaipeTypeConfigRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<List<NaipeTypeConfig>> GetAllOrderedAsync()
    {
        using var context = CreateContext();
        return await context.Set<NaipeTypeConfig>()
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.InstrumentType)
            .ToListAsync();
    }

    public async Task<List<NaipeTypeConfig>> GetVisibleOrderedAsync()
    {
        using var context = CreateContext();
        return await context.Set<NaipeTypeConfig>()
            .AsNoTracking()
            .Where(c => c.IsVisible)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.InstrumentType)
            .ToListAsync();
    }

    public async Task<NaipeTypeConfig?> GetByInstrumentTypeAsync(InstrumentType type)
    {
        using var context = CreateContext();
        return await context.Set<NaipeTypeConfig>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.InstrumentType == type);
    }
}
