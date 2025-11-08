using Microsoft.AspNetCore.Http;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for album cover image storage service
/// </summary>
public interface IAlbumStorageService
{
    /// <summary>
    /// Uploads an album cover image from an IFormFile
    /// </summary>
    Task<string> UploadImageAsync(IFormFile file, int albumId);

    /// <summary>
    /// Uploads an album cover image from a byte array
    /// </summary>
    Task<string> UploadImageAsync(byte[] imageData, int albumId, string contentType);

    /// <summary>
    /// Gets the public URL for an album cover image
    /// </summary>
    Task<string?> GetImageUrlAsync(string filename);

    /// <summary>
    /// Deletes an album cover image from storage
    /// </summary>
    Task<bool> DeleteImageAsync(string filename);

    /// <summary>
    /// Checks if an album cover image exists in storage
    /// </summary>
    Task<bool> ImageExistsAsync(string filename);
}
