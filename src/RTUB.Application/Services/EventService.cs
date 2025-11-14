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
    private readonly IImageStorageService _imageStorageService;

    public EventService(ApplicationDbContext context, IImageStorageService imageStorageService)
    {
        _context = context;
        _imageStorageService = imageStorageService;
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
        var today = DateTime.Today;
        return await _context.Events
            .Where(e => (e.EndDate.HasValue ? e.EndDate.Value.Date : e.Date.Date) >= today)
            .OrderBy(e => e.Date)
            .Take(count)
            .ToListAsync();
    }

    public async Task<IEnumerable<Event>> GetPastEventsAsync(int count = 10)
    {
        var today = DateTime.Today;
        return await _context.Events
            .Where(e => (e.EndDate.HasValue ? e.EndDate.Value.Date : e.Date.Date) < today)
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

    public async Task<Event> CreateEventAsync(string name, DateTime date, string location, EventType type, string description = "", DateTime? endDate = null)
    {
        var eventEntity = Event.Create(name, date, location, type, description);
        
        if (endDate.HasValue)
        {
            eventEntity.SetEndDate(endDate);
        }
        
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();
        return eventEntity;
    }

    public async Task<Event> CreateEventWithImageAsync(string name, DateTime date, string location, EventType type, string description, DateTime? endDate, Stream imageStream, string fileName, string contentType)
    {
        var eventEntity = Event.Create(name, date, location, type, description);
        
        if (endDate.HasValue)
        {
            eventEntity.SetEndDate(endDate);
        }
        
        _context.Events.Add(eventEntity);
        
        // Save to get the ID - this creates the "Created" audit log
        await _context.SaveChangesAsync();
        
        // Upload image to Cloudflare R2 using the generated ID
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "events", eventEntity.Id.ToString());
        
        // Set the image URL - EF Core is still tracking this entity
        eventEntity.SetImage(imageUrl);
        
        // Save again - this will create a "Modified" audit log with ImageUrl change
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

    public async Task UpdateEventWithImageAsync(int id, string name, DateTime date, string location, string description, DateTime? endDate, Stream imageStream, string fileName, string contentType)
    {
        var eventEntity = await _context.Events.FindAsync(id);
        if (eventEntity == null)
            throw new InvalidOperationException($"Event with ID {id} not found");

        // Update event details
        eventEntity.UpdateDetails(name, date, location, description);
        
        if (endDate.HasValue)
        {
            eventEntity.SetEndDate(endDate);
        }
        else
        {
            eventEntity.EndDate = null;
        }

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(eventEntity.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(eventEntity.ImageUrl);
        }

        // Upload new image to Cloudflare R2
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "events", id.ToString());
        eventEntity.SetImage(imageUrl);
        
        _context.Events.Update(eventEntity);
        await _context.SaveChangesAsync();
    }

    public async Task SetEventImageAsync(int id, Stream imageStream, string fileName, string contentType)
    {
        var eventEntity = await _context.Events.FindAsync(id);
        if (eventEntity == null)
            throw new InvalidOperationException($"Event with ID {id} not found");

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(eventEntity.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(eventEntity.ImageUrl);
        }

        // Upload new image to Cloudflare R2
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "events", id.ToString());
        eventEntity.SetImage(imageUrl);
        
        _context.Events.Update(eventEntity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteEventAsync(int id)
    {
        var eventEntity = await _context.Events.FindAsync(id);
        if (eventEntity == null)
            throw new InvalidOperationException($"Event with ID {id} not found");

        // Delete associated image from R2 storage if it exists
        if (!string.IsNullOrEmpty(eventEntity.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(eventEntity.ImageUrl);
        }

        _context.Events.Remove(eventEntity);
        await _context.SaveChangesAsync();
    }

    public async Task CancelEventAsync(int id, string reason)
    {
        var eventEntity = await _context.Events.FindAsync(id);
        if (eventEntity == null)
            throw new InvalidOperationException($"Event with ID {id} not found");

        eventEntity.Cancel(reason);
        _context.Events.Update(eventEntity);
        await _context.SaveChangesAsync();
    }

    public async Task UncancelEventAsync(int id)
    {
        var eventEntity = await _context.Events.FindAsync(id);
        if (eventEntity == null)
            throw new InvalidOperationException($"Event with ID {id} not found");

        eventEntity.Uncancel();
        _context.Events.Update(eventEntity);
        await _context.SaveChangesAsync();
    }
}
