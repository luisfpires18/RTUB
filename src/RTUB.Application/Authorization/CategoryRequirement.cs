using Microsoft.AspNetCore.Authorization;
using RTUB.Core.Constants;

namespace RTUB.Application.Authorization;

/// <summary>
/// Requirement that checks if user has a specific category
/// </summary>
public class CategoryRequirement : IAuthorizationRequirement
{
    public string Category { get; }

    public CategoryRequirement(string category)
    {
        Category = category ?? throw new ArgumentNullException(nameof(category));
    }
}

/// <summary>
/// Handler for CategoryRequirement that checks if user has the required category claim
/// </summary>
public class CategoryAuthorizationHandler : AuthorizationHandler<CategoryRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CategoryRequirement requirement)
    {
        // Check if user has the required category claim
        if (context.User.HasClaim(c => 
            c.Type == CustomClaimTypes.Category && 
            c.Value == requirement.Category))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
