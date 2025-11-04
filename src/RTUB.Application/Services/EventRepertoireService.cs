using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing event repertoires
/// </summary>
public class EventRepertoireService : IEventRepertoireService
{
    private readonly ApplicationDbContext _context;

    public EventRepertoireService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<EventRepertoire>> GetRepertoireByEventIdAsync(int eventId)
    {
        return await _context.EventRepertoires
            .Include(er => er.Song)
                .ThenInclude(s => s!.Album)
            .Where(er => er.EventId == eventId)
            .OrderBy(er => er.DisplayOrder)
            .ToListAsync();
    }

    public async Task<EventRepertoire?> GetRepertoireItemAsync(int id)
    {
        return await _context.EventRepertoires
            .Include(er => er.Song)
            .Include(er => er.Event)
            .FirstOrDefaultAsync(er => er.Id == id);
    }

    public async Task<EventRepertoire> AddSongToRepertoireAsync(int eventId, int songId, int displayOrder)
    {
        // Check if song already exists in repertoire
        var existing = await _context.EventRepertoires
            .FirstOrDefaultAsync(er => er.EventId == eventId && er.SongId == songId);
        
        if (existing != null)
        {
            throw new InvalidOperationException("Song already exists in event repertoire");
        }

        var repertoireItem = EventRepertoire.Create(eventId, songId, displayOrder);
        _context.EventRepertoires.Add(repertoireItem);
        await _context.SaveChangesAsync();
        
        return repertoireItem;
    }

    public async Task RemoveSongFromRepertoireAsync(int id)
    {
        var repertoireItem = await _context.EventRepertoires.FindAsync(id);
        if (repertoireItem != null)
        {
            _context.EventRepertoires.Remove(repertoireItem);
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateRepertoireOrderAsync(int eventId, List<int> songIds)
    {
        var repertoireItems = await _context.EventRepertoires
            .Where(er => er.EventId == eventId)
            .ToListAsync();

        for (int i = 0; i < songIds.Count; i++)
        {
            var item = repertoireItems.FirstOrDefault(er => er.SongId == songIds[i]);
            if (item != null)
            {
                item.UpdateOrder(i + 1);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<bool> IsSongInRepertoireAsync(int eventId, int songId)
    {
        return await _context.EventRepertoires
            .AnyAsync(er => er.EventId == eventId && er.SongId == songId);
    }
}
