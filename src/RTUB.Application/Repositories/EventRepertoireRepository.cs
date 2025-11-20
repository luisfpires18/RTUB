using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for EventRepertoire entity
/// </summary>
public class EventRepertoireRepository : Repository<EventRepertoire>, IEventRepertoireRepository
{
    public EventRepertoireRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EventRepertoire>> GetRepertoireByEventIdAsync(int eventId)
    {
        return await _dbSet
            .Include(er => er.Song)
                .ThenInclude(s => s!.Album)
            .Where(er => er.EventId == eventId)
            .OrderBy(er => er.DisplayOrder)
            .ToListAsync();
    }

    public async Task<bool> SongExistsInRepertoireAsync(int eventId, int songId)
    {
        return await _dbSet
            .AnyAsync(er => er.EventId == eventId && er.SongId == songId);
    }

    public async Task<EventRepertoire?> GetRepertoireItemWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(er => er.Song)
            .Include(er => er.Event)
            .FirstOrDefaultAsync(er => er.Id == id);
    }
}
