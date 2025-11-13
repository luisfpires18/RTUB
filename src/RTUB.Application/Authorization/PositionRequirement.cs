using Microsoft.AspNetCore.Authorization;
using RTUB.Core.Constants;

namespace RTUB.Application.Authorization;

/// <summary>
/// Requirement that checks if user has a specific position
/// </summary>
public class PositionRequirement : IAuthorizationRequirement
{
    public string Position { get; }

    public PositionRequirement(string position)
    {
        Position = position ?? throw new ArgumentNullException(nameof(position));
    }
}

/// <summary>
/// Handler for PositionRequirement that checks if user has the required position claim
/// </summary>
public class PositionAuthorizationHandler : AuthorizationHandler<PositionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PositionRequirement requirement)
    {
        // Check if user has the required position claim
        if (context.User.HasClaim(c => 
            c.Type == CustomClaimTypes.Position && 
            c.Value == requirement.Position))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
