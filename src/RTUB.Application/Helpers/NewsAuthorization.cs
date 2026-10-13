using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// Rights on the public "Novidades" feed (React track 025): everyone, visitors included, reads published posts;
/// Admin and Owner (Owner inherits Admin) see drafts and write, publish, unpublish and delete. Mod does not manage.
/// </summary>
public static class NewsAuthorization
{
    public static bool CanManage(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static string? UserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
