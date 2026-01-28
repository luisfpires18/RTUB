using Microsoft.AspNetCore.Components.Authorization;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for naipe-related authorization checks
/// Extracted from Naipes.razor to improve separation of concerns
/// </summary>
public interface INaipeAuthorizationService
{
    /// <summary>
    /// Determines if a user can edit a specific content item
    /// </summary>
    /// <param name="authState">The current authentication state</param>
    /// <param name="currentUserId">The current user ID (can be null if not authenticated)</param>
    /// <param name="contentCreatedByUserId">The user ID who created the content</param>
    /// <returns>True if the user can edit the content</returns>
    bool CanEditContent(AuthenticationState authState, string? currentUserId, string contentCreatedByUserId);

    /// <summary>
    /// Determines if a user can delete a specific content item
    /// </summary>
    /// <param name="authState">The current authentication state</param>
    /// <param name="currentUserId">The current user ID (can be null if not authenticated)</param>
    /// <param name="contentCreatedByUserId">The user ID who created the content</param>
    /// <returns>True if the user can delete the content</returns>
    bool CanDeleteContent(AuthenticationState authState, string? currentUserId, string contentCreatedByUserId);
}
