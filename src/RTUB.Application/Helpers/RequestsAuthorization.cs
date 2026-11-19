using System.Security.Claims;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;

namespace RTUB.Application.Helpers;

/// <summary>
/// The performance-requests admin rules (docs/react-requests-questions.md), enforced server-side by
/// <c>RequestAdminService</c>, with Owner inheriting Admin as everywhere else on the React track:
///
/// - Visitors: nothing; the page sends them to sign in.
/// - Leitões: nothing, unless Admin or Owner.
/// - Any other signed-in member: every request, contact details included.
/// - Admin or Owner: also approve, reject and delete.
/// </summary>
public static class RequestsAuthorization
{
    public static bool CanManage(ClaimsPrincipal user) => user.IsMember() && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static bool CanOpen(ApplicationUser member, ClaimsPrincipal user) => CanManage(user) || !member.IsLeitao();
}
