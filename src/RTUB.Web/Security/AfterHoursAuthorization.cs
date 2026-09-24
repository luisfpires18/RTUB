using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;

namespace RTUB.Security;

/// <summary>
/// The After Hours feature gate: one authorization policy that requires an authenticated user
/// <b>and</b> <see cref="AfterHoursOptions.Enabled"/>. Every After Hours page inherits it from
/// <c>Pages/AfterHours/_Imports.razor</c>, and the navigation entry uses the same policy, so the
/// route and the menu can never disagree. See docs/after_hours/README.md.
/// </summary>
public static class AfterHoursAuthorization
{
    public const string Policy = "AfterHours";
}

/// <summary>
/// Satisfied only while After Hours is enabled in configuration.
/// </summary>
public sealed class AfterHoursEnabledRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Reads the feature state per evaluation, so a missing or false setting fails closed.
/// </summary>
public sealed class AfterHoursEnabledHandler(IOptionsMonitor<AfterHoursOptions> options)
    : AuthorizationHandler<AfterHoursEnabledRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AfterHoursEnabledRequirement requirement)
    {
        if (options.CurrentValue.Enabled)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
