using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Event operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface IEventService
{
    Task<Event?> GetEventByIdAsync(int id);
    Task<IEnumerable<Event>> GetAllEventsAsync();
    Task<IEnumerable<Event>> GetUpcomingEventsAsync(int count = 10);
    Task<IEnumerable<Event>> GetPastEventsAsync(int count = 10);
    Task<IEnumerable<Event>> GetEventsByTypeAsync(EventType type);
    Task<Event> CreateEventAsync(string name, DateTime date, string location, EventType type, string description = "", DateTime? endDate = null, string? imageUrl = null);
    Task UpdateEventAsync(int id, string name, DateTime date, string location, string description, DateTime? endDate = null);
    Task UpdateEventWithImageAsync(int id, string name, DateTime date, string location, string description, DateTime? endDate, Stream imageStream, string fileName, string contentType);
    Task SetEventImageAsync(int id, Stream imageStream, string fileName, string contentType);
    Task DeleteEventAsync(int id);
    Task CancelEventAsync(int id, string reason);
    Task UncancelEventAsync(int id);
    Task<IEnumerable<EventVideo>> GetVideosByEventIdAsync(int eventId);
    Task<EventVideo> AddVideoAsync(int eventId, Stream fileStream, string fileName, string contentType, string createdByUserId, string? title = null);
    Task UpdateVideoTitleAsync(int videoId, string? title, string userId, bool isAdmin = false);
    Task UpdateVideoOrderAsync(int eventId, List<int> videoIds);
    Task DeleteVideoAsync(int videoId, string userId, bool isAdmin = false);
    Task<int> GetVideoCountByEventIdAsync(int eventId);
    
    // Video play count tracking
    Task RecordVideoPlayAsync(int videoId, string? userId = null);
}
