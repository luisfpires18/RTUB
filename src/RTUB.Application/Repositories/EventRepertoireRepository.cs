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
    public EventRepertoireRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public override async Task<EventRepertoire> AddAsync(EventRepertoire entity)
    {
        using var context = CreateContext();

        // Preload Event and Song into context Local cache for audit log display name resolution
        if (entity.EventId > 0)
            await context.Events.FindAsync(entity.EventId);
        if (entity.SongId > 0)
            await context.Songs.FindAsync(entity.SongId);

        await context.Set<EventRepertoire>().AddAsync(entity);
        await context.SaveChangesAsync();
        return entity;
    }

    public override async Task UpdateAsync(EventRepertoire entity)
    {
        using var context = CreateContext();

        // Preload Event and Song into context Local cache for audit log display name resolution
        if (entity.EventId > 0)
            await context.Events.FindAsync(entity.EventId);
        if (entity.SongId > 0)
            await context.Songs.FindAsync(entity.SongId);

        var tracked = await context.Set<EventRepertoire>().FindAsync(entity.Id)
            ?? throw new InvalidOperationException(
                $"EventRepertoire with Id {entity.Id} not found in the database.");
        context.Entry(tracked).CurrentValues.SetValues(entity);
        await context.SaveChangesAsync();
    }

    public override async Task DeleteAsync(int id)
    {
        using var context = CreateContext();
        var entity = await context.Set<EventRepertoire>().FindAsync(id);
        if (entity == null) return;

        // Preload Event and Song into context Local cache for audit log display name resolution
        if (entity.EventId > 0)
            await context.Events.FindAsync(entity.EventId);
        if (entity.SongId > 0)
            await context.Songs.FindAsync(entity.SongId);

        context.Set<EventRepertoire>().Remove(entity);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<EventRepertoire>> GetRepertoireByEventIdAsync(int eventId, DateTime? date = null)
    {
        using var context = CreateContext();
        var query = context.Set<EventRepertoire>()
            .AsNoTracking()
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
        using var context = CreateContext();
        var dateOnly = date.Date;
        return await context.Set<EventRepertoire>()
            .AsNoTracking()
            .AnyAsync(er => er.EventId == eventId && er.SongId == songId && er.RepertoireDate.Date == dateOnly);
    }

    public async Task<EventRepertoire?> GetRepertoireItemWithDetailsAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<EventRepertoire>()
            .AsNoTracking()
            .Include(er => er.Song)
                .ThenInclude(s => s!.Album)
            .Include(er => er.Event)
            .FirstOrDefaultAsync(er => er.Id == id);
    }

    public async Task<IEnumerable<DateTime>> GetRepertoireDatesAsync(int eventId)
    {
        using var context = CreateContext();
        return await context.Set<EventRepertoire>()
            .AsNoTracking()
            .Where(er => er.EventId == eventId)
            .Select(er => er.RepertoireDate.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync();
    }

    public async Task RemoveRepertoireDayAsync(int eventId, DateTime date)
    {
        using var context = CreateContext();
        var dateOnly = date.Date;
        var itemsToRemove = await context.Set<EventRepertoire>()
            .Where(er => er.EventId == eventId && er.RepertoireDate.Date == dateOnly)
            .ToListAsync();

        context.Set<EventRepertoire>().RemoveRange(itemsToRemove);
        await context.SaveChangesAsync();
    }
}
