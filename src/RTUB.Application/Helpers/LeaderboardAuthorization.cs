using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The classification rules (docs/react-leaderboard.md), enforced server-side by
/// <c>LeaderboardService</c>, with Owner inheriting Admin:
///
/// - Visitors: nothing; the page sends them to sign in.
/// - Any signed-in member (Mod included): the table, a member's details, the comments; comment, like, and delete their
///   own comments.
/// - Admin or Owner: also delete any comment. Nobody edits the story text.
/// </summary>
public static class LeaderboardAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static string? UserId(ClaimsPrincipal user) => IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
