using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;
using RTUB.Application.Utilities;


namespace RTUB.Application.Services;

/// <summary>
/// Event service implementation using Repository pattern
/// Contains business logic for event operations
/// Follows Single Responsibility and Dependency Inversion principles
/// Now depends on IEventRepository abstraction instead of concrete DbContext
/// </summary>
public class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly IImageStorageService _imageStorageService;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IEventVideoRepository _eventVideoRepository;
    private readonly IEventVideoStorageService _eventVideoStorageService;

    public EventService(IEventRepository eventRepository, IImageStorageService imageStorageService, IEnrollmentRepository enrollmentRepository, IEventVideoRepository eventVideoRepository, IEventVideoStorageService eventVideoStorageService)
    {
        _eventRepository = eventRepository;
        _imageStorageService = imageStorageService;
        _enrollmentRepository = enrollmentRepository;
        _eventVideoRepository = eventVideoRepository;
        _eventVideoStorageService = eventVideoStorageService;
    }

    public async Task<Event?> GetEventByIdAsync(int id)
    {
        return await _eventRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Event>> GetAllEventsAsync()
    {
        return await _eventRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Event>> GetUpcomingEventsAsync(int count = 10)
    {
        return await _eventRepository.GetUpcomingEventsAsync(count);
    }

    public async Task<IEnumerable<Event>> GetPastEventsAsync(int count = 10)
    {
        return await _eventRepository.GetPastEventsAsync(count);
    }

    public async Task<IEnumerable<Event>> GetEventsByTypeAsync(EventType type)
    {
        return await _eventRepository.GetEventsByTypeAsync(type);
    }

    public async Task<Event> CreateEventAsync(string name, DateTime date, string location, EventType type, string description = "", DateTime? endDate = null, string? imageUrl = null)
    {
        var eventEntity = Event.Create(name, date, location, type, description);

        if (endDate.HasValue)
        {
            eventEntity.SetEndDate(endDate);
        }

        if (!string.IsNullOrEmpty(imageUrl))
        {
            eventEntity.SetImage(imageUrl);
        }

        return await _eventRepository.AddAsync(eventEntity);
    }

    public async Task UpdateEventAsync(int id, string name, DateTime date, string location, string description, DateTime? endDate = null)
    {
        var eventEntity = await _eventRepository.GetByIdAsync(id);
        if (eventEntity == null)
            throw new EntityNotFoundException(nameof(Event), id);

        eventEntity.UpdateDetails(name, date, location, description);

        if (endDate.HasValue)
        {
            eventEntity.SetEndDate(endDate);
        }
        else
        {
            eventEntity.EndDate = null;
        }

        await _eventRepository.UpdateAsync(eventEntity);
    }

    public async Task UpdateEventWithImageAsync(int id, string name, DateTime date, string location, string description, DateTime? endDate, Stream imageStream, string fileName, string contentType)
    {
        var eventEntity = await _eventRepository.GetByIdAsync(id);
        if (eventEntity == null)
            throw new EntityNotFoundException(nameof(Event), id);

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

        // Upload new image to Cloudflare R2 using normalized event name
        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(name);
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "events", normalizedName);
        eventEntity.SetImage(imageUrl);

        await _eventRepository.UpdateAsync(eventEntity);
    }

    public async Task SetEventImageAsync(int id, Stream imageStream, string fileName, string contentType)
    {
        var eventEntity = await _eventRepository.GetByIdAsync(id);
        if (eventEntity == null)
            throw new EntityNotFoundException(nameof(Event), id);

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(eventEntity.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(eventEntity.ImageUrl);
        }

        // Upload new image to Cloudflare R2 using normalized event name
        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(eventEntity.Name);
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "events", normalizedName);
        eventEntity.SetImage(imageUrl);

        await _eventRepository.UpdateAsync(eventEntity);
    }

    public async Task DeleteEventAsync(int id)
    {
        var eventEntity = await _eventRepository.GetByIdAsync(id);
        if (eventEntity == null)
            throw new EntityNotFoundException(nameof(Event), id);

        // Delete associated image from R2 storage if it exists
        if (!string.IsNullOrEmpty(eventEntity.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(eventEntity.ImageUrl);
        }

        await _eventRepository.DeleteAsync(eventEntity);
    }

    public async Task CancelEventAsync(int id, string reason)
    {
        var eventEntity = await _eventRepository.GetByIdAsync(id);
        if (eventEntity == null)
            throw new EntityNotFoundException(nameof(Event), id);

        eventEntity.Cancel(reason);
        await _eventRepository.UpdateAsync(eventEntity);

        // Delete all enrollments for this event using batch operation
        await _enrollmentRepository.DeleteByEventIdAsync(id);
    }

    public async Task UncancelEventAsync(int id)
    {
        var eventEntity = await _eventRepository.GetByIdAsync(id);
        if (eventEntity == null)
            throw new EntityNotFoundException(nameof(Event), id);

        eventEntity.Uncancel();
        await _eventRepository.UpdateAsync(eventEntity);
    }

    public async Task<IEnumerable<EventVideo>> GetVideosByEventIdAsync(int eventId)
    {
        return await _eventVideoRepository.GetByEventIdAsync(eventId);
    }

    public async Task<EventVideo> AddVideoAsync(int eventId, Stream fileStream, string fileName, string contentType, string createdByUserId, string? title = null)
    {
        // Validate that event exists
        var eventEntity = await _eventRepository.GetByIdAsync(eventId);
        if (eventEntity == null)
            throw new EntityNotFoundException(nameof(Event), eventId);

        // Upload video to storage
        var videoUrl = await _eventVideoStorageService.UploadVideoAsync(fileStream, fileName, contentType, eventId);

        // Get file size from stream position (if seekable)
        long sizeBytes = 0;
        if (fileStream.CanSeek)
        {
            sizeBytes = fileStream.Length;
        }

        // Determine sort order (next available position)
        var existingVideosCount = await _eventVideoRepository.GetCountByEventIdAsync(eventId);
        var sortOrder = existingVideosCount;

        // Create EventVideo entity
        var eventVideo = EventVideo.CreateVideo(
            eventId,
            videoUrl,
            contentType,
            sizeBytes,
            createdByUserId,
            title,
            sortOrder
        );

        // Save to repository
        var createdVideo = await _eventVideoRepository.AddAsync(eventVideo);

        return createdVideo;
    }

    public async Task DeleteVideoAsync(int videoId, string userId, bool isAdmin = false)
    {
        // Fetch video by id
        var video = await _eventVideoRepository.GetByIdAsync(videoId);

        if (video == null)
            throw new EntityNotFoundException(nameof(EventVideo), videoId);

        // Check permissions: only allow if user is the uploader OR is an admin
        if (video.CreatedByUserId != userId && !isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to delete this video.");
        }

        // Delete from storage
        try
        {
            await _eventVideoStorageService.DeleteVideoAsync(video.Url);
        }
        catch
        {
            // Continue with database deletion even if storage deletion fails
            // Storage service already logs errors internally
        }

        // Delete from repository
        await _eventVideoRepository.DeleteAsync(video);
    }

    public async Task<int> GetVideoCountByEventIdAsync(int eventId)
    {
        return await _eventVideoRepository.GetCountByEventIdAsync(eventId);
    }
}
