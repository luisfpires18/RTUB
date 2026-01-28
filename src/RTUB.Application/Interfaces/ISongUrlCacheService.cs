namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for caching song URLs (audio, lyrics PDF) with size limits
/// Extracted from Songs.razor to improve separation of concerns
/// </summary>
public interface ISongUrlCacheService
{
    /// <summary>
    /// Gets a cached URL if available, otherwise returns null
    /// </summary>
    /// <param name="cacheKey">The cache key to look up</param>
    /// <returns>Cached URL or null if not found</returns>
    string? GetCachedUrl(string cacheKey);

    /// <summary>
    /// Caches a URL with automatic size limit management
    /// </summary>
    /// <param name="cacheKey">The cache key</param>
    /// <param name="url">The URL to cache</param>
    /// <param name="maxCacheSize">Maximum number of entries in cache (default: 100)</param>
    void CacheUrl(string cacheKey, string url, int maxCacheSize = 100);

    /// <summary>
    /// Clears all cached URLs
    /// </summary>
    void ClearCache();
}
