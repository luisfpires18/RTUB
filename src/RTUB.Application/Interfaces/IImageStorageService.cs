namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing image storage in Cloudflare R2 (S3-compatible)
/// Handles upload, download, and deletion of images for entities
/// </summary>
public interface IImageStorageService
{
    /// <summary>
    /// Uploads an image file to R2 storage
    /// </summary>
    /// <param name="fileStream">Stream containing the image data</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">MIME content type of the image</param>
    /// <param name="entityType">Type of entity (e.g., "profile", "album", "event")</param>
    /// <param name="entityId">ID of the entity</param>
    /// <returns>Public URL of the uploaded image</returns>
    Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string entityType, string entityId);

    /// <summary>
    /// Deletes an image from R2 storage
    /// </summary>
    /// <param name="imageUrl">Public URL of the image to delete</param>
    Task DeleteImageAsync(string imageUrl);

    /// <summary>
    /// Checks if an image exists in R2 storage
    /// </summary>
    /// <param name="imageUrl">Public URL of the image</param>
    /// <returns>True if image exists, false otherwise</returns>
    Task<bool> ImageExistsAsync(string imageUrl);
}
