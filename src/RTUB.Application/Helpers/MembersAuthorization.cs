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
/// - Task 076: Mod (<see cref="CanEditInfo"/>) edits the member information the queue needs - Cidade, Contacto,
///   instruments, the missing Leitão / Caloiro / Tuno dates and the Padrinho - through its own limited endpoint; nothing
///   else of Admin's (no create, delete, names, email, category, nickname, expel, reminder).
/// </summary>
public static class MembersAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static bool CanDeleteAnyMember(ClaimsPrincipal user) => IsMember(user) && user.IsInRole("Owner");

    /// <summary>Task 076: the limited member-information edit and the instruments - Mod, Admin, Owner.</summary>
    public static bool CanEditInfo(ClaimsPrincipal user) => CanManage(user) || (IsMember(user) && user.IsInRole("Mod"));
}
