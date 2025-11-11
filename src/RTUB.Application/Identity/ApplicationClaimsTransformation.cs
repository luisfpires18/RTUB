using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Core.Constants;
using RTUB.Core.Entities;
using System.Security.Claims;

namespace RTUB.Application.Identity;

/// <summary>
/// Claims transformation service that refreshes user claims from the database
/// when they are outdated. Uses caching to minimize database queries.
/// </summary>
public class ApplicationClaimsTransformation : IClaimsTransformation
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);

    public ApplicationClaimsTransformation(
        UserManager<ApplicationUser> userManager,
        IMemoryCache cache)
    {
        _userManager = userManager;
        _cache = cache;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        // Only transform authenticated users
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return principal;
        }

        // Check if we have cached transformed claims
        var cacheKey = $"claims_{userId}";
        if (_cache.TryGetValue(cacheKey, out ClaimsPrincipal? cachedPrincipal) && cachedPrincipal != null)
        {
            return cachedPrincipal;
        }

        // Load user from database
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return principal;
        }

        // Create new identity with fresh claims
        var identity = new ClaimsIdentity(principal.Identity);

        // Remove old Category and Position claims
        var oldClaims = identity.Claims
            .Where(c => c.Type == CustomClaimTypes.Category || c.Type == CustomClaimTypes.Position)
            .ToList();

        foreach (var claim in oldClaims)
        {
            identity.RemoveClaim(claim);
        }

        // Add fresh Category claims
        foreach (var category in user.Categories)
        {
            identity.AddClaim(new Claim(CustomClaimTypes.Category, category.ToString()));
        }

        // Add fresh Position claims
        foreach (var position in user.Positions)
        {
            identity.AddClaim(new Claim(CustomClaimTypes.Position, position.ToString()));
        }

        var transformedPrincipal = new ClaimsPrincipal(identity);

        // Cache the transformed principal
        _cache.Set(cacheKey, transformedPrincipal, _cacheDuration);

        return transformedPrincipal;
    }

    /// <summary>
    /// Invalidates the cached claims for a specific user
    /// Should be called when user's categories or positions are updated
    /// </summary>
    public void InvalidateUserClaims(string userId)
    {
        var cacheKey = $"claims_{userId}";
        _cache.Remove(cacheKey);
    }
}
