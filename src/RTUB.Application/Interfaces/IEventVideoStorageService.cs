namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing event video storage in Cloudflare R2
/// Handles upload and deletion of video files for events
/// </summary>
public interface IEventVideoStorageService
{
    /// <summary>
    /// Uploads a video file for an event
    /// </summary>
    /// <param name="fileStream">Stream containing the video data</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">MIME content type</param>
    /// <param name="eventId">ID of the event</param>
    /// <returns>Public URL of the uploaded video</returns>
    Task<string> UploadVideoAsync(Stream fileStream, string fileName, string contentType, int eventId);

    /// <summary>
    /// Deletes a video from R2 storage
    /// </summary>
    /// <param name="videoUrl">Public URL of the video to delete</param>
    Task DeleteVideoAsync(string videoUrl);

    /// <summary>
    /// Checks if a video exists in R2 storage
    /// </summary>
    /// <param name="videoUrl">Public URL of the video</param>
    /// <returns>True if video exists, false otherwise</returns>
    Task<bool> VideoExistsAsync(string videoUrl);
}
