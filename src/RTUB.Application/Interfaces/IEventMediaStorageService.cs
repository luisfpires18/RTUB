namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing event discussion media storage in Cloudflare R2
/// Handles upload and deletion of images and videos for posts and comments
/// </summary>
public interface IEventMediaStorageService
{
    /// <summary>
    /// Uploads an image for a post or comment
    /// </summary>
    /// <param name="fileStream">Stream containing the image data</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">MIME content type</param>
    /// <param name="eventId">ID of the event</param>
    /// <param name="mediaType">Type of media ("post" or "comment")</param>
    /// <returns>Public URL of the uploaded image</returns>
    Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, int eventId, string mediaType);

    /// <summary>
    /// Uploads a video for a post
    /// </summary>
    /// <param name="fileStream">Stream containing the video data</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">MIME content type</param>
    /// <param name="eventId">ID of the event</param>
    /// <returns>Public URL of the uploaded video</returns>
    Task<string> UploadVideoAsync(Stream fileStream, string fileName, string contentType, int eventId);

    /// <summary>
    /// Deletes media from R2 storage
    /// </summary>
    /// <param name="mediaUrl">Public URL of the media to delete</param>
    Task DeleteMediaAsync(string mediaUrl);

    /// <summary>
    /// Checks if media exists in R2 storage
    /// </summary>
    /// <param name="mediaUrl">Public URL of the media</param>
    /// <returns>True if media exists, false otherwise</returns>
    Task<bool> MediaExistsAsync(string mediaUrl);
}
