using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for handling album image URLs
/// Extracted from Albums.razor to improve separation of concerns
/// </summary>
public class AlbumImageService : IAlbumImageService
{
    public string GetAlbumImageUrl(string imageSrc, int refreshTrigger = 0)
    {
        if (string.IsNullOrEmpty(imageSrc)) return string.Empty;

        // Add refresh trigger only when an image has been updated
        // This forces browser to re-fetch after edits while maintaining ETag caching
        if (refreshTrigger > 0 && imageSrc.StartsWith("/api/images/"))
        {
            return $"{imageSrc}?r={refreshTrigger}";
        }

        return imageSrc;
    }
}
