using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for storing and retrieving gallery media files (images/videos)
/// </summary>
public interface IGalleryMediaStorageService
{
    /// <summary>
    /// Upload a media file and return the URL
    /// </summary>
    /// <param name="fileStream">File content stream</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="contentType">MIME type</param>
    /// <param name="mediaType">Type of media (image or video)</param>
    /// <param name="title">Title of the media (optional, for file naming)</param>
    /// <param name="year">Year of the media (optional, for file naming)</param>
    /// <param name="month">Month of the media (optional, for file naming)</param>
    /// <param name="day">Day of the media (optional, for file naming)</param>
    /// <returns>URL of the uploaded file</returns>
    Task<string> UploadMediaAsync(Stream fileStream, string fileName, string contentType, MediaType mediaType,
        string? title = null, int? year = null, byte? month = null, byte? day = null);

    /// <summary>
    /// Delete a media file
    /// </summary>
    /// <param name="mediaUrl">URL of the file to delete</param>
    Task DeleteMediaAsync(string mediaUrl);

    /// <summary>
    /// Generate thumbnail for video
    /// </summary>
    /// <param name="videoUrl">URL of the video</param>
    /// <returns>URL of the thumbnail</returns>
    Task<string?> GenerateThumbnailAsync(string videoUrl);

    /// <summary>
    /// Get maximum allowed file size for media type
    /// </summary>
    /// <param name="mediaType">Type of media</param>
    /// <returns>Maximum file size in bytes</returns>
    long GetMaxFileSize(MediaType mediaType);
}
