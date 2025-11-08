using Microsoft.AspNetCore.Http;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing profile image storage and retrieval using IDrive S3
/// </summary>
public interface IProfileStorageService
{
    /// <summary>
    /// Uploads a profile image to IDrive S3 storage
    /// </summary>
    /// <param name="file">The image file to upload</param>
    /// <param name="username">The username for generating the filename</param>
    /// <returns>The filename of the uploaded image</returns>
    Task<string> UploadImageAsync(IFormFile file, string username);

    /// <summary>
    /// Uploads a profile image from byte array to IDrive S3 storage
    /// </summary>
    /// <param name="imageData">The image data as byte array</param>
    /// <param name="username">The username for generating the filename</param>
    /// <param name="contentType">The content type of the image (e.g., image/jpeg)</param>
    /// <returns>The filename of the uploaded image</returns>
    Task<string> UploadImageAsync(byte[] imageData, string username, string contentType);

    /// <summary>
    /// Generates a URL for accessing a profile image
    /// </summary>
    /// <param name="filename">The filename of the image in S3</param>
    /// <returns>Pre-signed URL valid for a limited time, or null if file doesn't exist</returns>
    Task<string?> GetImageUrlAsync(string filename);

    /// <summary>
    /// Deletes a profile image from IDrive S3 storage
    /// </summary>
    /// <param name="filename">The filename of the image to delete</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteImageAsync(string filename);

    /// <summary>
    /// Checks if a profile image exists in storage
    /// </summary>
    /// <param name="filename">The filename to check</param>
    /// <returns>True if file exists, false otherwise</returns>
    Task<bool> ImageExistsAsync(string filename);
}
