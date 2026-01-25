using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for meeting ATA confirmation management
/// </summary>
public interface IMeetingAtaConfirmationService
{
    /// <summary>
    /// Gets a user's confirmation for an ATA
    /// </summary>
    Task<MeetingAtaConfirmation?> GetUserConfirmationAsync(int ataId, string userId);

    /// <summary>
    /// Confirms an ATA for a user
    /// </summary>
    Task ConfirmAtaAsync(int ataId, string userId, string? notes = null);

    /// <summary>
    /// Refuses an ATA for a user
    /// </summary>
    Task RefuseAtaAsync(int ataId, string userId, string? notes = null);

    /// <summary>
    /// Gets all confirmations for an ATA
    /// </summary>
    Task<IEnumerable<MeetingAtaConfirmation>> GetConfirmationsByAtaIdAsync(int ataId);
}