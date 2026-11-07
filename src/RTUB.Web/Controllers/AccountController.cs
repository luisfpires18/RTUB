using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RTUB.Application.Helpers;
using ApplicationUser = RTUB.Core.Entities.ApplicationUser;

namespace RTUB.Controllers;

/// <summary>
/// Session state for the React portal (React track 002, docs/react-portal-pilot.md).
/// Read-only and about the caller only: never another user, never contact details, dates of
/// birth or anything else a member's own profile page does not already show them. Roles are not
/// listed; <c>menu</c> (task 030) says only which member-menu groups to show the caller, as the
/// Blazor navbar already did (<see cref="MemberMenuAccess"/>).
/// Signing in and out stay with the antiforgery-protected /auth endpoints (React /login posts there).
/// </summary>
[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly string? _publicOrigin;
    private readonly RTUB.Application.Services.PageViewCounter _pageViews;

    public AccountController(UserManager<ApplicationUser> userManager, IConfiguration configuration, RTUB.Application.Services.PageViewCounter pageViews)
    {
        _userManager = userManager;
        _publicOrigin = TrustedOrigin(configuration["Calendar:PublicOrigin"]);
        _pageViews = pageViews;
    }

    [HttpGet("me")]
    [AllowAnonymous]
    public async Task<IActionResult> Me()
    {
        // Session state must never be served from a cache: not after sign-out, not to another user.
        Response.Headers.CacheControl = "no-store";


        // The build, as /api/version reports it: the footer reads it from here, one request fewer per page load (task 054).
        var version = RTUB.Web.Services.BuildInfo.Current.Version;
        var user = User.Identity?.IsAuthenticated == true ? await _userManager.GetUserAsync(User) : null;

        // Expelled or deleted members arrive here anonymous: the cookie validator
        // (AddCookieAuthenticationServices) rejects their session on every request.
        if (user is null)
        {
            return Ok(new { authenticated = false, version });
        }

        // Task 089: every React page asks here once per load; the Referer is the page. Members only (a visitor cannot
        // grow the buffer), counted in memory, path only.
        _pageViews.Record(Request.Headers.Referer.ToString(), Request.Host.Value);

        var fullName = $"{user.FirstName} {user.LastName}".Trim();

        return Ok(new
        {
            authenticated = true,
            version,
            displayName = FirstNonBlank(user.Nickname, user.FirstName, user.UserName),
            fullName = fullName.Length > 0 ? fullName : null,
            avatarUrl = user.ProfilePictureSrc,
            categories = user.Categories.Select(StatusHelper.GetCategoryDisplay).ToArray(),
            menu = MemberMenuAccess.For(User, user),
            // Task 087: a Reformado has no "As minhas ações pendentes" (no menu entry, no page, no reminder push).
            retired = user.IsRetired,
            // Task 079: the configured public origin for links that leave RTUB (Google Calendar). Never the request's host.
            publicOrigin = _publicOrigin
        });
    }

    /// <summary>An absolute https origin (http only for localhost), normalized to scheme://host[:port]; else null.</summary>
    public static string? TrustedOrigin(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
            && uri.AbsolutePath == "/" && uri.Query == "" && uri.Fragment == "" && uri.UserInfo == ""
            ? uri.GetLeftPart(UriPartial.Authority)
            : null;

    private static string FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "Tuno";
}
