using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Utilities;


namespace RTUB.Application.Services;

/// <summary>
/// Slideshow service implementation
/// Contains business logic for slideshow operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class SlideshowService : ISlideshowService
{
    private readonly ApplicationDbContext _context;
    private readonly IImageStorageService _imageStorageService;

    public SlideshowService(ApplicationDbContext context, IImageStorageService imageStorageService)
    {
        _context = context;
        _imageStorageService = imageStorageService;
    }

    public async Task<Slideshow?> GetSlideshowByIdAsync(int id)
    {
        return await _context.Slideshows.FindAsync(id);
    }

    public async Task<IEnumerable<Slideshow>> GetAllSlideshowsAsync()
    {
        return await _context.Slideshows.ToListAsync();
    }

    public async Task<IEnumerable<Slideshow>> GetActiveSlideshowsAsync()
    {
        return await _context.Slideshows
            .Where(s => s.IsActive)
            .OrderBy(s => s.Order)
            .ToListAsync();
    }

    public async Task<Slideshow> CreateSlideshowAsync(string title, int order, string description = "", int intervalMs = 5000)
    {
        var slideshow = Slideshow.Create(title, order, description, intervalMs);
        _context.Slideshows.Add(slideshow);
        await _context.SaveChangesAsync();
        return slideshow;
    }

    public async Task UpdateSlideshowAsync(int id, string title, string description, int order, int intervalMs, bool isActive)
    {
        var slideshow = await _context.Slideshows.FindAsync(id);
        if (slideshow == null)
            throw new InvalidOperationException($"Slideshow with ID {id} not found");

        slideshow.UpdateDetails(title, description, order, intervalMs);
        
        // Update active state
        if (isActive && !slideshow.IsActive)
        {
            slideshow.Activate();
        }
        else if (!isActive && slideshow.IsActive)
        {
            slideshow.Deactivate();
        }
        
        _context.Slideshows.Update(slideshow);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateSlideshowWithImageAsync(int id, string title, string description, int order, int intervalMs, bool isActive, Stream imageStream, string fileName, string contentType)
    {
        var slideshow = await _context.Slideshows.FindAsync(id);
        if (slideshow == null)
            throw new InvalidOperationException($"Slideshow with ID {id} not found");

        // Update slideshow details
        slideshow.UpdateDetails(title, description, order, intervalMs);
        
        // Update active state
        if (isActive && !slideshow.IsActive)
        {
            slideshow.Activate();
        }
        else if (!isActive && slideshow.IsActive)
        {
            slideshow.Deactivate();
        }

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(slideshow.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(slideshow.ImageUrl);
        }

        // Upload new image to Cloudflare R2 using normalized slideshow title
        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(title);
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "slideshows", normalizedName);
        slideshow.SetImage(imageUrl);
        
        _context.Slideshows.Update(slideshow);
        await _context.SaveChangesAsync();
    }

    public async Task SetSlideshowImageAsync(int id, Stream imageStream, string fileName, string contentType)
    {
        var slideshow = await _context.Slideshows.FindAsync(id);
        if (slideshow == null)
            throw new InvalidOperationException($"Slideshow with ID {id} not found");

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(slideshow.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(slideshow.ImageUrl);
        }

        // Upload new image to Cloudflare R2 using normalized slideshow title
        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(slideshow.Title);
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "slideshows", normalizedName);
        slideshow.SetImage(imageUrl);
        
        _context.Slideshows.Update(slideshow);
        await _context.SaveChangesAsync();
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

        // Delete associated image from R2 storage if it exists
        if (!string.IsNullOrEmpty(slideshow.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(slideshow.ImageUrl);
        }

        _context.Slideshows.Remove(slideshow);
        await _context.SaveChangesAsync();
    }
}
