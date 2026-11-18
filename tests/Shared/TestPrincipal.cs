using System.Security.Claims;

namespace RTUB.Tests;

/// <summary>
/// The caller's session, for tests that call a permission rule directly.
/// Source-linked into every test project by <c>tests/Directory.Build.props</c>.
/// </summary>
internal static class TestPrincipal
{
    /// <summary>Nobody signed in.</summary>
    public static ClaimsPrincipal Visitor() => new(new ClaimsIdentity());

    /// <summary>A signed-in member with these system roles ("Mod", "Admin", "Owner"); none is a plain member.</summary>
    public static ClaimsPrincipal SignedIn(string userId, params string[] roles) => new(new ClaimsIdentity(Claims(userId, roles), "test"));

    /// <summary>The same claims on an identity that is not authenticated: a rule must not trust claims alone.</summary>
    public static ClaimsPrincipal NotAuthenticated(string userId, params string[] roles) => new(new ClaimsIdentity(Claims(userId, roles)));

    private static IEnumerable<Claim> Claims(string userId, string[] roles) =>
        roles.Select(role => new Claim(ClaimTypes.Role, role)).Prepend(new Claim(ClaimTypes.NameIdentifier, userId));
}
