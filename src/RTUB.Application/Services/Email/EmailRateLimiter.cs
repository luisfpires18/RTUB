using Microsoft.Extensions.Caching.Memory;

namespace RTUB.Application.Services.Email;

/// <summary>
/// Handles rate limiting for email sending to prevent duplicate sends
/// Follows Single Responsibility Principle - only handles caching and rate limiting logic
/// </summary>
public class EmailRateLimiter
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultRateLimitDuration = TimeSpan.FromMinutes(5);

    public EmailRateLimiter(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Checks if an email should be rate-limited based on the cache key
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the email operation</param>
    /// <returns>True if the email should be rate-limited (already sent recently), false otherwise</returns>
    public bool ShouldRateLimit(string cacheKey)
    {
        return ShouldRateLimit(cacheKey, DefaultRateLimitDuration);
    }

    /// <summary>
    /// Checks if an email should be rate-limited based on the cache key with custom duration
    /// </summary>
    /// <param name="cacheKey">Unique identifier for the email operation</param>
    /// <param name="duration">How long to cache the rate limit</param>
    /// <returns>True if the email should be rate-limited (already sent recently), false otherwise</returns>
    public bool ShouldRateLimit(string cacheKey, TimeSpan duration)
    {
        if (string.IsNullOrEmpty(cacheKey))
        {
            throw new ArgumentException("Cache key cannot be null or empty", nameof(cacheKey));
        }

        if (_cache.TryGetValue<bool>(cacheKey, out _))
        {
            return true; // Already sent recently
        }

        // Mark as sent for the specified duration
        _cache.Set(cacheKey, true, duration);
        return false;
    }

    /// <summary>
    /// Clears the rate limit for a specific cache key (for testing purposes)
    /// </summary>
    public void ClearRateLimit(string cacheKey)
    {
        if (!string.IsNullOrEmpty(cacheKey))
        {
            _cache.Remove(cacheKey);
        }
    }
}
