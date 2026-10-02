using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Órgãos Sociais rules (React tracks 008 and 016, docs/react-governance.md), enforced server-side by
/// <c>GovernanceManagementService</c>. Same as the Blazor /member/roles, plus Owner inheriting Admin:
///
/// - Visitors: the public mandate only (GET /api/public/governance).
/// - Member, Mod, Admin, Owner: the RGI (the Blazor page showed it to Member, Mod and Admin).
/// - Mod, Admin, Owner: create fiscal years, assign and remove positions (was Mod and Admin).
/// </summary>
public static class GovernanceAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanManage(ClaimsPrincipal user) =>
        IsMember(user) && (user.IsInRole("Mod") || user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static bool CanViewRgi(ClaimsPrincipal user) => CanManage(user) || (IsMember(user) && user.IsInRole("Member"));

    public static string? UserId(ClaimsPrincipal user) =>
        IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
