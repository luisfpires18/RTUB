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
    /// Deletes a meeting ata
    /// </summary>
    Task DeleteAtaAsync(int id);

    /// <summary>
    /// Checks if an ata exists for a given meeting
    /// </summary>
    Task<bool> ExistsForMeetingAsync(int meetingId);

    /// <summary>
    /// Gets the status of ATAs for multiple meetings in a single query.
    /// Returns a dictionary with meeting IDs as keys and nullable MeetingAtaStatus as values.
    /// If a meeting has no ATA, the value will be null.
    /// </summary>
    /// <param name="meetingIds">Collection of meeting IDs to check</param>
    /// <returns>Dictionary mapping meeting ID to ATA status (null if no ATA exists)</returns>
    Task<Dictionary<int, MeetingAtaStatus?>> GetAtaStatusForMeetingsAsync(IEnumerable<int> meetingIds);

    /// <summary>
    /// Determines if a user can create or edit an ata for a meeting
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="meeting">Meeting entity</param>
    /// <param name="userRoles">User's role names</param>
    /// <param name="userPositions">User's positions</param>
    /// <returns>True if user can create/edit, false otherwise</returns>
    bool CanCreateOrEditAta(string userId, Meeting meeting, IEnumerable<string> userRoles, IEnumerable<Position> userPositions);

    /// <summary>
    /// Determines if a user can view an ata
    /// </summary>
    /// <param name="userId">User ID to check</param>
    /// <param name="meeting">Meeting entity</param>
    /// <param name="userRoles">User's role names</param>
    /// <returns>True if user can view, false otherwise</returns>
    bool CanViewAta(string userId, Meeting meeting, IEnumerable<string> userRoles);

    /// <summary>
    /// Adds an agenda point to an ata
    /// </summary>
    Task AddAgendaPointAsync(int ataId, MeetingAtaAgendaPoint point);

    /// <summary>
    /// Updates an agenda point
    /// </summary>
    Task UpdateAgendaPointAsync(MeetingAtaAgendaPoint point);

    /// <summary>
    /// Deletes an agenda point
    /// </summary>
    Task DeleteAgendaPointAsync(int pointId);

    /// <summary>
    /// Reorders agenda points based on a list of IDs
    /// </summary>
    /// <param name="ataId">Meeting Ata ID</param>
    /// <param name="pointIds">List of point IDs in desired order</param>
    Task ReorderAgendaPointsAsync(int ataId, List<int> pointIds);

    /// <summary>
    /// Adds an attachment to an ata
    /// </summary>
    Task AddAttachmentAsync(int ataId, MeetingAtaAttachment attachment);

    /// <summary>
    /// Updates an attachment
    /// </summary>
    Task UpdateAttachmentAsync(MeetingAtaAttachment attachment);

    /// <summary>
    /// Deletes an attachment
    /// </summary>
    Task DeleteAttachmentAsync(int attachmentId);

    /// <summary>
    /// Publishes an ata, changing its status from Draft to Published.
    /// Once published, the ata can no longer be edited or deleted.
    /// </summary>
    /// <param name="id">Ata ID</param>
    Task PublishAtaAsync(int id);
}
