using Microsoft.AspNetCore.Http;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Interface for instrument image storage service
/// </summary>
public interface IInstrumentStorageService
{
    /// <summary>
    /// Uploads an instrument image from an IFormFile
    /// </summary>
    Task<string> UploadImageAsync(IFormFile file, int instrumentId);

    /// <summary>
    /// Uploads an instrument image from a byte array
    /// </summary>
    Task<string> UploadImageAsync(byte[] imageData, int instrumentId, string contentType);

    /// <summary>
    /// Gets the public URL for an instrument image
    /// </summary>
    Task<string?> GetImageUrlAsync(string filename);

    /// <summary>
    /// Deletes an instrument image from storage
    /// </summary>
    Task<bool> DeleteImageAsync(string filename);

    /// <summary>
    /// Checks if an instrument image exists in storage
    /// </summary>
    Task<bool> ImageExistsAsync(string filename);
}
