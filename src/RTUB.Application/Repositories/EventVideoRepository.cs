using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for EventVideo entity
/// </summary>
public class EventVideoRepository : Repository<EventVideo>, IEventVideoRepository
{
    public EventVideoRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EventVideo>> GetByEventIdAsync(int eventId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ev => ev.CreatedByUser)
            .Where(ev => ev.EventId == eventId)
            .OrderBy(ev => ev.SortOrder)
            .ThenBy(ev => ev.CreatedAt)
            .ToListAsync();
    }

    public async Task<int> GetCountByEventIdAsync(int eventId)
    {
        return await _dbSet
            .Where(ev => ev.EventId == eventId)
            .CountAsync();
    }
}
