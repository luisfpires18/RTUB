// Events: videos (add, rename, reorder, delete).
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public partial class EventService
{
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
            var notification = _pushNotificationFactory.CreateEventVideoUploadNotification(
                eventEntity,
                uploaderName);

            // Get all users (excluding the uploader)
            var allUserIds = await _userManager.Users
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            // Send to each user
            await _pushNotificationService.SendToSelectedUsersAsync(allUserIds.Where(id => id != createdByUserId), notification);
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
        var videos = await _eventVideoRepository.QueryAsync(q => q
            .Where(v => v.EventId == eventId)
            .ToListAsync(cancellationToken), cancellationToken);

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
}
