using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The members area rules (React track 017, docs/react-members.md), enforced server-side by
/// <c>MemberDirectoryService</c>. Same as the Blazor /members and /hierarchy (both [Authorize]):
///
/// - Visitors: nothing (401); the pages send them to sign in.
/// - Any signed-in member: the directory, a member's details (contacts included, as the old modal), the active
///   members and birthdays lists, and the hierarchy.
/// - Admin or Owner: also the link to the Blazor /members/manage tools (create, edit, delete, expel, nickname, make
///   active, push reminder), which keep their own per-action checks.
/// </summary>
public static class MembersAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));
}
