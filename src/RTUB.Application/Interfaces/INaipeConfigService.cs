namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing naipe configuration image URLs
/// Extracted from NaipesConfig.razor to improve separation of concerns
/// </summary>
public interface INaipeConfigService
{
    /// <summary>
    /// Gets the image URL with cache-busting query parameter if needed
    /// </summary>
    /// <param name="imageSrc">Original image source URL</param>
    /// <param name="refreshTrigger">Refresh trigger value (0 means no refresh needed)</param>
    /// <returns>Image URL with optional refresh parameter</returns>
    string GetImageUrl(string? imageSrc, int refreshTrigger);
}
