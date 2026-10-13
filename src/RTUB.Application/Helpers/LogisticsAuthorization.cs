using System.Security.Claims;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Logistics rules (unit 030 LOG-1, React track 023, docs/react-logistics.md), enforced server-side by
/// <c>LogisticsKanbanService</c>:
///
/// - Visitors: nothing (401); the pages send them to sign in.
/// - Leitões: nothing (403), unless they manage Logistics. The old boards page sent them away; the old board page forgot
///   to, which 023 fixes.
/// - Any other signed-in member: every board, list and card face; create reminders (stored, as before).
/// - Mod, Admin and Owner (Owner inherits Admin since 023): everything else - boards, lists, cards, card details,
///   labels, checklist, links, assignments, board files, moving cards and lists.
/// </summary>
public static class LogisticsAuthorization
{
    public static bool CanManage(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && (user.IsInRole("Admin") || user.IsInRole("Mod") || user.IsInRole("Owner"));

    public static string? UserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public static bool CanView(ApplicationUser member, ClaimsPrincipal user) => CanManage(user) || !member.IsLeitao();
}
