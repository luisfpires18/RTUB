using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for MeetingParticipation entity
/// </summary>
public interface IMeetingParticipationRepository : IRepository<MeetingParticipation>
{
    /// <summary>
    /// Get participations by meeting ID with User included
    /// </summary>
    Task<IEnumerable<MeetingParticipation>> GetByMeetingIdAsync(int meetingId);

    /// <summary>
    /// Get participations by user ID with Meeting included
    /// </summary>
    Task<IEnumerable<MeetingParticipation>> GetByUserIdAsync(string userId);

    /// <summary>
    /// Get participation by meeting ID and user ID
    /// </summary>
    Task<MeetingParticipation?> GetByMeetingAndUserAsync(int meetingId, string userId);

    /// <summary>
    /// Get participations by attendance status
    /// </summary>
    Task<IEnumerable<MeetingParticipation>> GetByAttendanceAsync(bool willAttend);

    /// <summary>
    /// Delete all participations for a specific meeting (batch operation)
    /// </summary>
    Task DeleteByMeetingIdAsync(int meetingId);
}
