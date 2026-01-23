using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;


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
    private readonly IPushNotificationFactory _pushNotificationFactory;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _context;

    public EventService(
        IEventRepository eventRepository,
        IImageStorageService imageStorageService,
        IEnrollmentRepository enrollmentRepository,
        IEventVideoRepository eventVideoRepository,
        IEventVideoStorageService eventVideoStorageService,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor httpContextAccessor,
        ApplicationDbContext context)
    {
        _eventRepository = eventRepository;
        _imageStorageService = imageStorageService;
        _enrollmentRepository = enrollmentRepository;
        _eventVideoRepository = eventVideoRepository;
        _eventVideoStorageService = eventVideoStorageService;
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _userManager = userManager;
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public async Task<Event?> GetEventByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _eventRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Event>> GetAllEventsAsync(CancellationToken cancellationToken = default)
    {
        return await _eventRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Event>> GetUpcomingEventsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return await _eventRepository.GetUpcomingEventsAsync(count);
    }

    public async Task<IEnumerable<Event>> GetPastEventsAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        return await _eventRepository.GetPastEventsAsync(count);
    }

    public async Task<IEnumerable<Event>> GetEventsByTypeAsync(EventType type, CancellationToken cancellationToken = default)
    {
        return await _eventRepository.GetEventsByTypeAsync(type);
    }

    public async Task<Event> CreateEventAsync(string name, DateTime date, string location, EventType type, string description = "", DateTime? endDate = null, string? imageUrl = null, CancellationToken cancellationToken = default)
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

    public async Task UpdateEventAsync(int id, string name, DateTime date, string location, string description, EventType type, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var eventEntity = await _eventRepository.GetByIdOrThrowAsync(id);

        eventEntity.UpdateDetails(name, date, location, description, type);

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

    public async Task UpdateEventWithImageAsync(int id, string name, DateTime date, string location, string description, EventType type, DateTime? endDate, Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var eventEntity = await _eventRepository.GetByIdOrThrowAsync(id);

        // Update event details
        eventEntity.UpdateDetails(name, date, location, description, type);

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

    public async Task SetEventImageAsync(int id, Stream imageStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var eventEntity = await _eventRepository.GetByIdOrThrowAsync(id);

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

    public async Task DeleteEventAsync(int id, CancellationToken cancellationToken = default)
    {
        var eventEntity = await _eventRepository.GetByIdOrThrowAsync(id);

        // Delete associated image from R2 storage if it exists
        if (!string.IsNullOrEmpty(eventEntity.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(eventEntity.ImageUrl);
        }

        await _eventRepository.DeleteAsync(eventEntity);
    }

    public async Task CancelEventAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        var eventEntity = await _eventRepository.GetByIdOrThrowAsync(id);

        eventEntity.Cancel(reason);
        await _eventRepository.UpdateAsync(eventEntity);

        // Delete all enrollments for this event using batch operation
        await _enrollmentRepository.DeleteByEventIdAsync(id);
    }

    public async Task UncancelEventAsync(int id, CancellationToken cancellationToken = default)
    {
        var eventEntity = await _eventRepository.GetByIdOrThrowAsync(id);

        eventEntity.Uncancel();
        await _eventRepository.UpdateAsync(eventEntity);
    }

    public async Task<IEnumerable<EventVideo>> GetVideosByEventIdAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await _eventVideoRepository.GetByEventIdAsync(eventId);
    }

    public async Task<EventVideo> AddVideoAsync(int eventId, Stream fileStream, string fileName, string contentType, string createdByUserId, string? title = null, CancellationToken cancellationToken = default)
    {
        // Validate that event exists
        var eventEntity = await _eventRepository.GetByIdOrThrowAsync(eventId);

        // Ensure we have a valid MIME type (mobile uploads may have empty/incorrect contentType)
        var mimeType = MimeTypeHelper.GetVideoMimeType(fileName, contentType);

        // Upload video to storage
        var videoUrl = await _eventVideoStorageService.UploadVideoAsync(fileStream, fileName, mimeType, eventId);

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
            mimeType,
            sizeBytes,
            createdByUserId,
            title,
            sortOrder
        );

        // Save to repository
        var createdVideo = await _eventVideoRepository.AddAsync(eventVideo);

        // Send push notification to all users
        try
        {
            var uploader = await _userManager.FindByIdAsync(createdByUserId);
            var uploaderName = uploader?.Nickname ?? uploader?.FirstName ?? "Um membro";
            var baseUrl = GetBaseUrl();

            var notification = _pushNotificationFactory.CreateEventVideoUploadNotification(
                eventEntity,
                uploaderName,
                baseUrl);

            // Get all users (excluding the uploader)
            var allUserIds = await _userManager.Users
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            // Send to each user
            foreach (var userId in allUserIds.Where(id => id != createdByUserId))
            {
                await _pushNotificationService.SendToUserAsync(userId, notification);
            }
        }
        catch
        {
            // Log error but don't fail the operation
            // Notification is secondary to the main operation
            // Note: EventService doesn't have a logger injected, so we silently catch
            // Consider adding ILogger<EventService> in the future
        }

        return createdVideo;
    }

    public async Task UpdateVideoTitleAsync(int videoId, string? title, string userId, bool isAdmin = false, CancellationToken cancellationToken = default)
    {
        // Fetch video by id
        var video = await _eventVideoRepository.GetByIdOrThrowAsync(videoId);

        // Check permissions: only allow if user is the uploader OR is an admin
        if (video.CreatedByUserId != userId && !isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to update this video.");
        }

        // Update the title
        video.UpdateTitle(title);

        // Save to repository
        await _eventVideoRepository.UpdateAsync(video);
    }

    public async Task UpdateVideoOrderAsync(int eventId, List<int> videoIds, CancellationToken cancellationToken = default)
    {
        var videos = await _eventVideoRepository.Query()
            .Where(v => v.EventId == eventId)
            .ToListAsync(cancellationToken);

        for (int i = 0; i < videoIds.Count; i++)
        {
            var video = videos.FirstOrDefault(v => v.Id == videoIds[i]);
            if (video != null)
            {
                video.UpdateOrder(i);
                await _eventVideoRepository.UpdateAsync(video);
            }
        }
    }

    public async Task DeleteVideoAsync(int videoId, string userId, bool isAdmin = false, CancellationToken cancellationToken = default)
    {
        // Fetch video by id
        var video = await _eventVideoRepository.GetByIdOrThrowAsync(videoId);

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

    public async Task<int> GetVideoCountByEventIdAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await _eventVideoRepository.GetCountByEventIdAsync(eventId);
    }

    private string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}";
        }
        return "https://rtub.pt"; // Fallback
    }
}
