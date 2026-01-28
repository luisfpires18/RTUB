namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for handling album image URLs
/// Extracted from Albums.razor to improve separation of concerns
/// </summary>
public interface IAlbumImageService
{
    /// <summary>
    /// Gets the album image URL with optional refresh trigger for cache busting
    /// </summary>
    /// <param name="imageSrc">Original image source URL</param>
    /// <param name="refreshTrigger">Refresh trigger value (0 = no refresh)</param>
    /// <returns>Image URL with refresh parameter if applicable</returns>
    string GetAlbumImageUrl(string imageSrc, int refreshTrigger = 0);
}
