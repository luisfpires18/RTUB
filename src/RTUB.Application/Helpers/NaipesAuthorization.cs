using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Naipes rules (task 033, docs/react-naipes.md), enforced server-side by <c>NaipeBoardService</c>. Same as the
/// Blazor /naipes and /naipes/config, with Owner inheriting Admin as everywhere else on the React track:
///
/// - Visitors: nothing (401); the pages send them to sign in.
/// - Any signed-in member (Leitões included): read, add videos and images, comment, edit and delete what they added,
///   delete their own comments.
/// - Admin or Owner: also edit and delete anyone's videos, images and comments, and change the instrument settings.
/// </summary>
public static class NaipesAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static bool CanEdit(ClaimsPrincipal user, string createdByUserId) =>
        CanManage(user) || (UserId(user) is { } me && me == createdByUserId);

    public static string? UserId(ClaimsPrincipal user) => IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
