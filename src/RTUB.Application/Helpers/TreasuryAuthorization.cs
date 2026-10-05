using System.Security.Claims;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Tesouraria rules (React track 024, docs/react-treasury.md), enforced server-side by <c>TreasuryService</c> and
/// <c>TreasuryRecordsService</c>. The old pages only hid buttons; three of them (calotes, MBWay, Nerba) had no sign-in at all.
///
/// - Visitors: nothing (401).
/// - Treasury members (<see cref="CanView"/>): Mod, Admin, Owner, and every member who is neither Leitão nor Caloiro (the
///   old /finance sent Caloiros away and showed Leitões nothing). They read reports, receipts, PDFs, every calote, MBWay
///   and Nerba.
/// - Caloiros and Leitões: only their own calotes (the daily push reminds them of those).
/// - Reports, activities, transactions, bank / cash (<see cref="CanManageReports"/>): Admin, Owner, or a Mod of the
///   treasury team (1.º / 2.º Tesoureiro). Publish and delete drafts (<see cref="CanPublish"/>): Owner or the treasury
///   team. Transaction history: Admin and Owner.
/// - Calotes, MBWay add, Nerba (<see cref="CanManageRecords"/>): Mod, Admin, Owner (Owner inherits Admin since 024).
///   MBWay edit and delete: Owner only, as before.
/// </summary>
public static class TreasuryAuthorization
{
    public static string? UserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public static bool IsManager(ClaimsPrincipal user) => user.IsInRole("Admin") || user.IsInRole("Mod") || user.IsInRole("Owner");

    public static bool CanView(ApplicationUser member, ClaimsPrincipal user) => IsManager(user) || (!member.IsLeitao() && !member.IsCaloiro());

    public static bool CanManageReports(ApplicationUser member, ClaimsPrincipal user) =>
        CanView(member, user) && (user.IsInRole("Admin") || user.IsInRole("Owner") || (user.IsInRole("Mod") && member.IsTreasuryTeam()));

    public static bool CanPublish(ApplicationUser member, ClaimsPrincipal user) =>
        CanView(member, user) && (user.IsInRole("Owner") || member.IsTreasuryTeam());

    public static bool CanSeeHistory(ClaimsPrincipal user) => user.IsInRole("Admin") || user.IsInRole("Owner");

    public static bool CanManageRecords(ClaimsPrincipal user) => IsManager(user);

    public static bool CanEditTransfers(ClaimsPrincipal user) => user.IsInRole("Owner");
}
