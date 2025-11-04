using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Event Repertoire operations
/// Manages the songs associated with events
/// </summary>
public interface IEventRepertoireService
{
    Task<IEnumerable<EventRepertoire>> GetRepertoireByEventIdAsync(int eventId);
    Task<EventRepertoire?> GetRepertoireItemAsync(int id);
    Task<EventRepertoire> AddSongToRepertoireAsync(int eventId, int songId, int displayOrder);
    Task RemoveSongFromRepertoireAsync(int id);
    Task UpdateRepertoireOrderAsync(int eventId, List<int> songIds);
    Task<bool> IsSongInRepertoireAsync(int eventId, int songId);
}
