using Microsoft.AspNetCore.Mvc;
using RTUB.Application.Interfaces;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using RTUB.Core.Entities;

namespace RTUB.Controllers;

[ApiController]
[Route("api/images")]
public class ImagesController : ControllerBase
{
    private readonly IImageService _imageService;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly int _cacheDurationInSeconds;
    private readonly IProfileStorageService _profileStorageService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ImagesController(
        IImageService imageService, 
        IWebHostEnvironment environment, 
        IConfiguration configuration,
        IProfileStorageService profileStorageService,
        UserManager<ApplicationUser> userManager)
    {
        _imageService = imageService;
        _environment = environment;
        _configuration = configuration;
        _cacheDurationInSeconds = configuration.GetValue<int>("ImageCaching:CacheDurationInSeconds", 86400); // Default 24 hours
        _profileStorageService = profileStorageService;
        _userManager = userManager;
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
    private IActionResult FileWithCache(byte[] data, string contentType)
    {
        var etag = GenerateETag(data);
        
        // Check if client has cached version with matching ETag
        if (Request.Headers.IfNoneMatch.Any(tag => tag == etag))
        {
            return StatusCode(304); // Not Modified
        }

        Response.Headers.Append("ETag", etag);
        // Use 'no-cache' which means "cache it, but always validate with server using ETag"
        // This ensures image updates are seen immediately while still getting 304 responses
        // for unchanged images (saving bandwidth). Alternative: use shorter max-age.
        Response.Headers.Append("Cache-Control", "public, no-cache");
        
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
        // Check if user has an S3-stored profile picture
        var user = await _userManager.FindByIdAsync(id);
        if (user != null && !string.IsNullOrEmpty(user.ImageUrl))
        {
            // Get pre-signed URL from S3
            var s3Url = await _profileStorageService.GetImageUrlAsync(user.ImageUrl);
            if (!string.IsNullOrEmpty(s3Url))
            {
                // Redirect to S3 URL
                return Redirect(s3Url);
            }
        }

        // Fall back to database-stored image
        var imageData = await _imageService.GetProfileImageAsync(id);

        // If user has a profile picture, return it with caching
        if (imageData != null)
        {
            return FileWithCache(imageData.Value.Data, imageData.Value.ContentType);
        }

        // Otherwise, return the default profile picture (WebP for better performance)
        var defaultImagePath = Path.Combine(_environment.WebRootPath, "images", "profile-pic.webp");

        if (!System.IO.File.Exists(defaultImagePath))
        {
            return NotFound("Default profile picture not found");
        }

        var imageBytes = await System.IO.File.ReadAllBytesAsync(defaultImagePath);
        return FileWithCache(imageBytes, "image/webp");
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
