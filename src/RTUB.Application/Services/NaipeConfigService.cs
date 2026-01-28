using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing naipe configuration image URLs
/// Extracted from NaipesConfig.razor to improve separation of concerns
/// </summary>
public class NaipeConfigService : INaipeConfigService
{
    /// <summary>
    /// Gets the image URL with cache-busting query parameter if needed
    /// </summary>
    /// <param name="imageSrc">Original image source URL</param>
    /// <param name="refreshTrigger">Refresh trigger value (0 means no refresh needed)</param>
    /// <returns>Image URL with optional refresh parameter</returns>
    public string GetImageUrl(string? imageSrc, int refreshTrigger)
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
