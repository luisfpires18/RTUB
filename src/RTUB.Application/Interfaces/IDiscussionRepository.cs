using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for Discussion entity
/// </summary>
public interface IDiscussionRepository : IRepository<Discussion>
{
    /// <summary>
    /// Get discussion by ID with Event included
    /// </summary>
    Task<Discussion?> GetByIdWithEventAsync(int id);

    /// <summary>
    /// Get discussion by event ID with Event included
    /// </summary>
    Task<Discussion?> GetByEventIdAsync(int eventId);
}
