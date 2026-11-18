using System.Security.Claims;

namespace RTUB.Application.Helpers;

/// <summary>What every area asks of the caller's session first: is someone signed in, and who.</summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>True when the caller is signed in.</summary>
    public static bool IsMember(this ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    /// <summary>The signed-in member's account id; null for a visitor.</summary>
    public static string? UserId(this ClaimsPrincipal user) => user.IsMember() ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
}
