using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Utilities;


namespace RTUB.Application.Services;

/// <summary>
/// Slideshow service implementation using Repository pattern
/// Contains business logic for slideshow operations
/// Follows Single Responsibility and Dependency Inversion principles
/// Now depends on ISlideshowRepository abstraction instead of concrete DbContext
/// </summary>
public class SlideshowService : ISlideshowService
{
    private readonly ISlideshowRepository _slideshowRepository;
    private readonly IImageStorageService _imageStorageService;

    public SlideshowService(ISlideshowRepository slideshowRepository, IImageStorageService imageStorageService)
    {
        _slideshowRepository = slideshowRepository;
        _imageStorageService = imageStorageService;
    }

    public async Task<Slideshow?> GetSlideshowByIdAsync(int id)
    {
        return await _slideshowRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<Slideshow>> GetAllSlideshowsAsync()
    {
        return await _slideshowRepository.GetAllAsync();
    }

    public async Task<IEnumerable<Slideshow>> GetActiveSlideshowsAsync()
    {
        return await _slideshowRepository.GetActiveSlideshowsAsync();
    }

    public async Task<IEnumerable<Slideshow>> GetActivePublicSlideshowsAsync()
    {
        return await _slideshowRepository.GetActivePublicSlideshowsAsync();
    }

    public async Task<Slideshow> CreateSlideshowAsync(string title, int order, string description = "", int intervalMs = 5000, string? imageUrl = null)
    {
        var slideshow = Slideshow.Create(title, order, description, intervalMs);
        if (!string.IsNullOrEmpty(imageUrl))
        {
            slideshow.SetImage(imageUrl);
        }
        return await _slideshowRepository.AddAsync(slideshow);
    }

    public async Task UpdateSlideshowAsync(int id, string title, string description, int order, int intervalMs, bool isActive, bool isExclusive)
    {
        var slideshow = await _slideshowRepository.GetByIdAsync(id);
        if (slideshow == null)
            throw new EntityNotFoundException(nameof(Slideshow), id);

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

        // Update exclusive state
        slideshow.SetExclusive(isExclusive);

        await _slideshowRepository.UpdateAsync(slideshow);
    }

    public async Task UpdateSlideshowWithImageAsync(int id, string title, string description, int order, int intervalMs, bool isActive, bool isExclusive, Stream imageStream, string fileName, string contentType)
    {
        var slideshow = await _slideshowRepository.GetByIdAsync(id);
        if (slideshow == null)
            throw new EntityNotFoundException(nameof(Slideshow), id);

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

        // Update exclusive state
        slideshow.SetExclusive(isExclusive);

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(slideshow.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(slideshow.ImageUrl);
        }

        // Upload new image to Cloudflare R2 using normalized slideshow title
        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(title);
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "slideshows", normalizedName);
        slideshow.SetImage(imageUrl);

        await _slideshowRepository.UpdateAsync(slideshow);
    }

    public async Task SetSlideshowImageAsync(int id, Stream imageStream, string fileName, string contentType)
    {
        var slideshow = await _slideshowRepository.GetByIdAsync(id);
        if (slideshow == null)
            throw new EntityNotFoundException(nameof(Slideshow), id);

        // Delete old image if it exists
        if (!string.IsNullOrEmpty(slideshow.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(slideshow.ImageUrl);
        }

        // Upload new image to Cloudflare R2 using normalized slideshow title
        var normalizedName = S3KeyNormalizer.NormalizeForS3Key(slideshow.Title);
        var imageUrl = await _imageStorageService.UploadImageAsync(imageStream, fileName, contentType, "slideshows", normalizedName);
        slideshow.SetImage(imageUrl);

        await _slideshowRepository.UpdateAsync(slideshow);
    }

    public async Task ActivateSlideshowAsync(int id)
    {
        var slideshow = await _slideshowRepository.GetByIdAsync(id);
        if (slideshow == null)
            throw new EntityNotFoundException(nameof(Slideshow), id);

        slideshow.Activate();
        await _slideshowRepository.UpdateAsync(slideshow);
    }

    public async Task DeactivateSlideshowAsync(int id)
    {
        var slideshow = await _slideshowRepository.GetByIdAsync(id);
        if (slideshow == null)
            throw new EntityNotFoundException(nameof(Slideshow), id);

        slideshow.Deactivate();
        await _slideshowRepository.UpdateAsync(slideshow);
    }

    public async Task DeleteSlideshowAsync(int id)
    {
        var slideshow = await _slideshowRepository.GetByIdAsync(id);
        if (slideshow == null)
            throw new EntityNotFoundException(nameof(Slideshow), id);

        // Delete associated image from R2 storage if it exists
        if (!string.IsNullOrEmpty(slideshow.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(slideshow.ImageUrl);
        }

        await _slideshowRepository.DeleteAsync(slideshow);
    }
}
