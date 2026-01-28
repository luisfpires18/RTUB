using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing user roles and positions
/// Extracted from Roles.razor to improve separation of concerns
/// </summary>
public interface IRoleManagementService
{
    /// <summary>
    /// Promotes a user to appropriate role based on position assignment
    /// Caloiro members get Mod role, others get Admin role
    /// </summary>
    /// <param name="user">The user to promote</param>
    /// <param name="position">The position being assigned</param>
    /// <param name="fiscalStartYear">The fiscal year start year (to determine if current year)</param>
    /// <param name="currentFiscalStartYear">The current fiscal year start year</param>
    /// <returns>True if promotion occurred, false otherwise</returns>
    Task<bool> PromoteUserForPositionAsync(
        ApplicationUser user,
        Position position,
        int fiscalStartYear,
        int currentFiscalStartYear);

    /// <summary>
    /// Demotes a user by removing Admin/Mod role and position when role assignment is removed
    /// </summary>
    /// <param name="user">The user to demote</param>
    /// <param name="position">The position being removed</param>
    /// <param name="fiscalStartYear">The fiscal year start year (to determine if current year)</param>
    /// <param name="currentFiscalStartYear">The current fiscal year start year</param>
    /// <returns>True if demotion occurred, false otherwise</returns>
    Task<bool> DemoteUserForPositionAsync(
        ApplicationUser user,
        Position position,
        int fiscalStartYear,
        int currentFiscalStartYear);
}
