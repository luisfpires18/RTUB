using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Discussion operations
/// </summary>
public interface IDiscussionService
{
    Task<Discussion?> GetByIdAsync(int id);
    Task<Discussion?> GetByEventIdAsync(int eventId);
    Task<Discussion> CreateForEventAsync(int eventId);
    Task<Discussion> GetOrCreateForEventAsync(int eventId);
}
