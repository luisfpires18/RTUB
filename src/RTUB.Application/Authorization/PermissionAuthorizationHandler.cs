using Microsoft.AspNetCore.Authorization;
using RTUB.Core.Constants;

namespace RTUB.Application.Authorization;

/// <summary>
/// Handler for PermissionRequirement that checks if user has the required permission claim
/// </summary>
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // Check if user has the required permission claim
        if (context.User.HasClaim(c => 
            c.Type == CustomClaimTypes.Permission && 
            c.Value == requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
