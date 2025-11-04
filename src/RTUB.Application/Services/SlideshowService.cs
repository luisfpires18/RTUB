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

    public SlideshowService(ApplicationDbContext context, IImageService imageService)
    {
        _context = context;
        _imageService = imageService;
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

        slideshow.SetImage(imageData, contentType, url);
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

        _context.Slideshows.Remove(slideshow);
        await _context.SaveChangesAsync();
    }
}
