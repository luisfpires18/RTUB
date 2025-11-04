using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for retrieving image data from entities
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Gets event image data
    /// </summary>
    Task<(byte[] Data, string ContentType)?> GetEventImageAsync(int eventId);

    /// <summary>
    /// Gets slideshow image data
    /// </summary>
    Task<(byte[] Data, string ContentType)?> GetSlideshowImageAsync(int slideshowId);

    /// <summary>
    /// Gets album cover image data
    /// </summary>
    Task<(byte[] Data, string ContentType)?> GetAlbumImageAsync(int albumId);

    /// <summary>
    /// Gets user profile picture data
    /// </summary>
    Task<(byte[] Data, string ContentType)?> GetProfileImageAsync(string userId);

    /// <summary>
    /// Gets instrument image data
    /// </summary>
    Task<(byte[] Data, string ContentType)?> GetInstrumentImageAsync(int instrumentId);

    /// <summary>
    /// Gets product image data
    /// </summary>
    Task<(byte[] Data, string ContentType)?> GetProductImageAsync(int productId);

    /// <summary>
    /// Invalidates cached event image
    /// </summary>
    void InvalidateEventImageCache(int eventId);

    /// <summary>
    /// Invalidates cached slideshow image
    /// </summary>
    void InvalidateSlideshowImageCache(int slideshowId);

    /// <summary>
    /// Invalidates cached album image
    /// </summary>
    void InvalidateAlbumImageCache(int albumId);

    /// <summary>
    /// Invalidates cached profile image
    /// </summary>
    void InvalidateProfileImageCache(string userId);

    /// <summary>
    /// Invalidates cached instrument image
    /// </summary>
    void InvalidateInstrumentImageCache(int instrumentId);

    /// <summary>
    /// Invalidates cached product image
    /// </summary>
    void InvalidateProductImageCache(int productId);
}
