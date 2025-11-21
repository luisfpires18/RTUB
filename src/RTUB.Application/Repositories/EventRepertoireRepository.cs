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
    
    public override async Task<EventRepertoire> AddAsync(EventRepertoire entity)
    {
        // Ensure the Event is loaded into Local cache for audit log display name resolution
        if (entity.EventId > 0)
        {
            var evt = await _context.Events.FindAsync(entity.EventId);
        }
        
        // Ensure the Song is loaded into Local cache for audit log display name resolution
        if (entity.SongId > 0)
        {
            var song = await _context.Songs.FindAsync(entity.SongId);
        }
        
        return await base.AddAsync(entity);
    }
    
    public override async Task UpdateAsync(EventRepertoire entity)
    {
        // Ensure the Event is loaded into Local cache for audit log display name resolution
        if (entity.EventId > 0)
        {
            var evt = await _context.Events.FindAsync(entity.EventId);
        }
        
        // Ensure the Song is loaded into Local cache for audit log display name resolution
        if (entity.SongId > 0)
        {
            var song = await _context.Songs.FindAsync(entity.SongId);
        }
        
        await base.UpdateAsync(entity);
    }
    
    public override async Task DeleteAsync(int id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
        {
            // Ensure the Event is loaded into Local cache for audit log display name resolution
            if (entity.EventId > 0)
            {
                var evt = await _context.Events.FindAsync(entity.EventId);
            }
            
            // Ensure the Song is loaded into Local cache for audit log display name resolution
            if (entity.SongId > 0)
            {
                var song = await _context.Songs.FindAsync(entity.SongId);
            }
        }
        
        await base.DeleteAsync(id);
    }

    public async Task<IEnumerable<EventRepertoire>> GetRepertoireByEventIdAsync(int eventId, DateTime? date = null)
    {
        var query = _dbSet
            .Include(er => er.Song)
                .ThenInclude(s => s!.Album)
            .Where(er => er.EventId == eventId);
        
        if (date.HasValue)
        {
            var dateOnly = date.Value.Date;
            query = query.Where(er => er.RepertoireDate.Date == dateOnly);
        }
        
        return await query
            .OrderBy(er => er.DisplayOrder)
            .ToListAsync();
    }

    public async Task<bool> SongExistsInRepertoireAsync(int eventId, int songId, DateTime date)
    {
        var dateOnly = date.Date;
        return await _dbSet
            .AnyAsync(er => er.EventId == eventId && er.SongId == songId && er.RepertoireDate.Date == dateOnly);
    }

    public async Task<EventRepertoire?> GetRepertoireItemWithDetailsAsync(int id)
    {
        return await _dbSet
            .Include(er => er.Song)
            .Include(er => er.Event)
            .FirstOrDefaultAsync(er => er.Id == id);
    }
    
    public async Task<IEnumerable<DateTime>> GetRepertoireDatesAsync(int eventId)
    {
        return await _dbSet
            .Where(er => er.EventId == eventId)
            .Select(er => er.RepertoireDate.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();
    }
    
    public async Task RemoveRepertoireDayAsync(int eventId, DateTime date)
    {
        var dateOnly = date.Date;
        var itemsToRemove = await _dbSet
            .Where(er => er.EventId == eventId && er.RepertoireDate.Date == dateOnly)
            .ToListAsync();
        
        _dbSet.RemoveRange(itemsToRemove);
        await _context.SaveChangesAsync();
    }
}
