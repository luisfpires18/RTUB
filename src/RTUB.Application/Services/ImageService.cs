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
        var cacheKey = $"album-image-{albumId}";
        
        if (_cache.TryGetValue<(byte[] Data, string ContentType)>(cacheKey, out var cachedImage))
        {
            return cachedImage;
        }
        
        var album = await _context.Albums.FindAsync(albumId);
        
        if (album?.CoverImageData != null && !string.IsNullOrEmpty(album.CoverImageContentType))
        {
            var result = (album.CoverImageData, album.CoverImageContentType);
            _cache.Set(cacheKey, result, _cacheOptions);
            return result;
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<(byte[] Data, string ContentType)?> GetProfileImageAsync(string userId)
    {
        var cacheKey = $"profile-image-{userId}";
        
        if (_cache.TryGetValue<(byte[] Data, string ContentType)>(cacheKey, out var cachedImage))
        {
            return cachedImage;
        }
        
        var user = await _userManager.FindByIdAsync(userId);
        
        if (user?.ProfilePictureData != null && !string.IsNullOrEmpty(user.ProfilePictureContentType))
        {
            var result = (user.ProfilePictureData, user.ProfilePictureContentType);
            _cache.Set(cacheKey, result, _cacheOptions);
            return result;
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task<(byte[] Data, string ContentType)?> GetInstrumentImageAsync(int instrumentId)
    {
        var cacheKey = $"instrument-image-{instrumentId}";
        
        if (_cache.TryGetValue<(byte[] Data, string ContentType)>(cacheKey, out var cachedImage))
        {
            return cachedImage;
        }
        
        var instrument = await _context.Instruments.FindAsync(instrumentId);
        
        if (instrument?.ImageData != null && !string.IsNullOrEmpty(instrument.ImageContentType))
        {
            var result = (instrument.ImageData, instrument.ImageContentType);
            _cache.Set(cacheKey, result, _cacheOptions);
            return result;
        }
        
        return null;
    }

    /// <inheritdoc/>
    public async Task<(byte[] Data, string ContentType)?> GetProductImageAsync(int productId)
    {
        var cacheKey = $"product-image-{productId}";
        
        if (_cache.TryGetValue<(byte[] Data, string ContentType)>(cacheKey, out var cachedImage))
        {
            return cachedImage;
        }
        
        var product = await _context.Products.FindAsync(productId);
        
        if (product?.ImageData != null && !string.IsNullOrEmpty(product.ImageContentType))
        {
            var result = (product.ImageData, product.ImageContentType);
            _cache.Set(cacheKey, result, _cacheOptions);
            return result;
        }
        
        return null;
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
