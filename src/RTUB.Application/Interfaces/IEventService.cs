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
    Task<Event> CreateEventAsync(string name, DateTime date, string location, EventType type, string description = "");
    Task UpdateEventAsync(int id, string name, DateTime date, string location, string description, DateTime? endDate = null);
    Task SetEventS3ImageAsync(int id, string? s3Filename);
    Task DeleteEventAsync(int id);
}
