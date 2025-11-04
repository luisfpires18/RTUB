using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;


namespace RTUB.Application.Services;

/// <summary>
/// Event service implementation
/// Contains business logic for event operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class EventService : IEventService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageService _imageService;

    public EventService(ApplicationDbContext context, IImageService imageService)
    {
        _context = context;
        _imageService = imageService;
    }

    public async Task<Event?> GetEventByIdAsync(int id)
    {
        return await _context.Events.FindAsync(id);
    }

    public async Task<IEnumerable<Event>> GetAllEventsAsync()
    {
        return await _context.Events.ToListAsync();
    }

    public async Task<IEnumerable<Event>> GetUpcomingEventsAsync(int count = 10)
    {
        var now = DateTime.Now;
        return await _context.Events
            .Where(e => (e.EndDate.HasValue ? e.EndDate.Value : e.Date) >= now)
            .OrderBy(e => e.Date)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Event>> GetPastEventsAsync(int count = 10)
    {
        var now = DateTime.Now;
        return await _context.Events
            .Where(e => (e.EndDate.HasValue ? e.EndDate.Value : e.Date) < now)
            .OrderByDescending(e => e.Date)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Event>> GetEventsByTypeAsync(EventType type)
    {
        return await _context.Events
            .Where(e => e.Type == type)
            .ToListAsync();
    }

    public async Task<Event> CreateEventAsync(string name, DateTime date, string location, EventType type, string description = "")
    {
        var eventEntity = Event.Create(name, date, location, type, description);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();
        return eventEntity;
    }

    public async Task UpdateEventAsync(int id, string name, DateTime date, string location, string description, DateTime? endDate = null)
    {
        var eventEntity = await _context.Events.FindAsync(id);
        if (eventEntity == null)
            throw new InvalidOperationException($"Event with ID {id} not found");

        eventEntity.UpdateDetails(name, date, location, description);
        
        if (endDate.HasValue)
        {
            eventEntity.SetEndDate(endDate);
        }
        else
        {
            eventEntity.EndDate = null;
        }
        
        _context.Events.Update(eventEntity);
        await _context.SaveChangesAsync();
    }

    public async Task SetEventImageAsync(int id, byte[]? imageData, string? contentType, string url = "")
    {
        var eventEntity = await _context.Events.FindAsync(id);
        if (eventEntity == null)
            throw new InvalidOperationException($"Event with ID {id} not found");

        eventEntity.SetImage(imageData, contentType, url);
        _context.Events.Update(eventEntity);
        await _context.SaveChangesAsync();
        
        // Invalidate the cached image so the new image is served immediately
        _imageService.InvalidateEventImageCache(id);
    }

    public async Task DeleteEventAsync(int id)
    {
        var eventEntity = await _context.Events.FindAsync(id);
        if (eventEntity == null)
            throw new InvalidOperationException($"Event with ID {id} not found");

        _context.Events.Remove(eventEntity);
        await _context.SaveChangesAsync();
    }
}
