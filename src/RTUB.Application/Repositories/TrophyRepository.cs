using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Trophy entity
/// </summary>
public class TrophyRepository : Repository<Trophy>, ITrophyRepository
{
    public TrophyRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Trophy>> GetByEventIdAsync(int eventId)
    {
        using var context = CreateContext();
        return await context.Set<Trophy>()
            .AsNoTracking()
            .Where(t => t.EventId == eventId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Trophy>> GetAllWithEventAsync()
    {
        using var context = CreateContext();
        return await context.Set<Trophy>()
            .AsNoTracking()
            .Include(t => t.Event)
            .OrderByDescending(t => t.Event!.Date)
            .ToListAsync();
    }
}
