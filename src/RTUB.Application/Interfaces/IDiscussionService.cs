using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Discussion operations
/// </summary>
public interface IDiscussionService
{
    Task<Discussion?> GetDiscussionByIdAsync(int id);
    Task<Discussion?> GetDiscussionByEventIdAsync(int eventId);
    Task<Discussion> CreateDiscussionAsync(int eventId, string? title = null);
    Task UpdateDiscussionAsync(int id, string? title);
    Task DeleteDiscussionAsync(int id);
}
