using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for meeting ata management
/// </summary>
public interface IMeetingAtaService
{
    /// <summary>
    /// Gets a meeting ata by ID
    /// </summary>
    Task<MeetingAta?> GetByIdAsync(int id);

    /// <summary>
    /// Gets a meeting ata by meeting ID
    /// </summary>
    Task<MeetingAta?> GetByMeetingIdAsync(int meetingId);

    /// <summary>
    /// Gets meeting atas for multiple meetings (batch operation to avoid N+1 queries)
    /// </summary>
    Task<Dictionary<int, MeetingAta>> GetByMeetingIdsAsync(IEnumerable<int> meetingIds);

    /// <summary>
    /// Gets a meeting ata by ID with all details (Meeting, Users, AgendaPoints, Attachments)
    /// </summary>
    Task<MeetingAta?> GetByIdWithDetailsAsync(int id);

    /// <summary>
    /// Creates a new meeting ata
    /// </summary>
    Task<MeetingAta> CreateAtaAsync(MeetingAta ata);

    /// <summary>
    /// Updates an existing meeting ata
    /// </summary>
    Task UpdateAtaAsync(MeetingAta ata);

    /// <summary>
    /// Checks if an ata exists for a given meeting
    /// </summary>
    Task<bool> ExistsForMeetingAsync(int meetingId);

    /// <summary>
    /// Determines if a user can create or edit an ata for a meeting
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="meeting">Meeting entity</param>
    /// <param name="userRoles">User's role names</param>
    /// <param name="userPositions">User's positions</param>
    /// <param name="existingAta">Optional existing ATA to check if user is secretary</param>
    /// <returns>True if user can create/edit, false otherwise</returns>
    bool CanCreateOrEditAta(string userId, Meeting meeting, IEnumerable<string> userRoles, IEnumerable<Position> userPositions, MeetingAta? existingAta = null);

    /// <summary>
    /// Publishes an ata, changing its status from Draft to Published.
    /// Once published, the ata can no longer be edited or deleted.
    /// </summary>
    /// <param name="id">Ata ID</param>
    Task PublishAtaAsync(int id);
}
