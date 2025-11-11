using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RTUB.Core.Constants;
using RTUB.Core.Entities;
using System.Security.Claims;

namespace RTUB.Application.Identity;

/// <summary>
/// Custom claims principal factory that adds Categories and Positions as claims
/// This factory is invoked automatically when a user signs in, populating their
/// claims from the ApplicationUser's Categories and Positions properties
/// </summary>
public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor)
        : base(userManager, roleManager, optionsAccessor)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        // Generate base claims (includes roles, email, username, name identifier, etc.)
        var identity = await base.GenerateClaimsAsync(user);

        // Add Category claims - each category becomes a separate claim
        foreach (var category in user.Categories)
        {
            identity.AddClaim(new Claim(CustomClaimTypes.Category, category.ToString()));
        }

        // Add Position claims - each position becomes a separate claim
        foreach (var position in user.Positions)
        {
            identity.AddClaim(new Claim(CustomClaimTypes.Position, position.ToString()));
        }

        return identity;
    }
}
