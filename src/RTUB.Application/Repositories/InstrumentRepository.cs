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
    public InstrumentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Instrument>> GetAllOrderedAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByCategoryAsync(string category)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(i => i.Category == category)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByConditionAsync(InstrumentCondition condition)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(i => i.Condition == condition)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    public async Task<IEnumerable<Instrument>> GetByLocationAsync(string location)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(i => i.Location == location)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }
}
