using Microsoft.AspNetCore.Identity;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for user role management operations
/// Extracted from UserRoles.razor to improve separation of concerns
/// </summary>
public class UserRoleService : IUserRoleService
{
    /// <summary>
    /// Changes a user's role, handling role hierarchy (Owner gets Admin role automatically)
    /// </summary>
    /// <param name="userManager">UserManager instance</param>
    /// <param name="userId">The user ID</param>
    /// <param name="newRole">The new role to assign (Owner, Admin, or Member)</param>
    /// <returns>Tuple containing (success, errorMessage). success is true if operation succeeded.</returns>
    public async Task<(bool success, string errorMessage)> ChangeUserRoleAsync(UserManager<ApplicationUser> userManager, string userId, string newRole)
    {
        try
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return (false, "Utilizador não encontrado.");
            }

            var currentRoles = await userManager.GetRolesAsync(user);

            // Remove all existing roles
            if (currentRoles.Any())
            {
                var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeResult.Succeeded)
                {
                    return (false, $"Erro ao remover funções antigas: {string.Join(", ", removeResult.Errors.Select(e => e.Description))}");
                }
            }

            // Add new role
            // Note: Role hierarchy is Owner > Admin > Member
            // Owner gets Admin role for access to admin features (following pattern from SeedData)
            // Admin and Member roles are mutually exclusive
            var addResult = await userManager.AddToRoleAsync(user, newRole);
            if (addResult.Succeeded)
            {
                // If promoting to Owner, also add Admin role for access to admin features
                if (newRole == "Owner")
                {
                    await userManager.AddToRoleAsync(user, "Admin");
                }

                // Invalidate security stamp to force fresh cookies and token refresh
                // This ensures role changes take effect immediately on next sign-in
                await userManager.UpdateSecurityStampAsync(user);

                return (true, $"Função de {user.FirstName} {user.LastName} alterada para {newRole} com sucesso.");
            }
            else
            {
                return (false, $"Erro ao adicionar nova função: {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Erro inesperado: {ex.Message}");
        }
    }
}
