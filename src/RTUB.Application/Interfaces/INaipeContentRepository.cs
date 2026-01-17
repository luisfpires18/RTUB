using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for NaipeContent entity
/// Provides data access operations for naipe content
/// </summary>
public interface INaipeContentRepository : IRepository<NaipeContent>
{
    /// <summary>
    /// Gets all content for a specific instrument type with details
    /// </summary>
    Task<IEnumerable<NaipeContent>> GetContentByInstrumentTypeAsync(InstrumentType instrumentType);

    /// <summary>
    /// Gets all content with details (includes user, comments, play counts)
    /// </summary>
    Task<IEnumerable<NaipeContent>> GetAllContentWithDetailsAsync();

    /// <summary>
    /// Gets content by ID with all related data
    /// </summary>
    Task<NaipeContent?> GetByIdWithDetailsAsync(int id);

    /// <summary>
    /// Gets play count for a specific content
    /// </summary>
    Task<int> GetPlayCountAsync(int contentId);
}
