using RTUB.Application.Services.Retirement;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for managing retirement status of active members
/// Evaluates CALOIRO/TUNO members based on activity history
/// </summary>
public interface IRetirementStatusService
{
    /// <summary>
    /// Evaluates retirement status for a user based on their activity history
    /// </summary>
    /// <param name="userId">User ID to evaluate</param>
    /// <returns>RetirementStatusResult containing calculated status and metrics</returns>
    Task<RetirementStatusResult> EvaluateRetirementStatusAsync(string userId);
    
    /// <summary>
    /// Updates user's IsRetired status in database if it differs from calculated value
    /// </summary>
    /// <param name="userId">User ID to update</param>
    /// <returns>True if status was updated, false otherwise</returns>
    Task<bool> UpdateUserRetirementStatusAsync(string userId);
}
