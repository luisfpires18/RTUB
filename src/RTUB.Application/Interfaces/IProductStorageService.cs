using Microsoft.AspNetCore.Http;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for product image storage service
/// </summary>
public interface IProductStorageService
{
    /// <summary>
    /// Uploads a product image from an IFormFile
    /// </summary>
    Task<string> UploadImageAsync(IFormFile file, int productId);

    /// <summary>
    /// Uploads a product image from a byte array
    /// </summary>
    Task<string> UploadImageAsync(byte[] imageData, int productId, string contentType);

    /// <summary>
    /// Gets the public URL for a product image
    /// </summary>
    Task<string?> GetImageUrlAsync(string filename);

    /// <summary>
    /// Deletes a product image from storage
    /// </summary>
    Task<bool> DeleteImageAsync(string filename);

    /// <summary>
    /// Checks if a product image exists in storage
    /// </summary>
    Task<bool> ImageExistsAsync(string filename);
}
