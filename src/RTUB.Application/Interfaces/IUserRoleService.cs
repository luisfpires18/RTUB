using Microsoft.AspNetCore.Identity;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for user role management operations
/// Extracted from UserRoles.razor to improve separation of concerns
/// </summary>
public interface IUserRoleService
{
    /// <summary>
    /// Changes a user's role, handling role hierarchy (Owner gets Admin role automatically)
    /// </summary>
    /// <param name="userManager">UserManager instance</param>
    /// <param name="userId">The user ID</param>
    /// <param name="newRole">The new role to assign (Owner, Admin, or Member)</param>
    /// <returns>Tuple containing (success, errorMessage). success is true if operation succeeded.</returns>
    Task<(bool success, string errorMessage)> ChangeUserRoleAsync(UserManager<ApplicationUser> userManager, string userId, string newRole);
}
