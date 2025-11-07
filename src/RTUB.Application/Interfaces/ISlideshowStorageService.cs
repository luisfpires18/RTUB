using Microsoft.AspNetCore.Http;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing slideshow image storage and retrieval using IDrive S3
/// </summary>
public interface ISlideshowStorageService
{
    /// <summary>
    /// Uploads a slideshow image to IDrive S3 storage
    /// </summary>
    /// <param name="file">The image file to upload</param>
    /// <param name="slideshowId">The slideshow ID for generating the filename</param>
    /// <returns>The public URL of the uploaded image</returns>
    Task<string> UploadImageAsync(IFormFile file, int slideshowId);

    /// <summary>
    /// Uploads a slideshow image from byte array to IDrive S3 storage
    /// </summary>
    /// <param name="imageData">The image data as byte array</param>
    /// <param name="slideshowId">The slideshow ID for generating the filename</param>
    /// <param name="contentType">The content type of the image (e.g., image/jpeg)</param>
    /// <returns>The public URL of the uploaded image</returns>
    Task<string> UploadImageAsync(byte[] imageData, int slideshowId, string contentType);

    /// <summary>
    /// Generates a URL for accessing a slideshow image
    /// </summary>
    /// <param name="filename">The filename of the image in S3</param>
    /// <returns>Pre-signed URL valid for a limited time, or null if file doesn't exist</returns>
    Task<string?> GetImageUrlAsync(string filename);

    /// <summary>
    /// Deletes a slideshow image from IDrive S3 storage
    /// </summary>
    /// <param name="filename">The filename of the image to delete</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteImageAsync(string filename);

    /// <summary>
    /// Checks if a slideshow image exists in storage
    /// </summary>
    /// <param name="filename">The filename to check</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> ImageExistsAsync(string filename);
}
