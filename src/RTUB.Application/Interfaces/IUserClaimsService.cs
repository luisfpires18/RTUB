using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using RTUB.Core.Constants;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for managing user claims based on categories, positions, and roles
/// </summary>
public interface IUserClaimsService
{
    /// <summary>
    /// Synchronizes user's categories and positions to claims
    /// Should be called on login or when user's categories/positions change
    /// </summary>
    Task SyncUserClaimsAsync(ApplicationUser user);
    
    /// <summary>
    /// Gets all claims for a user (including category, position, and permission claims)
    /// </summary>
    Task<IList<Claim>> GetUserClaimsAsync(ApplicationUser user);
    
    /// <summary>
    /// Adds a permission claim to a user
    /// </summary>
    Task AddPermissionClaimAsync(ApplicationUser user, string permission);
    
    /// <summary>
    /// Removes a permission claim from a user
    /// </summary>
    Task RemovePermissionClaimAsync(ApplicationUser user, string permission);
    
    /// <summary>
    /// Checks if a user has a specific permission
    /// </summary>
    Task<bool> HasPermissionAsync(ApplicationUser user, string permission);
    
    /// <summary>
    /// Gets all permissions for a user
    /// </summary>
    Task<IList<string>> GetPermissionsAsync(ApplicationUser user);
}
