using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The members area rules (React tracks 017 and 018, docs/react-members.md), enforced server-side by
/// <c>MemberDirectoryService</c> and <c>MemberAdminService</c>. Same as the Blazor /members (all [Authorize]), with
/// Owner inheriting Admin:
///
/// - Visitors: nothing (401); the pages send them to sign in.
/// - Any signed-in member: the directory, a member's details (contacts included, as the old modal), the active
///   members and birthdays lists, and the hierarchy.
/// - Admin or Owner: create and edit members, instruments, "Definir alcunha", expel and reactivate Leitões, "Tornar
///   ativo", the push reminder, and delete Leitões.
/// - Owner: also delete any member, and change complete Leitão / Caloiro / Tuno dates (as an Admin who is the current
///   Magister).
/// </summary>
public static class MembersAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static bool CanDeleteAnyMember(ClaimsPrincipal user) => IsMember(user) && user.IsInRole("Owner");
}
