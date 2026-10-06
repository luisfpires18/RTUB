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

    public AccountController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet("me")]
    [AllowAnonymous]
    public async Task<IActionResult> Me()
    {
        // Session state must never be served from a cache: not after sign-out, not to another user.
        Response.Headers.CacheControl = "no-store";

        var user = User.Identity?.IsAuthenticated == true ? await _userManager.GetUserAsync(User) : null;

        // Expelled or deleted members arrive here anonymous: the cookie validator
        // (AddCookieAuthenticationServices) rejects their session on every request.
        if (user is null)
        {
            return Ok(new { authenticated = false });
        }

        var fullName = $"{user.FirstName} {user.LastName}".Trim();

        return Ok(new
        {
            authenticated = true,
            displayName = FirstNonBlank(user.Nickname, user.FirstName, user.UserName),
            fullName = fullName.Length > 0 ? fullName : null,
            avatarUrl = user.ProfilePictureSrc,
            categories = user.Categories.Select(StatusHelper.GetCategoryDisplay).ToArray(),
            menu = MemberMenuAccess.For(User, user)
        });
    }

    private static string FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "Tuno";
}
