using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;


namespace RTUB.Application.Services;

/// <summary>
/// Slideshow service implementation
/// Contains business logic for slideshow operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class SlideshowService : ISlideshowService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageService _imageService;
    private readonly ISlideshowStorageService _slideshowStorageService;

    public SlideshowService(
        ApplicationDbContext context, 
        IImageService imageService,
        ISlideshowStorageService slideshowStorageService)
    {
        _context = context;
        _imageService = imageService;
        _slideshowStorageService = slideshowStorageService;
    }

    public async Task<Slideshow?> GetSlideshowByIdAsync(int id)
    {
        return await _context.Slideshows.FindAsync(id);
    }

    public async Task<IEnumerable<Slideshow>> GetAllSlideshowsAsync()
    {
        var slideshows = await _context.Slideshows.ToListAsync();
        await GeneratePresignedUrlsForSlideshowsAsync(slideshows);
        return slideshows;
    }

    public async Task<IEnumerable<Slideshow>> GetActiveSlideshowsAsync()
    {
        return await _context.Slideshows
            .Where(s => s.IsActive)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<IEnumerable<Slideshow>> GetActiveSlideshowsWithUrlsAsync()
    {
        var slideshows = await _context.Slideshows
            .Where(s => s.IsActive)
            .OrderBy(s => s.Order)
            .ToListAsync();

        await GeneratePresignedUrlsForSlideshowsAsync(slideshows);
        return slideshows;
    }

    /// <summary>
    /// Generates pre-signed URLs for slideshows stored in IDrive S3
    /// </summary>
    private async Task GeneratePresignedUrlsForSlideshowsAsync(IEnumerable<Slideshow> slideshows)
    {
        foreach (var slideshow in slideshows)
        {
            // If ImageUrl contains a filename (not a full URL and not an API path), get the pre-signed URL
            if (!string.IsNullOrEmpty(slideshow.ImageUrl) && 
                !slideshow.ImageUrl.StartsWith("http") && 
                !slideshow.ImageUrl.StartsWith("/"))
            {
                var url = await _slideshowStorageService.GetImageUrlAsync(slideshow.ImageUrl);
                if (!string.IsNullOrEmpty(url))
                {
                    // Temporarily store the generated URL in ImageUrl for rendering
                    slideshow.ImageUrl = url;
                }
                else
                {
                    // File not found in S3, clear ImageUrl so GetImageSource() can fall back to ImageData
                    slideshow.ImageUrl = null;
                }
            }
        }
    }

    public async Task<Slideshow> CreateSlideshowAsync(string title, int order, string description = "", int intervalMs = 5000)
    {
        var slideshow = Slideshow.Create(title, order, description, intervalMs);
        _context.Slideshows.Add(slideshow);
        await _context.SaveChangesAsync();
        return slideshow;
    }

    public async Task UpdateSlideshowAsync(int id, string title, string description, int order, int intervalMs)
    {
        var slideshow = await _context.Slideshows.FindAsync(id);
        if (slideshow == null)
            throw new InvalidOperationException($"Slideshow with ID {id} not found");

        slideshow.UpdateDetails(title, description, order, intervalMs);
        _context.Slideshows.Update(slideshow);
        await _context.SaveChangesAsync();
    }

    public async Task SetSlideshowImageAsync(int id, byte[]? imageData, string? contentType, string url = "")
    {
        var slideshow = await _context.Slideshows.FindAsync(id);
        if (slideshow == null)
            throw new InvalidOperationException($"Slideshow with ID {id} not found");

        // If imageData is provided, upload to IDrive S3
        if (imageData != null && !string.IsNullOrEmpty(contentType))
        {
            // Upload to IDrive S3 and get the filename
            var filename = await _slideshowStorageService.UploadImageAsync(imageData, id, contentType);
            
            // Delete old image from S3 if it exists and is not a database-stored image
            if (!string.IsNullOrEmpty(slideshow.ImageUrl) && 
                !slideshow.ImageUrl.StartsWith("http") && 
                !slideshow.ImageUrl.StartsWith("/"))
            {
                await _slideshowStorageService.DeleteImageAsync(slideshow.ImageUrl);
            }
            
            // Store the filename in the ImageUrl field, clear ImageData
            slideshow.SetImage(null, null, filename);
        }
        else
        {
            // If no imageData provided, just update the URL
            slideshow.SetImage(imageData, contentType, url);
        }
        
        _context.Slideshows.Update(slideshow);
        await _context.SaveChangesAsync();
        
        // Invalidate the cached slideshow image so the new image is served immediately
        _imageService.InvalidateSlideshowImageCache(id);
    }

    public async Task ActivateSlideshowAsync(int id)
    {
        var slideshow = await _context.Slideshows.FindAsync(id);
        if (slideshow == null)
            throw new InvalidOperationException($"Slideshow with ID {id} not found");

        slideshow.Activate();
        _context.Slideshows.Update(slideshow);
        await _context.SaveChangesAsync();
    }

    public async Task DeactivateSlideshowAsync(int id)
    {
        var slideshow = await _context.Slideshows.FindAsync(id);
        if (slideshow == null)
            throw new InvalidOperationException($"Slideshow with ID {id} not found");

        slideshow.Deactivate();
        _context.Slideshows.Update(slideshow);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteSlideshowAsync(int id)
    {
        var slideshow = await _context.Slideshows.FindAsync(id);
        if (slideshow == null)
            throw new InvalidOperationException($"Slideshow with ID {id} not found");

        // Delete image from S3 if it exists
        if (!string.IsNullOrEmpty(slideshow.ImageUrl))
        {
            await _slideshowStorageService.DeleteImageAsync(slideshow.ImageUrl);
        }

        _context.Slideshows.Remove(slideshow);
        await _context.SaveChangesAsync();
    }
}
