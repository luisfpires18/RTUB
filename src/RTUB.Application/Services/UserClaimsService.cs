using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using RTUB.Application.Interfaces;
using RTUB.Core.Constants;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing user claims based on categories, positions, and roles
/// Ensures claims are in sync with user's Categories and Positions properties
/// </summary>
public class UserClaimsService : IUserClaimsService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserClaimsService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    /// <summary>
    /// Synchronizes user's categories and positions to claims
    /// This maintains backward compatibility while enabling claims-based authorization
    /// </summary>
    public async Task SyncUserClaimsAsync(ApplicationUser user)
    {
        // Get existing claims
        var existingClaims = await _userManager.GetClaimsAsync(user);
        
        // Remove old category and position claims
        var oldCategoryClaims = existingClaims.Where(c => c.Type == CustomClaimTypes.Category).ToList();
        var oldPositionClaims = existingClaims.Where(c => c.Type == CustomClaimTypes.Position).ToList();
        var oldYearClaims = existingClaims.Where(c => 
            c.Type == CustomClaimTypes.YearTuno || 
            c.Type == CustomClaimTypes.YearCaloiro || 
            c.Type == CustomClaimTypes.YearLeitao).ToList();
        
        if (oldCategoryClaims.Any())
            await _userManager.RemoveClaimsAsync(user, oldCategoryClaims);
        if (oldPositionClaims.Any())
            await _userManager.RemoveClaimsAsync(user, oldPositionClaims);
        if (oldYearClaims.Any())
            await _userManager.RemoveClaimsAsync(user, oldYearClaims);
        
        // Add category claims
        var categoryClaims = user.Categories.Select(c => 
            new Claim(CustomClaimTypes.Category, c.ToString())).ToList();
        
        if (categoryClaims.Any())
            await _userManager.AddClaimsAsync(user, categoryClaims);
        
        // Add position claims
        var positionClaims = user.Positions.Select(p => 
            new Claim(CustomClaimTypes.Position, p.ToString())).ToList();
        
        if (positionClaims.Any())
            await _userManager.AddClaimsAsync(user, positionClaims);
        
        // Add year claims for tracking tenure
        var yearClaims = new List<Claim>();
        if (user.YearTuno.HasValue)
            yearClaims.Add(new Claim(CustomClaimTypes.YearTuno, user.YearTuno.Value.ToString()));
        if (user.YearCaloiro.HasValue)
            yearClaims.Add(new Claim(CustomClaimTypes.YearCaloiro, user.YearCaloiro.Value.ToString()));
        if (user.YearLeitao.HasValue)
            yearClaims.Add(new Claim(CustomClaimTypes.YearLeitao, user.YearLeitao.Value.ToString()));
        
        if (yearClaims.Any())
            await _userManager.AddClaimsAsync(user, yearClaims);
        
        // Add automatic permission claims based on category/position
        await AddAutomaticPermissionClaimsAsync(user);
    }

    /// <summary>
    /// Adds automatic permission claims based on user's categories and positions
    /// </summary>
    private async Task AddAutomaticPermissionClaimsAsync(ApplicationUser user)
    {
        var permissionClaims = new List<Claim>();
        
        // Category-based permissions
        if (user.Categories.Contains(MemberCategory.Leitao) && 
            !user.Categories.Any(c => c != MemberCategory.Leitao))
        {
            // Leitão only - restricted permissions
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.LeitaoRestricted));
        }
        
        if (user.Categories.Contains(MemberCategory.Caloiro) ||
            user.Categories.Contains(MemberCategory.Tuno) ||
            user.Categories.Contains(MemberCategory.Veterano) ||
            user.Categories.Contains(MemberCategory.Tunossauro))
        {
            // Effective members
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.IsEffectiveMember));
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.EnrollInEvents));
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.EnrollAsPerformer));
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.ViewMemberPages));
        }
        
        if (user.Categories.Contains(MemberCategory.Tuno) ||
            user.Categories.Contains(MemberCategory.Veterano) ||
            user.Categories.Contains(MemberCategory.Tunossauro))
        {
            // Tuno or higher - can be mentor, vote, hold president positions
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.CanBeMentor));
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.CanVote));
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.CanHoldPresidentPosition));
        }
        
        // Position-based permissions
        if (user.Positions.Contains(Position.Magister))
        {
            // Magister has highest authority
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.ManageAllFinances));
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.AssignAllPositions));
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.ViewAuditLogs));
        }
        
        if (user.Positions.Contains(Position.Ensaiador))
        {
            // Ensaiador manages rehearsals
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.ManageAllRehearsals));
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.ManageEnsaiadorRepertoire));
        }
        
        if (user.Positions.Contains(Position.PrimeiroTesoureiro) ||
            user.Positions.Contains(Position.SegundoTesoureiro))
        {
            // Treasurers manage finances
            permissionClaims.Add(new Claim(CustomClaimTypes.Permission, Permissions.ManageTreasurerFinances));
        }
        
        // Remove existing permission claims and add new ones
        var existingPermissionClaims = (await _userManager.GetClaimsAsync(user))
            .Where(c => c.Type == CustomClaimTypes.Permission).ToList();
        
        if (existingPermissionClaims.Any())
            await _userManager.RemoveClaimsAsync(user, existingPermissionClaims);
        
        if (permissionClaims.Any())
            await _userManager.AddClaimsAsync(user, permissionClaims);
    }

    public async Task<IList<Claim>> GetUserClaimsAsync(ApplicationUser user)
    {
        return await _userManager.GetClaimsAsync(user);
    }

    public async Task AddPermissionClaimAsync(ApplicationUser user, string permission)
    {
        var claim = new Claim(CustomClaimTypes.Permission, permission);
        await _userManager.AddClaimAsync(user, claim);
    }

    public async Task RemovePermissionClaimAsync(ApplicationUser user, string permission)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        var claim = claims.FirstOrDefault(c => 
            c.Type == CustomClaimTypes.Permission && c.Value == permission);
        
        if (claim != null)
            await _userManager.RemoveClaimAsync(user, claim);
    }

    public async Task<bool> HasPermissionAsync(ApplicationUser user, string permission)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        return claims.Any(c => c.Type == CustomClaimTypes.Permission && c.Value == permission);
    }

    public async Task<IList<string>> GetPermissionsAsync(ApplicationUser user)
    {
        var claims = await _userManager.GetClaimsAsync(user);
        return claims
            .Where(c => c.Type == CustomClaimTypes.Permission)
            .Select(c => c.Value)
            .ToList();
    }
}
