using Microsoft.AspNetCore.Identity;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing user roles and positions
/// Extracted from Roles.razor to improve separation of concerns
/// </summary>
public class RoleManagementService : IRoleManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;

    /// <summary>
    /// Initializes a new instance of the RoleManagementService
    /// </summary>
    /// <param name="userManager">User manager for role operations</param>
    public RoleManagementService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    /// <summary>
    /// Promotes a user to appropriate role based on position assignment
    /// Caloiro members get Mod role, others get Admin role
    /// </summary>
    /// <param name="user">The user to promote</param>
    /// <param name="position">The position being assigned</param>
    /// <param name="fiscalStartYear">The fiscal year start year (to determine if current year)</param>
    /// <param name="currentFiscalStartYear">The current fiscal year start year</param>
    /// <returns>True if promotion occurred, false otherwise</returns>
    public async Task<bool> PromoteUserForPositionAsync(
        ApplicationUser user,
        Position position,
        int fiscalStartYear,
        int currentFiscalStartYear)
    {
        // Only promote if fiscal year is current
        bool isFiscalYearCurrent = (fiscalStartYear == currentFiscalStartYear);
        if (!isFiscalYearCurrent)
        {
            return false;
        }

        // Update user's current Positions property
        var positions = user.Positions.ToList();
        if (!positions.Contains(position))
        {
            positions.Add(position);
            user.Positions = positions;
            await _userManager.UpdateAsync(user);
        }

        // Promote user from Member to Admin or Mod role
        // CALOIRO members get Mod role, others get Admin role
        var currentRoles = await _userManager.GetRolesAsync(user);
        var isCaloiro = user.Categories.Contains(MemberCategory.Caloiro);
        var targetRole = isCaloiro ? "Mod" : "Admin";
        
        if (!currentRoles.Contains("Admin") && !currentRoles.Contains("Mod"))
        {
            // Remove Member role if present
            if (currentRoles.Contains("Member"))
            {
                await _userManager.RemoveFromRoleAsync(user, "Member");
            }
            // Add appropriate role (Mod for Caloiro, Admin for others)
            await _userManager.AddToRoleAsync(user, targetRole);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Demotes a user by removing Admin/Mod role and position when role assignment is removed
    /// </summary>
    /// <param name="user">The user to demote</param>
    /// <param name="position">The position being removed</param>
    /// <param name="fiscalStartYear">The fiscal year start year (to determine if current year)</param>
    /// <param name="currentFiscalStartYear">The current fiscal year start year</param>
    /// <returns>True if demotion occurred, false otherwise</returns>
    public async Task<bool> DemoteUserForPositionAsync(
        ApplicationUser user,
        Position position,
        int fiscalStartYear,
        int currentFiscalStartYear)
    {
        // Only demote if fiscal year is current
        bool isFiscalYearCurrent = (fiscalStartYear == currentFiscalStartYear);
        if (!isFiscalYearCurrent)
        {
            return false;
        }

        // Remove from user's current Positions property
        var positions = user.Positions.ToList();
        if (positions.Contains(position))
        {
            positions.Remove(position);
            user.Positions = positions;
            await _userManager.UpdateAsync(user);
        }

        // Check if user has no more positions - if so, demote from Admin/Mod to Member
        var userRoles = await _userManager.GetRolesAsync(user);
        if (!positions.Any() && (userRoles.Contains("Admin") || userRoles.Contains("Mod")) && !userRoles.Contains("Owner"))
        {
            // Remove Admin or Mod role
            if (userRoles.Contains("Admin"))
            {
                await _userManager.RemoveFromRoleAsync(user, "Admin");
            }
            if (userRoles.Contains("Mod"))
            {
                await _userManager.RemoveFromRoleAsync(user, "Mod");
            }
            
            // Add Member role
            await _userManager.AddToRoleAsync(user, "Member");
            
            // Invalidate security stamp to force fresh cookies and token refresh
            await _userManager.UpdateSecurityStampAsync(user);
            return true;
        }

        return false;
    }
}
