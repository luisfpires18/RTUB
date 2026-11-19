using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Órgãos Sociais rules (docs/react-governance.md), enforced server-side by
/// <c>GovernanceManagementService</c>. Owner inherits Admin:
///
/// - Visitors: the public mandate only (GET /api/public/governance).
/// - Member, Mod, Admin, Owner: the RGI.
/// - Mod, Admin, Owner: create fiscal years, assign and remove positions.
/// </summary>
public static class GovernanceAuthorization
{
    public static bool CanManage(ClaimsPrincipal user) =>
        user.IsMember() && (user.IsInRole("Mod") || user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static bool CanViewRgi(ClaimsPrincipal user) => CanManage(user) || (user.IsMember() && user.IsInRole("Member"));
}
