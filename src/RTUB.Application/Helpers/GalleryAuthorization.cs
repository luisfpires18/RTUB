using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// The Gallery rules (React tracks 009 and 015, docs/react-gallery.md), enforced server-side by
/// <c>GalleryTimelineService</c> and <c>GalleryManagementService</c>. Roles inherit: Owner includes Admin.
///
/// - Visitors: public items only, no person tags.
/// - Any signed-in member: every item and its tags; uploads (and tags people while doing it).
/// - The uploader, Admin or Owner (was IsInRole("Admin") only): edit and delete an item. Mod: no extra rights.
/// </summary>
public static class GalleryAuthorization
{
    public static bool IsMember(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static string? UserId(ClaimsPrincipal user) =>
        IsMember(user) ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public static bool CanManageAll(ClaimsPrincipal user) => IsMember(user) && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static bool CanEdit(ClaimsPrincipal user, string uploaderId) =>
        CanManageAll(user) || (UserId(user) is { } me && me == uploaderId);
}
