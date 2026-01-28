using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for caching song URLs (audio, lyrics PDF) with size limits
/// Extracted from Songs.razor to improve separation of concerns
/// Session-based cache (scoped service)
/// </summary>
public class SongUrlCacheService : ISongUrlCacheService
{
    private readonly Dictionary<string, string> _urlCache = new();

    public string? GetCachedUrl(string cacheKey)
    {
        return _urlCache.TryGetValue(cacheKey, out var cachedUrl) ? cachedUrl : null;
    }

    public void CacheUrl(string cacheKey, string url, int maxCacheSize = 100)
    {
        if (string.IsNullOrEmpty(url))
            return;

        // If cache is at limit, remove oldest entry (first key)
        if (_urlCache.Count >= maxCacheSize)
        {
            var firstKey = _urlCache.Keys.First();
            _urlCache.Remove(firstKey);
        }
        _urlCache[cacheKey] = url;
    }

    public void ClearCache()
    {
        _urlCache.Clear();
    }
}
