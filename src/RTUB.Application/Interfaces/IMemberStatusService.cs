using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing member status (retirement status, activity tracking)
/// </summary>
public interface IMemberStatusService
{
    /// <summary>
    /// Gets the current status for a specific member
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>Member status result</returns>
    Task<MemberStatusResult> GetMemberStatusAsync(string userId);

    /// <summary>
    /// Updates the status for a specific member
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>Updated member status result</returns>
    Task<MemberStatusResult> UpdateMemberStatusAsync(string userId);

    /// <summary>
    /// Updates status for all active members
    /// </summary>
    /// <returns>Number of members updated</returns>
    Task<int> UpdateAllMemberStatusesAsync();

    /// <summary>
    /// Gets status for multiple members in batch
    /// </summary>
    /// <param name="userIds">Collection of user IDs to get status for</param>
    /// <returns>Dictionary mapping user ID to member status result (nullable values)</returns>
    Task<Dictionary<string, MemberStatusResult?>> GetMemberStatusesBatchAsync(IEnumerable<string> userIds);

    /// <summary>
    /// Activates a member with override, bypassing normal retirement checks
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>Updated member status result</returns>
    /// <exception cref="InvalidOperationException">Thrown when user is not found</exception>
    Task<MemberStatusResult> ActivateMemberWithOverrideAsync(string userId);
}
