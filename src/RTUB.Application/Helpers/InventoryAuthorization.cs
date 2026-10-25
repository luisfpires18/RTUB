using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The instruments inventory rules (React track 020, docs/react-inventory.md), enforced server-side by
/// <c>InstrumentInventoryService</c>. Same as the Blazor /inventory ([Authorize]; management shown to "Admin,Mod"), with
/// Owner inheriting Admin:
///
/// - Visitors: nothing (401); the page sends them to sign in.
/// - Any signed-in member: the list, the counters and every instrument's details.
/// - Mod, Admin, Owner: create, edit, set the image and delete instruments.
/// - Leitões too (their job is to keep the inventory up to date): checked by category in the service, since it needs the
///   member record, not the claims.
/// </summary>
public static class InventoryAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static string? UserId(ClaimsPrincipal user) => IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public static bool CanManage(ClaimsPrincipal user) =>
        IsMember(user) && (user.IsInRole("Mod") || user.IsInRole("Admin") || user.IsInRole("Owner"));
}
