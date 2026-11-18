using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;


namespace RTUB.Application.Services;

/// <summary>
/// Event service implementation using Repository pattern
/// Contains business logic for event operations
/// Follows Single Responsibility and Dependency Inversion principles
/// Now depends on IEventRepository abstraction instead of concrete DbContext
/// </summary>
public partial class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;

    private readonly IImageStorageService _imageStorageService;

    private readonly IEnrollmentRepository _enrollmentRepository;

    private readonly IEventVideoRepository _eventVideoRepository;

    private readonly IEventVideoStorageService _eventVideoStorageService;

    private readonly IPushNotificationFactory _pushNotificationFactory;

    private readonly IPushNotificationService _pushNotificationService;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public EventService(
        IEventRepository eventRepository,
        IImageStorageService imageStorageService,
        IEnrollmentRepository enrollmentRepository,
        IEventVideoRepository eventVideoRepository,
        IEventVideoStorageService eventVideoStorageService,
        IPushNotificationFactory pushNotificationFactory,
        IPushNotificationService pushNotificationService,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _eventRepository = eventRepository;
        _imageStorageService = imageStorageService;
        _enrollmentRepository = enrollmentRepository;
        _eventVideoRepository = eventVideoRepository;
        _eventVideoStorageService = eventVideoStorageService;
        _pushNotificationFactory = pushNotificationFactory;
        _pushNotificationService = pushNotificationService;
        _userManager = userManager;
        _contextFactory = contextFactory;
    }

    public async Task<IEnumerable<Event>> GetEventsByTypeAsync(EventType type, CancellationToken cancellationToken = default)
    {
        return await _eventRepository.GetEventsByTypeAsync(type);
    }

    public async Task<Event> CreateEventAsync(string name, DateTime date, string location, EventType type, string description = "", DateTime? endDate = null, string? imageUrl = null, bool isPublic = true, CancellationToken cancellationToken = default)
    {
        var eventEntity = Event.Create(name, date, location, type, description);
        eventEntity.IsPublic = isPublic;

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

    public async Task UpdateEventAsync(int id, string name, DateTime date, string location, string description, EventType type, DateTime? endDate = null, bool? isPublic = null, CancellationToken cancellationToken = default)
    {
        var eventEntity = await _eventRepository.GetByIdOrThrowAsync(id);

        eventEntity.UpdateDetails(name, date, location, description, type);
        // Null keeps the event's visibility: only an explicit choice changes it.
        eventEntity.IsPublic = isPublic ?? eventEntity.IsPublic;

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

    public async Task RemoveEventImageAsync(int id, CancellationToken cancellationToken = default)
    {
        var eventEntity = await _eventRepository.GetByIdOrThrowAsync(id);
        if (string.IsNullOrEmpty(eventEntity.ImageUrl))
        {
            return;
        }

        await _imageStorageService.DeleteImageAsync(eventEntity.ImageUrl);
        eventEntity.SetImage(null);
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

        // Activities that accounted for this event lose the link (Activity.EventId has no foreign key), in the
        // same save as the deletion, so no activity is left pointing at a deleted event. Both changes are audited.
        await using var db = _contextFactory.CreateDbContext();
        foreach (var activity in await db.Activities.Where(a => a.EventId == id).ToListAsync(cancellationToken))
        {
            activity.EventId = null;
        }

        // The same for gallery media uploaded for the event (the media stays in the gallery).
        foreach (var media in await db.GalleryMedia.Where(m => m.EventId == id).ToListAsync(cancellationToken))
        {
            media.EventId = null;
        }

        if (await db.Events.FindAsync([id], cancellationToken) is { } tracked)
        {
            db.Events.Remove(tracked);
        }

        await db.SaveChangesAsync(cancellationToken);
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
}
