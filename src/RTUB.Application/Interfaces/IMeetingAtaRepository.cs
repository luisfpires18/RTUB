using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for MeetingAta entity with domain-specific operations
/// </summary>
public interface IMeetingAtaRepository
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
    /// Gets a meeting ata by ID with all related entities (Meeting, Users, AgendaPoints, Attachments)
    /// </summary>
    Task<MeetingAta?> GetByIdWithDetailsAsync(int id);

    /// <summary>
    /// Gets all meeting atas
    /// </summary>
    Task<IEnumerable<MeetingAta>> GetAllAsync();

    /// <summary>
    /// Creates a new meeting ata
    /// </summary>
    Task<MeetingAta> CreateAsync(MeetingAta ata);

    /// <summary>
    /// Updates an existing meeting ata
    /// </summary>
    Task UpdateAsync(MeetingAta ata);

    /// <summary>
    /// Deletes a meeting ata
    /// </summary>
    Task DeleteAsync(int id);

    /// <summary>
    /// Checks if an ata exists for a given meeting
    /// </summary>
    Task<bool> ExistsForMeetingAsync(int meetingId);
}
