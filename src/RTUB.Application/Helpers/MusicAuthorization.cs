using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Music rules (React track 006, docs/react-music.md), taken from the retired Blazor pages and
/// enforced server-side by <c>MusicService</c>. Owner is checked explicitly wherever Admin is, so an
/// Owner keeps Admin's Music rights even without the Admin role.
///
/// - Albums: Mod, Admin and Owner create and edit; only Admin and Owner delete.
/// - Exclusive albums and their access list: Owner only.
/// - Songs: Mod, Admin and Owner create and edit; only Admin and Owner delete.
/// - Videos: any signed-in member who can see the album uploads; the uploader, Admin or Owner deletes.
/// - Statistics: any signed-in member sees song and album totals; per-member detail is Admin/Owner.
/// </summary>
public static class MusicAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool IsOwner(ClaimsPrincipal user) => IsMember(user) && user.IsInRole("Owner");

    public static bool IsAdmin(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static bool CanManage(ClaimsPrincipal user) => IsAdmin(user) || (IsMember(user) && user.IsInRole("Mod"));

    public static bool CanDelete(ClaimsPrincipal user) => IsAdmin(user);

    public static bool CanManageExclusive(ClaimsPrincipal user) => IsOwner(user);

    public static bool CanSeeDetailedStatistics(ClaimsPrincipal user) => IsAdmin(user);

    public static string? UserId(ClaimsPrincipal user) =>
        IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
