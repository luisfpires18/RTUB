using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Slideshow operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface ISlideshowService
{
    Task<Slideshow?> GetSlideshowByIdAsync(int id);
    Task<IEnumerable<Slideshow>> GetAllSlideshowsAsync();
    Task<IEnumerable<Slideshow>> GetActiveSlideshowsAsync();
    Task<Slideshow> CreateSlideshowAsync(string title, int order, string description = "", int intervalMs = 5000);
    Task UpdateSlideshowAsync(int id, string title, string description, int order, int intervalMs);
    Task SetSlideshowImageAsync(int id, Stream imageStream, string fileName, string contentType);
    Task ActivateSlideshowAsync(int id);
    Task DeactivateSlideshowAsync(int id);
    Task DeleteSlideshowAsync(int id);
}
