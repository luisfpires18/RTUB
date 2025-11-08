using RTUB.Application.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Data;
using RTUB.Core.Entities;


namespace RTUB.Application.Services;

/// <summary>
/// Service for retrieving image data from entities with memory caching
/// </summary>
public class ImageService : IImageService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    public ImageService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IMemoryCache cache)
    {
        _context = context;
        _userManager = userManager;
        _cache = cache;
        
        // Cache images for 1 hour in memory (reduces database queries)
        _cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
            SlidingExpiration = TimeSpan.FromMinutes(30)
        };
    }

    /// <inheritdoc/>
    public async Task<(byte[] Data, string ContentType)?> GetEventImageAsync(int eventId)
    {
        var cacheKey = $"event-image-{eventId}";
        
        if (_cache.TryGetValue<(byte[] Data, string ContentType)>(cacheKey, out var cachedImage))
        {
            return cachedImage;
        }
        
        // Event images are now stored in S3, not in the database
        // This method returns null for events, as images are served directly from S3
        return null;
    }

    /// <inheritdoc/>
    public async Task<(byte[] Data, string ContentType)?> GetAlbumImageAsync(int albumId)
    {
        // Albums now use S3 storage - images are served directly from S3 URLs
        // This method returns null to indicate no blob data is available
        // The album's ImageUrl should be used instead
        return await Task.FromResult<(byte[] Data, string ContentType)?>(null);
    }

    /// <inheritdoc/>
    public async Task<(byte[] Data, string ContentType)?> GetProfileImageAsync(string userId)
    {
        // Profile pictures now use S3 storage - images are served directly from S3 URLs
        // This method returns null to indicate no blob data is available
        return await Task.FromResult<(byte[] Data, string ContentType)?>(null);
    }

    /// <inheritdoc/>
    public async Task<(byte[] Data, string ContentType)?> GetInstrumentImageAsync(int instrumentId)
    {
        // Instruments now use S3 storage - images are served directly from S3 URLs
        // This method returns null to indicate no blob data is available
        // The instrument's ImageUrl should be used instead
        return await Task.FromResult<(byte[] Data, string ContentType)?>(null);
    }

    /// <inheritdoc/>
    public async Task<(byte[] Data, string ContentType)?> GetProductImageAsync(int productId)
    {
        // Products now use S3 storage with direct URLs - no database image data
        // Return null to indicate image should be served from ImageUrl property
        return await Task.FromResult<(byte[] Data, string ContentType)?>(null);
    }

    /// <inheritdoc/>
    public void InvalidateEventImageCache(int eventId)
    {
        var cacheKey = $"event-image-{eventId}";
        _cache.Remove(cacheKey);
    }

    /// <inheritdoc/>
    public void InvalidateSlideshowImageCache(int slideshowId)
    {
        var cacheKey = $"slideshow-image-{slideshowId}";
        _cache.Remove(cacheKey);
    }

    /// <inheritdoc/>
    public void InvalidateAlbumImageCache(int albumId)
    {
        var cacheKey = $"album-image-{albumId}";
        _cache.Remove(cacheKey);
    }

    /// <inheritdoc/>
    public void InvalidateProfileImageCache(string userId)
    {
        var cacheKey = $"profile-image-{userId}";
        _cache.Remove(cacheKey);
    }

    /// <inheritdoc/>
    public void InvalidateInstrumentImageCache(int instrumentId)
    {
        var cacheKey = $"instrument-image-{instrumentId}";
        _cache.Remove(cacheKey);
    }

    /// <inheritdoc/>
    public void InvalidateProductImageCache(int productId)
    {
        var cacheKey = $"product-image-{productId}";
        _cache.Remove(cacheKey);
    }
}
