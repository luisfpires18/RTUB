using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Instrument entity
/// </summary>
public class InstrumentRepository : Repository<Instrument>, IInstrumentRepository
{
    public InstrumentRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Instrument>> GetAllOrderedAsync()
    {
        using var context = CreateContext();
        return await context.Set<Instrument>()
            .AsNoTracking()
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByCategoryAsync(string category)
    {
        using var context = CreateContext();
        return await context.Set<Instrument>()
            .AsNoTracking()
            .Where(i => i.Category == category)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByConditionAsync(InstrumentCondition condition)
    {
        using var context = CreateContext();
        return await context.Set<Instrument>()
            .AsNoTracking()
            .Where(i => i.Condition == condition)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByLocationAsync(string location)
    {
        using var context = CreateContext();
        return await context.Set<Instrument>()
            .AsNoTracking()
            .Where(i => i.Location == location)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }
}
