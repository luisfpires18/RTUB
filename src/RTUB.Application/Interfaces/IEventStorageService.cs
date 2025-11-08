using Microsoft.AspNetCore.Http;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing event image storage and retrieval using IDrive S3
/// </summary>
public interface IEventStorageService
{
    /// <summary>
    /// Uploads an event image to IDrive S3 storage
    /// </summary>
    /// <param name="file">The image file to upload</param>
    /// <param name="eventId">The event ID for generating the filename</param>
    /// <returns>The filename of the uploaded image</returns>
    Task<string> UploadImageAsync(IFormFile file, int eventId);

    /// <summary>
    /// Uploads an event image from byte array to IDrive S3 storage
    /// </summary>
    /// <param name="imageData">The image data as byte array</param>
    /// <param name="eventId">The event ID for generating the filename</param>
    /// <param name="contentType">The content type of the image (e.g., image/jpeg)</param>
    /// <returns>The filename of the uploaded image</returns>
    Task<string> UploadImageAsync(byte[] imageData, int eventId, string contentType);

    /// <summary>
    /// Generates a URL for accessing an event image
    /// </summary>
    /// <param name="filename">The filename of the image in S3</param>
    /// <returns>Pre-signed URL valid for a limited time, or null if file doesn't exist</returns>
    Task<string?> GetImageUrlAsync(string filename);

    /// <summary>
    /// Deletes an event image from IDrive S3 storage
    /// </summary>
    /// <param name="filename">The filename of the image to delete</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteImageAsync(string filename);

    /// <summary>
    /// Checks if an event image exists in storage
    /// </summary>
    /// <param name="filename">The filename to check</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> ImageExistsAsync(string filename);
}
