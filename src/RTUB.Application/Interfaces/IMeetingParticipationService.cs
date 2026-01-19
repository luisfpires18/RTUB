using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for MeetingParticipation operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface IMeetingParticipationService
{
    Task<MeetingParticipation?> GetParticipationByIdAsync(int id);
    Task<IEnumerable<MeetingParticipation>> GetAllParticipationsAsync();
    Task<IEnumerable<MeetingParticipation>> GetParticipationsByMeetingIdAsync(int meetingId);
    Task<IEnumerable<MeetingParticipation>> GetParticipationsByUserIdAsync(string userId);
    Task<MeetingParticipation?> GetParticipationByMeetingAndUserAsync(int meetingId, string userId);
    Task<MeetingParticipation> CreateParticipationAsync(string userId, int meetingId, string? notes = null, bool willAttend = true);
    Task<MeetingParticipation> UpdateParticipationAsync(int participationId, bool willAttend, string? notes = null);
    Task DeleteParticipationAsync(int id);
    
    /// <summary>
    /// Gets participation counts (WillAttend=true) for multiple meetings in a single query
    /// </summary>
    Task<Dictionary<int, int>> GetParticipationCountsByMeetingIdsAsync(IEnumerable<int> meetingIds);
}
