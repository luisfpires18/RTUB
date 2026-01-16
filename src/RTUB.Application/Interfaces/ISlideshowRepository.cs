using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Slideshow entity
/// Provides data access operations for slideshows
/// </summary>
public interface ISlideshowRepository : IRepository<Slideshow>
{
    /// <summary>
    /// Gets all active slideshows ordered by display order
    /// </summary>
    Task<IEnumerable<Slideshow>> GetActiveSlideshowsAsync();

    /// <summary>
    /// Gets all active public slideshows (not exclusive) ordered by display order
    /// </summary>
    Task<IEnumerable<Slideshow>> GetActivePublicSlideshowsAsync();
}
