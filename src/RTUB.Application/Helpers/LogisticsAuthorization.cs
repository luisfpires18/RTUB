using System.Security.Claims;
using RTUB.Core.Entities;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Logistics rules (docs/react-logistics.md), enforced server-side
/// by <c>LogisticsKanbanService</c>:
///
/// - Visitors: nothing; the pages send them to sign in.
/// - Any signed-in member, Leitões included (expelled accounts never get a session; refused here too): every
///   board, list, card face and card details, board file downloads; create reminders (stored).
/// - A member a card is assigned to ("Atribuir a" or "Membros atribuídos"): also ticks that card's checklist items and
///   changes its status. Nothing else; checked against the database on every write.
/// - Mod, Admin and Owner (Owner inherits Admin): everything else - boards, lists, cards, labels, checklist
///   text, links, assignments, board files upload / delete, moving cards and lists.
/// </summary>
public static class LogisticsAuthorization
{
    public static bool CanManage(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && (user.IsInRole("Admin") || user.IsInRole("Mod") || user.IsInRole("Owner"));

    public static string? UserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public static bool CanView(ApplicationUser member, ClaimsPrincipal user) => CanManage(user) || !member.IsExpelled;
}
