using Microsoft.AspNetCore.Components.Authorization;
using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for event-related authorization checks
/// Extracted from EventDiscussion.razor to improve separation of concerns
/// </summary>
public interface IEventAuthorizationService
{
    /// <summary>
    /// Determines authorization status for event discussion operations
    /// </summary>
    /// <param name="authState">The current authentication state</param>
    /// <param name="currentUser">The current user (can be null if not authenticated)</param>
    /// <returns>Tuple containing (isAdmin, isOwner, canCreatePost)</returns>
    (bool isAdmin, bool isOwner, bool canCreatePost) GetEventDiscussionAuthorization(
        AuthenticationState authState,
        ApplicationUser? currentUser);
}
