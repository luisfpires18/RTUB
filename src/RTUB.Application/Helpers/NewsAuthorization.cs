using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>
/// Rights on the public "Novidades" feed (React track 025): everyone, visitors included, reads published posts;
/// Admin and Owner (Owner inherits Admin) see drafts and write, publish, unpublish and delete. Mod
/// (<see cref="CanWrite"/>) does the same except delete, which stays with Admin / Owner.
/// </summary>
public static class NewsAuthorization
{
    public static bool CanWrite(ClaimsPrincipal user) =>
        CanManage(user) || (user.Identity?.IsAuthenticated == true && user.IsInRole("Mod"));

    public static bool CanManage(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true && (user.IsInRole("Admin") || user.IsInRole("Owner"));

    public static string? UserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
