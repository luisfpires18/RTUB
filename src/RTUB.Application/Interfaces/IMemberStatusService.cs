using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for managing member status in the Tuna
/// Handles retirement status computation and last activity tracking
/// </summary>
public interface IMemberStatusService
{
    /// <summary>
    /// Gets the comprehensive status of a member including retirement state and last activity dates
    /// First checks the database cache, then computes if needed
    /// </summary>
    /// <param name="userId">The user ID to get status for</param>
    /// <returns>A result containing retirement status, last activity dates, and activity flags</returns>
    Task<MemberStatusResult> GetMemberStatusAsync(string userId);
    
    /// <summary>
    /// Updates the status for a specific member by recalculating from activities
    /// Persists the result to the database
    /// </summary>
    /// <param name="userId">The user ID to update status for</param>
    /// <returns>The updated status result</returns>
    Task<MemberStatusResult> UpdateMemberStatusAsync(string userId);
    
    /// <summary>
    /// Updates the status for all active members (Caloiro, Tuno, Veterano, Tunossauro)
    /// Should be called periodically by a background service
    /// </summary>
    /// <returns>The number of member statuses updated</returns>
    Task<int> UpdateAllMemberStatusesAsync();
}
