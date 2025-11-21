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
    /// Automatically computes and persists retirement status based on activity history
    /// </summary>
    /// <param name="userId">The user ID to get status for</param>
    /// <returns>A result containing retirement status, last activity dates, and activity flags</returns>
    Task<MemberStatusResult> GetMemberStatusAsync(string userId);
}
