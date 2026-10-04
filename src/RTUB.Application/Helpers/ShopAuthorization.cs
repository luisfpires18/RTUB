using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The shop rules (React track 021, docs/react-shop.md), enforced server-side by <c>ProductShopService</c>. Same as the
/// Blazor /shop ([Authorize]; "Adicionar Produto" shown to "Admin,Mod", edit/delete/reservations to "Admin"), with Owner
/// inheriting Admin:
///
/// - Visitors: nothing (401); the page sends them to sign in.
/// - Any signed-in member: every product, its details; reserve members-only products in stock; cancel their own.
/// - Mod: also add products (with their first image).
/// - Admin, Owner: also edit and delete products, change images, see and delete every reservation.
/// </summary>
public static class ShopAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanCreate(ClaimsPrincipal user) => CanManage(user) || (IsMember(user) && user.IsInRole("Mod"));

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static string? UserId(ClaimsPrincipal user) => IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
