// Songs: YouTube links, uploaded videos and play count.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Extensions;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

public partial class SongService
{
    public async Task AddYouTubeUrlAsync(int songId, string url)
    {
        var song = await _songRepository.GetSongForUpdateAsync(songId);

        if (song == null)
            throw new EntityNotFoundException(nameof(Song), songId);

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("YouTube URL cannot be empty", nameof(url));

        var canonicalUrl = CanonicalizeUrl(url.Trim());

        // Check if URL already exists (avoid duplicates)
        if (song.YouTubeUrls.Any(y => CanonicalizeUrl(y.Url) == canonicalUrl))
        {
            return; // URL already exists, don't add duplicate
        }

        // Add YouTube URL to song directly via repository
        var youtubeUrl = new SongYouTubeUrl
        {
            SongId = songId,
            Url = url.Trim()
        };

        await _songRepository.AddYouTubeUrlAsync(youtubeUrl);
    }

    public async Task RemoveYouTubeUrlAsync(int songId, string url)
    {
        var song = await _songRepository.GetSongForUpdateAsync(songId);

        if (song == null)
            throw new EntityNotFoundException(nameof(Song), songId);

        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("YouTube URL cannot be empty", nameof(url));

        var canonicalUrl = CanonicalizeUrl(url.Trim());

        // Remove YouTube URL from song - use canonicalized comparison
        var youtubeUrl = song.YouTubeUrls.FirstOrDefault(y => CanonicalizeUrl(y.Url) == canonicalUrl);
        if (youtubeUrl != null)
        {
            // Use repository method to ensure consistent data access pattern
            await _songRepository.DeleteYouTubeUrlAsync(youtubeUrl);
        }
        else
        {
            _logger?.LogWarning("Attempted to remove non-existent YouTube URL from song {SongId}: {Url}",
                songId, canonicalUrl);
        }
    }

    /// <summary>
    /// Canonicalizes a URL by trimming and normalizing to lowercase for comparison.
    /// This provides basic normalization for duplicate detection.
    /// Note: Does not handle protocol differences (http vs https), trailing slashes,
    /// or query parameter ordering - these are preserved from user input.
    /// </summary>
    private string CanonicalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        return url.Trim().ToLowerInvariant();
    }

    public async Task<SongVideo> AddVideoAsync(int songId, Stream fileStream, string fileName, string contentType, string createdByUserId, string? title = null)
    {
        // Validate that song exists
        var song = await _songRepository.GetSongForUpdateAsync(songId);
        if (song == null)
            throw new EntityNotFoundException(nameof(Song), songId);

        // Ensure we have a valid MIME type (mobile uploads may have empty/incorrect contentType)
        var mimeType = MimeTypeHelper.GetVideoMimeType(fileName, contentType);

        // Upload video to storage
        var videoUrl = await _songVideoStorageService.UploadVideoAsync(fileStream, fileName, mimeType, songId);

        // Get file size from stream position (if seekable)
        long sizeBytes = 0;
        if (fileStream.CanSeek)
        {
            sizeBytes = fileStream.Length;
        }

        // Determine sort order (next available position)
        var existingVideosCount = await _songVideoRepository.GetCountBySongIdAsync(songId);
        var sortOrder = existingVideosCount;

        // Create SongVideo entity
        var songVideo = SongVideo.CreateVideo(
            songId,
            videoUrl,
            mimeType,
            sizeBytes,
            createdByUserId,
            title,
            sortOrder
        );

        // Save to repository
        var createdVideo = await _songVideoRepository.AddAsync(songVideo);

        // Send push notification to users
        try
        {
            var uploader = await _userManager.FindByIdAsync(createdByUserId);
            var uploaderName = uploader?.Nickname ?? uploader?.FirstName ?? "Um membro";
            var notification = _pushNotificationFactory.CreateSongVideoUploadNotification(
                song,
                uploaderName);

            // Get the album to check if it's exclusive
            var album = await _albumRepository.GetByIdAsync(song.AlbumId);

            List<string> targetUserIds;
            if (album?.IsExclusive == true)
            {
                // For exclusive albums, only notify authorized users
                var authorizedUserIds = await _albumRepository.GetAuthorizedUserIdsAsync(song.AlbumId);
                targetUserIds = authorizedUserIds.ToList();
            }
            else
            {
                // For non-exclusive albums, notify all users
                targetUserIds = await _userManager.Users
                    .Select(u => u.Id)
                    .ToListAsync();
            }

            // Send to each user (excluding the uploader)
            await _pushNotificationService.SendToSelectedUsersAsync(targetUserIds.Where(id => id != createdByUserId), notification);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the operation
            _logger?.LogError(ex, "Failed to send push notifications for song video upload. SongId: {SongId}", songId);
        }

        return createdVideo;
    }

    public async Task DeleteVideoAsync(int videoId, string userId, bool isAdmin = false)
    {
        // Fetch video by id
        var video = await _songVideoRepository.GetByIdOrThrowAsync(videoId);

        // Check permissions: only allow if user is the uploader OR is an admin
        if (video.CreatedByUserId != userId && !isAdmin)
        {
            _logger?.LogWarning("User {UserId} attempted to delete video {VideoId} created by {CreatedByUserId} without permission",
                userId, videoId, video.CreatedByUserId);
            throw new UnauthorizedAccessException("You do not have permission to delete this video.");
        }

        // Delete from storage
        try
        {
            await _songVideoStorageService.DeleteVideoAsync(video.Url);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete video from storage: {VideoUrl}", video.Url);
            // Continue with database deletion even if storage deletion fails
        }

        // Delete from repository
        await _songVideoRepository.DeleteAsync(video);
    }

    // Play count tracking methods
    public async Task IncrementPlayCountAsync(int songId, string? userId = null)
    {
        var playCount = new SongPlayCount
        {
            SongId = songId,
            UserId = userId,
            PlayedAt = DateTime.UtcNow
        };

        using var context = _contextFactory.CreateDbContext();
        context.SongPlayCounts.Add(playCount);
        await context.SaveChangesAsync();
    }
}
