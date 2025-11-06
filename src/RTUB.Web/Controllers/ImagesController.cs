using Microsoft.AspNetCore.Mvc;
using RTUB.Application.Interfaces;
using System.Security.Cryptography;

namespace RTUB.Controllers;

[ApiController]
[Route("api/images")]
public class ImagesController : ControllerBase
{
    private readonly IImageService _imageService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly int _cacheDurationInSeconds;

    public ImagesController(IImageService imageService, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _imageService = imageService;
        _environment = environment;
        _configuration = configuration;
        _cacheDurationInSeconds = configuration.GetValue<int>("ImageCaching:CacheDurationInSeconds", 86400); // Default 24 hours
    }

    /// <summary>
    /// Generates an ETag from image data using SHA-256 for better security
    /// </summary>
    private static string GenerateETag(byte[] data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Returns a file result with caching headers or 304 Not Modified if ETag matches
    /// </summary>
    private IActionResult FileWithCache(byte[] data, string contentType, DateTime? lastModified = null, bool immutable = false)
    {
        var etag = GenerateETag(data);
        
        // Check if client has cached version with matching ETag
        if (Request.Headers.IfNoneMatch.Any(tag => tag == etag))
        {
            return StatusCode(304); // Not Modified
        }

        // Check If-Modified-Since for conditional requests
        if (lastModified.HasValue && Request.Headers.IfModifiedSince.Any())
        {
            if (DateTime.TryParse(Request.Headers.IfModifiedSince.ToString(), out var ifModifiedSince))
            {
                if (lastModified.Value <= ifModifiedSince.AddSeconds(1)) // Add 1 second tolerance for precision
                {
                    return StatusCode(304); // Not Modified
                }
            }
        }

        Response.Headers.Append("ETag", etag);
        
        // Set Last-Modified header if provided
        if (lastModified.HasValue)
        {
            Response.Headers.Append("Last-Modified", lastModified.Value.ToUniversalTime().ToString("R"));
        }
        
        // Use immutable caching when version is in URL (cache busting)
        if (immutable)
        {
            Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");
        }
        else
        {
            // Use 'no-cache' which means "cache it, but always validate with server using ETag"
            // This ensures image updates are seen immediately while still getting 304 responses
            // for unchanged images (saving bandwidth). Alternative: use shorter max-age.
            Response.Headers.Append("Cache-Control", "public, no-cache");
        }
        
        return File(data, contentType);
    }

    [HttpGet("event/{id}")]
    public async Task<IActionResult> GetEventImage(int id)
    {
        var imageData = await _imageService.GetEventImageAsync(id);

        if (imageData == null)
        {
            return NotFound();
        }

        return FileWithCache(imageData.Value.Data, imageData.Value.ContentType);
    }

    [HttpGet("slideshow/{id}")]
    public async Task<IActionResult> GetSlideshowImage(int id)
    {
        var imageData = await _imageService.GetSlideshowImageAsync(id);

        if (imageData == null)
        {
            return NotFound();
        }

        return FileWithCache(imageData.Value.Data, imageData.Value.ContentType);
    }

    [HttpGet("album/{id}")]
    public async Task<IActionResult> GetAlbumImage(int id)
    {
        var imageData = await _imageService.GetAlbumImageAsync(id);

        if (imageData == null)
        {
            return NotFound();
        }

        return FileWithCache(imageData.Value.Data, imageData.Value.ContentType);
    }

    [HttpGet("profile/{id}")]
    public async Task<IActionResult> GetProfileImage(string id)
    {
        var imageData = await _imageService.GetProfileImageAsync(id);

        // Check if version parameter is present for immutable caching
        var hasVersion = Request.Query.ContainsKey("v");

        // If user has a profile picture, return it with caching
        if (imageData != null)
        {
            // Use immutable caching when version is specified in URL
            return FileWithCache(imageData.Value.Data, imageData.Value.ContentType, DateTime.UtcNow, immutable: hasVersion);
        }

        // Otherwise, return the default profile picture (WebP for better performance)
        var defaultImagePath = Path.Combine(_environment.WebRootPath, "images", "profile-pic.webp");

        if (!System.IO.File.Exists(defaultImagePath))
        {
            return NotFound("Default profile picture not found");
        }

        var imageBytes = await System.IO.File.ReadAllBytesAsync(defaultImagePath);
        // Default image can use immutable caching since it rarely changes
        var defaultImageLastModified = System.IO.File.GetLastWriteTimeUtc(defaultImagePath);
        return FileWithCache(imageBytes, "image/webp", defaultImageLastModified, immutable: true);
    }

    [HttpGet("instrument/{id}")]
    public async Task<IActionResult> GetInstrumentImage(int id)
    {
        var imageData = await _imageService.GetInstrumentImageAsync(id);

        if (imageData == null)
        {
            return NotFound();
        }

        return FileWithCache(imageData.Value.Data, imageData.Value.ContentType);
    }

    [HttpGet("product/{id}")]
    public async Task<IActionResult> GetProductImage(int id)
    {
        var imageData = await _imageService.GetProductImageAsync(id);

        if (imageData == null)
        {
            return NotFound();
        }

        return FileWithCache(imageData.Value.Data, imageData.Value.ContentType);
    }
}
