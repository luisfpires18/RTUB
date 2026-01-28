using Microsoft.AspNetCore.Identity;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for user account operations (unlocking, etc.)
/// Extracted from UserRoles.razor to improve separation of concerns
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// Unlocks a user account by removing lockout and resetting failed access count
    /// </summary>
    /// <param name="userManager">UserManager instance</param>
    /// <param name="userId">The user ID to unlock</param>
    /// <returns>Tuple containing (success, errorMessage). success is true if operation succeeded.</returns>
    Task<(bool success, string errorMessage)> UnlockUserAsync(UserManager<ApplicationUser> userManager, string userId);
}
