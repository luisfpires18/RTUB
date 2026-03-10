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

    // Positions that always grant Admin regardless of member category
    private static readonly HashSet<Position> AlwaysAdminPositions =
    [
        Position.PresidenteMesaAssembleia,
        Position.PresidenteConselhoFiscal,
        Position.PresidenteConselhoVeteranos,
        Position.Ensaiador,
    ];

    // Positions that grant no role (position tracking only)
    private static readonly HashSet<Position> NoRolePositions =
    [
        Position.PrimeiroSecretarioMesaAssembleia,
        Position.SegundoSecretarioMesaAssembleia,
        Position.PrimeiroRelatorConselhoFiscal,
        Position.SegundoRelatorConselhoFiscal,
    ];

    public RoleManagementService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    }

    public async Task<bool> PromoteUserForPositionAsync(
        ApplicationUser user,
        Position position,
        int fiscalStartYear,
        int currentFiscalStartYear)
    {
        if (fiscalStartYear != currentFiscalStartYear)
            return false;

        // Update user's Positions property
        var positions = user.Positions.ToList();
        if (!positions.Contains(position))
        {
            positions.Add(position);
            user.Positions = positions;
            await _userManager.UpdateAsync(user);
        }

        // Positions that carry no role — skip role assignment
        if (NoRolePositions.Contains(position))
            return false;

        var currentRoles = await _userManager.GetRolesAsync(user);

        // Already has a non-Member role — no further promotion needed
        if (currentRoles.Contains("Admin") || currentRoles.Contains("Mod"))
            return false;

        // Determine target role:
        // - AlwaysAdmin positions → Admin regardless of category
        // - Direção positions → Admin for Tuno, Mod for Caloiro
        string targetRole;
        if (AlwaysAdminPositions.Contains(position))
        {
            targetRole = "Admin";
        }
        else
        {
            targetRole = user.Categories.Contains(MemberCategory.Caloiro) ? "Mod" : "Admin";
        }

        if (currentRoles.Contains("Member"))
            await _userManager.RemoveFromRoleAsync(user, "Member");

        await _userManager.AddToRoleAsync(user, targetRole);
        return true;
    }

    public async Task<bool> DemoteUserForPositionAsync(
        ApplicationUser user,
        Position position,
        int fiscalStartYear,
        int currentFiscalStartYear)
    {
        if (fiscalStartYear != currentFiscalStartYear)
            return false;

        // Remove from user's Positions property
        var positions = user.Positions.ToList();
        if (positions.Contains(position))
        {
            positions.Remove(position);
            user.Positions = positions;
            await _userManager.UpdateAsync(user);
        }

        // Check if any remaining positions still grant a role
        bool hasRoleGrantingPosition = positions.Any(p => !NoRolePositions.Contains(p));
        if (hasRoleGrantingPosition)
            return false;

        var userRoles = await _userManager.GetRolesAsync(user);
        if (!(userRoles.Contains("Admin") || userRoles.Contains("Mod")) || userRoles.Contains("Owner"))
            return false;

        if (userRoles.Contains("Admin"))
            await _userManager.RemoveFromRoleAsync(user, "Admin");
        if (userRoles.Contains("Mod"))
            await _userManager.RemoveFromRoleAsync(user, "Mod");

        await _userManager.AddToRoleAsync(user, "Member");
        await _userManager.UpdateSecurityStampAsync(user);
        return true;
    }
}
