using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Event entity
/// Provides event-specific data access operations
/// </summary>
public class EventRepository : Repository<Event>, IEventRepository
{
    public EventRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Event>> GetEventsByTypeAsync(EventType type)
    {
        using var context = CreateContext();
        return await context.Set<Event>()
            .AsNoTracking()
            .Where(e => e.Type == type)
            .ToListAsync();
    }
}
