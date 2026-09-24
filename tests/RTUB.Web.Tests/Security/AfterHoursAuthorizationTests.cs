using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Security;
using Xunit;

namespace RTUB.Web.Tests.Security;

/// <summary>
/// The After Hours gate in isolation: fail-closed default and the handler's single decision.
/// The routed and navigation behaviour is covered by the integration tests.
/// </summary>
public class AfterHoursAuthorizationTests
{
    [Fact]
    public void Options_DefaultToDisabled()
    {
        new AfterHoursOptions().Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Handler_SucceedsOnlyWhenEnabled(bool enabled, bool expected)
    {
        var options = new Mock<IOptionsMonitor<AfterHoursOptions>>();
        options.Setup(o => o.CurrentValue).Returns(new AfterHoursOptions { Enabled = enabled });
        var requirement = new AfterHoursEnabledRequirement();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "member")], "Test"));
        var context = new AuthorizationHandlerContext([requirement], user, resource: null);

        await new AfterHoursEnabledHandler(options.Object).HandleAsync(context);

        context.HasSucceeded.Should().Be(expected);
    }
}
