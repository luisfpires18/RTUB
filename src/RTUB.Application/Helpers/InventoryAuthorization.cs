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
/// </summary>
public static class InventoryAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanManage(ClaimsPrincipal user) =>
        IsMember(user) && (user.IsInRole("Mod") || user.IsInRole("Admin") || user.IsInRole("Owner"));
}
