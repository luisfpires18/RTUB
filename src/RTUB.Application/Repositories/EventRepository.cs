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
    public EventRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Event>> GetUpcomingEventsAsync(int count = 10)
    {
        var today = DateTime.Today;
        return await _dbSet
            .AsNoTracking()
            .Where(e => (e.EndDate.HasValue ? e.EndDate.Value.Date : e.Date.Date) >= today)
            .OrderBy(e => e.Date)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Event>> GetPastEventsAsync(int count = 10)
    {
        var today = DateTime.Today;
        return await _dbSet
            .AsNoTracking()
            .Where(e => (e.EndDate.HasValue ? e.EndDate.Value.Date : e.Date.Date) < today)
            .OrderByDescending(e => e.Date)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Event>> GetEventsByTypeAsync(EventType type)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(e => e.Type == type)
            .ToListAsync();
    }

    public async Task<Event?> GetEventWithRepertoireAsync(int id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(e => e.RepertoireSongs)
            .ThenInclude(er => er.Song)
            .FirstOrDefaultAsync(e => e.Id == id);
    }
}
