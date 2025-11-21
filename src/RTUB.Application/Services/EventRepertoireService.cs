using Microsoft.EntityFrameworkCore;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing event repertoires
/// </summary>
public class EventRepertoireService : IEventRepertoireService
{
    private readonly IEventRepertoireRepository _repertoireRepository;

    public EventRepertoireService(IEventRepertoireRepository repertoireRepository)
    {
        _repertoireRepository = repertoireRepository;
    }

    public async Task<IEnumerable<EventRepertoire>> GetRepertoireByEventIdAsync(int eventId, DateTime? date = null)
    {
        return await _repertoireRepository.GetRepertoireByEventIdAsync(eventId, date);
    }

    public async Task<EventRepertoire?> GetRepertoireItemAsync(int id)
    {
        return await _repertoireRepository.GetRepertoireItemWithDetailsAsync(id);
    }

    public async Task<EventRepertoire> AddSongToRepertoireAsync(int eventId, int songId, int displayOrder, DateTime repertoireDate)
    {
        // Check if song already exists in repertoire for this date
        var exists = await _repertoireRepository.SongExistsInRepertoireAsync(eventId, songId, repertoireDate);
        
        if (exists)
        {
            throw new InvalidOperationException("Song already exists in event repertoire for this date");
        }

        var repertoireItem = EventRepertoire.Create(eventId, songId, displayOrder, repertoireDate);
        return await _repertoireRepository.AddAsync(repertoireItem);
    }

    public async Task RemoveSongFromRepertoireAsync(int id)
    {
        await _repertoireRepository.DeleteAsync(id);
    }

    public async Task UpdateRepertoireOrderAsync(int eventId, DateTime date, List<int> songIds)
    {
        var dateOnly = date.Date;
        var repertoireItems = await _repertoireRepository.Query()
            .Where(er => er.EventId == eventId && er.RepertoireDate.Date == dateOnly)
            .ToListAsync();

        for (int i = 0; i < songIds.Count; i++)
        {
            var item = repertoireItems.FirstOrDefault(er => er.SongId == songIds[i]);
            if (item != null)
            {
                item.UpdateOrder(i + 1);
                await _repertoireRepository.UpdateAsync(item);
            }
        }
    }

    public async Task<bool> IsSongInRepertoireAsync(int eventId, int songId, DateTime date)
    {
        return await _repertoireRepository.SongExistsInRepertoireAsync(eventId, songId, date);
    }
    
    public async Task RemoveRepertoireDayAsync(int eventId, DateTime date)
    {
        await _repertoireRepository.RemoveRepertoireDayAsync(eventId, date);
    }
    
    public async Task<IEnumerable<DateTime>> GetRepertoireDatesAsync(int eventId)
    {
        return await _repertoireRepository.GetRepertoireDatesAsync(eventId);
    }
}
