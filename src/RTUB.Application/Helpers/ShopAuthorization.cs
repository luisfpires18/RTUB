using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The shop rules (React track 021, docs/react-shop.md), enforced server-side by <c>ProductShopService</c>. Same as the
/// Blazor /shop ([Authorize]; "Adicionar Produto" shown to "Admin,Mod", edit/delete/reservations to "Admin"), with Owner
/// inheriting Admin:
///
/// - Visitors: the public product catalogue only; member detail, reservation and management queries still require a
///   signed-in member.
/// - Any signed-in member: every product, its details; reserve members-only products in stock; cancel their own.
/// - Mod: also add and edit products, change images, promotions, and see and delete every reservation
///   (<see cref="CanManageReservations"/>).
/// - Admin, Owner: also delete products.
/// </summary>
public static class ShopAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool CanCreate(ClaimsPrincipal user) => CanManage(user) || (IsMember(user) && user.IsInRole("Mod"));

    /// <summary>See a product's reservations and delete anyone's: Mod and above.</summary>
    public static bool CanManageReservations(ClaimsPrincipal user) => CanCreate(user);

    public static bool CanManage(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static string? UserId(ClaimsPrincipal user) => IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
