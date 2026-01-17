namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing naipe educational content storage in Cloudflare R2
/// Handles upload and deletion of videos and images for instrument learning materials
/// </summary>
public interface INaipeMediaStorageService
{
    /// <summary>
    /// Uploads a video for naipe content
    /// </summary>
    /// <param name="fileStream">Stream containing the video data</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">MIME content type</param>
    /// <param name="instrumentType">Type of instrument</param>
    /// <returns>Public URL of the uploaded video</returns>
    Task<string> UploadVideoAsync(Stream fileStream, string fileName, string contentType, string instrumentType);

    /// <summary>
    /// Uploads an image for naipe content
    /// </summary>
    /// <param name="fileStream">Stream containing the image data</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">MIME content type</param>
    /// <param name="instrumentType">Type of instrument</param>
    /// <returns>Public URL of the uploaded image</returns>
    Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string instrumentType);

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
