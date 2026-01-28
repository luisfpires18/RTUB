using Microsoft.AspNetCore.Identity;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for user account operations (unlocking, etc.)
/// Extracted from UserRoles.razor to improve separation of concerns
/// </summary>
public class UserAccountService : IUserAccountService
{
    /// <summary>
    /// Unlocks a user account by removing lockout and resetting failed access count
    /// </summary>
    /// <param name="userManager">UserManager instance</param>
    /// <param name="userId">The user ID to unlock</param>
    /// <returns>Tuple containing (success, errorMessage). success is true if operation succeeded.</returns>
    public async Task<(bool success, string errorMessage)> UnlockUserAsync(UserManager<ApplicationUser> userManager, string userId)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return (false, "Utilizador não encontrado.");
        }

        var unlockResult = await userManager.SetLockoutEndDateAsync(user, null);
        if (!unlockResult.Succeeded)
        {
            return (false, $"Erro ao desbloquear conta: {string.Join(", ", unlockResult.Errors.Select(e => e.Description))}");
        }

        var resetResult = await userManager.ResetAccessFailedCountAsync(user);
        if (!resetResult.Succeeded)
        {
            return (false, $"Erro ao reiniciar tentativas: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
        }

        return (true, $"Conta de {user.FirstName} {user.LastName} desbloqueada com sucesso.");
    }
}
