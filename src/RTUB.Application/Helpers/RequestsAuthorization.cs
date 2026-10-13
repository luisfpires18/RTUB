using System.Security.Claims;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;

namespace RTUB.Application.Helpers;

/// <summary>
/// The performance-requests admin rules (task 031, docs/react-requests-questions.md), enforced server-side by
/// <c>RequestAdminService</c>. Same as the Blazor /requests ([Authorize]; Leitões sent away unless Admin or Owner;
/// approve, reject and delete for Admin only), with Owner inheriting Admin as everywhere else on the React track:
///
/// - Visitors: nothing (401); the page sends them to sign in.
/// - Leitões: nothing (403), unless Admin or Owner.
/// - Any other signed-in member: every request, contact details included (as the old page showed them).
/// - Admin or Owner: also approve, reject and delete.
/// </summary>
public static class RequestsAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static bool CanOpen(ApplicationUser member, ClaimsPrincipal user) => CanManage(user) || !member.IsLeitao();

    public static string? UserId(ClaimsPrincipal user) => IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
