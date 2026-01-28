using Microsoft.AspNetCore.Components.Authorization;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for naipe-related authorization checks
/// Extracted from Naipes.razor to improve separation of concerns
/// </summary>
public class NaipeAuthorizationService : INaipeAuthorizationService
{
    /// <summary>
    /// Determines if a user can edit a specific content item
    /// </summary>
    /// <param name="authState">The current authentication state</param>
    /// <param name="currentUserId">The current user ID (can be null if not authenticated)</param>
    /// <param name="contentCreatedByUserId">The user ID who created the content</param>
    /// <returns>True if the user can edit the content</returns>
    public bool CanEditContent(AuthenticationState authState, string? currentUserId, string contentCreatedByUserId)
    {
        var isAdmin = authState.User.IsInRole("Admin");
        return isAdmin || (currentUserId != null && currentUserId == contentCreatedByUserId);
    }

    /// <summary>
    /// Determines if a user can delete a specific content item
    /// </summary>
    /// <param name="authState">The current authentication state</param>
    /// <param name="currentUserId">The current user ID (can be null if not authenticated)</param>
    /// <param name="contentCreatedByUserId">The user ID who created the content</param>
    /// <returns>True if the user can delete the content</returns>
    public bool CanDeleteContent(AuthenticationState authState, string? currentUserId, string contentCreatedByUserId)
    {
        var isAdmin = authState.User.IsInRole("Admin");
        return isAdmin || (currentUserId != null && currentUserId == contentCreatedByUserId);
    }
}
