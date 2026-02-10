namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing item type (weapons/drinks) image storage in Cloudflare R2.
/// Handles upload and deletion of configuration images for weapon and drink types.
/// </summary>
public interface IItemTypeMediaStorageService
{
    /// <summary>
    /// Uploads an image for an item type configuration
    /// </summary>
    /// <param name="fileStream">Stream containing the image data</param>
    /// <param name="fileName">Original filename</param>
    /// <param name="contentType">MIME content type</param>
    /// <param name="itemTypeKey">Type key (e.g., "SwordOneHand", "Cerveja")</param>
    /// <returns>Public URL of the uploaded image</returns>
    Task<string> UploadImageAsync(Stream fileStream, string fileName, string contentType, string itemTypeKey);

    /// <summary>
    /// Deletes an item type image from R2 storage
    /// </summary>
    /// <param name="imageUrl">Public URL of the image to delete</param>
    Task DeleteImageAsync(string imageUrl);

    /// <summary>
    /// Checks if an item type image exists in R2 storage
    /// </summary>
    /// <param name="imageUrl">Public URL of the image to check</param>
    /// <returns>True if the image exists</returns>
    Task<bool> ImageExistsAsync(string imageUrl);
}
