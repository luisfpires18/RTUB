using Microsoft.EntityFrameworkCore;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;


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

    /// <summary>
    /// Initializes a new instance of the SlideshowService
    /// </summary>
    /// <param name="slideshowRepository">Repository for slideshow operations</param>
    /// <param name="imageStorageService">Service for image storage operations</param>
    public SlideshowService(ISlideshowRepository slideshowRepository, IImageStorageService imageStorageService)
    {
        _slideshowRepository = slideshowRepository;
        _imageStorageService = imageStorageService;
    }

    /// <summary>
    /// Gets a slideshow by its ID
    /// </summary>
    /// <param name="id">The ID of the slideshow to retrieve</param>
    /// <returns>The slideshow if found, null otherwise</returns>
    public async Task<Slideshow?> GetSlideshowByIdAsync(int id)
    {
        return await _slideshowRepository.GetByIdAsync(id);
    }

    /// <summary>
    /// Gets all slideshows
    /// </summary>
    /// <returns>Collection of all slideshows</returns>
    public async Task<IEnumerable<Slideshow>> GetAllSlideshowsAsync()
    {
        return await _slideshowRepository.GetAllAsync();
    }

    /// <summary>
    /// Gets all active slideshows
    /// </summary>
    /// <returns>Collection of active slideshows</returns>
    public async Task<IEnumerable<Slideshow>> GetActiveSlideshowsAsync()
    {
        return await _slideshowRepository.GetActiveSlideshowsAsync();
    }

    /// <summary>
    /// Gets all active public slideshows
    /// </summary>
    /// <returns>Collection of active public slideshows</returns>
    public async Task<IEnumerable<Slideshow>> GetActivePublicSlideshowsAsync()
    {
        return await _slideshowRepository.GetActivePublicSlideshowsAsync();
    }

    /// <summary>
    /// Creates a new slideshow
    /// </summary>
    /// <param name="title">The title of the slideshow</param>
    /// <param name="order">The display order</param>
    /// <param name="description">Optional description</param>
    /// <param name="intervalMs">The interval between slides in milliseconds (default: 5000)</param>
    /// <param name="imageUrl">Optional image URL</param>
    /// <returns>The created slideshow</returns>
    public async Task<Slideshow> CreateSlideshowAsync(string title, int order, string description = "", int intervalMs = 5000, string? imageUrl = null)
    {
        var slideshow = Slideshow.Create(title, order, description, intervalMs);
        if (!string.IsNullOrEmpty(imageUrl))
        {
            slideshow.SetImage(imageUrl);
        }
        return await _slideshowRepository.AddAsync(slideshow);
    }

    /// <summary>
    /// Updates an existing slideshow
    /// </summary>
    /// <param name="id">The ID of the slideshow to update</param>
    /// <param name="title">The new title</param>
    /// <param name="description">The new description</param>
    /// <param name="order">The new display order</param>
    /// <param name="intervalMs">The new interval between slides in milliseconds</param>
    /// <param name="isActive">Whether the slideshow is active</param>
    /// <param name="isExclusive">Whether the slideshow is exclusive</param>
    /// <exception cref="EntityNotFoundException">Thrown when the slideshow is not found</exception>
    public async Task UpdateSlideshowAsync(int id, string title, string description, int order, int intervalMs, bool isActive, bool isExclusive)
    {
        var slideshow = await _slideshowRepository.GetByIdOrThrowAsync(id);

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

    /// <summary>
    /// Updates a slideshow and uploads a new image
    /// </summary>
    /// <param name="id">The ID of the slideshow to update</param>
    /// <param name="title">The new title</param>
    /// <param name="description">The new description</param>
    /// <param name="order">The new display order</param>
    /// <param name="intervalMs">The new interval between slides in milliseconds</param>
    /// <param name="isActive">Whether the slideshow is active</param>
    /// <param name="isExclusive">Whether the slideshow is exclusive</param>
    /// <param name="imageStream">The image stream to upload</param>
    /// <param name="fileName">The name of the image file</param>
    /// <param name="contentType">The content type of the image</param>
    /// <exception cref="EntityNotFoundException">Thrown when the slideshow is not found</exception>
    public async Task UpdateSlideshowWithImageAsync(int id, string title, string description, int order, int intervalMs, bool isActive, bool isExclusive, Stream imageStream, string fileName, string contentType)
    {
        var slideshow = await _slideshowRepository.GetByIdOrThrowAsync(id);

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

    /// <summary>
    /// Sets or updates the image for a slideshow
    /// </summary>
    /// <param name="id">The ID of the slideshow</param>
    /// <param name="imageStream">The image stream to upload</param>
    /// <param name="fileName">The name of the image file</param>
    /// <param name="contentType">The content type of the image</param>
    /// <exception cref="EntityNotFoundException">Thrown when the slideshow is not found</exception>
    public async Task SetSlideshowImageAsync(int id, Stream imageStream, string fileName, string contentType)
    {
        var slideshow = await _slideshowRepository.GetByIdOrThrowAsync(id);

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

    /// <summary>
    /// Activates a slideshow
    /// </summary>
    /// <param name="id">The ID of the slideshow to activate</param>
    /// <exception cref="EntityNotFoundException">Thrown when the slideshow is not found</exception>
    public async Task ActivateSlideshowAsync(int id)
    {
        var slideshow = await _slideshowRepository.GetByIdOrThrowAsync(id);

        slideshow.Activate();
        await _slideshowRepository.UpdateAsync(slideshow);
    }

    /// <summary>
    /// Deactivates a slideshow
    /// </summary>
    /// <param name="id">The ID of the slideshow to deactivate</param>
    /// <exception cref="EntityNotFoundException">Thrown when the slideshow is not found</exception>
    public async Task DeactivateSlideshowAsync(int id)
    {
        var slideshow = await _slideshowRepository.GetByIdOrThrowAsync(id);

        slideshow.Deactivate();
        await _slideshowRepository.UpdateAsync(slideshow);
    }

    /// <summary>
    /// Deletes a slideshow and its associated image
    /// </summary>
    /// <param name="id">The ID of the slideshow to delete</param>
    /// <exception cref="EntityNotFoundException">Thrown when the slideshow is not found</exception>
    public async Task DeleteSlideshowAsync(int id)
    {
        var slideshow = await _slideshowRepository.GetByIdOrThrowAsync(id);

        // Delete associated image from R2 storage if it exists
        if (!string.IsNullOrEmpty(slideshow.ImageUrl))
        {
            await _imageStorageService.DeleteImageAsync(slideshow.ImageUrl);
        }

        await _slideshowRepository.DeleteAsync(slideshow);
    }
}
