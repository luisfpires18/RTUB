// CookieValidationTests: Rejections tests.
using System.Data.Common;
using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Integration.Tests;

public partial class CookieValidationTests
{
    // ------------------------------------------------------------------------ security checks

    [Fact]
    public async Task SecurityStampChange_RejectsCookie()
    {
        var (client, user) = await SignInNewUserAsync("sec-stamp", "10.30.0.5");

        // Arm the activity-write throttle first: the security checks must still reject on a
        // request that would skip the LastLoginDate write.
        (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(user.Id);
            (await userManager.UpdateSecurityStampAsync(stored!)).Succeeded.Should().BeTrue();
        }

        await AssertCookieRejectedAsync(client);
    }

    [Fact]
    public async Task ExpelledUser_RejectsCookie()
    {
        var (client, user) = await SignInNewUserAsync("sec-expelled", "10.30.0.6");

        // Arm the activity-write throttle first: the security checks must still reject on a
        // request that would skip the LastLoginDate write.
        (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(user.Id);
            stored!.IsExpelled = true;
            // UpdateAsync does not touch the security stamp, so this exercises the expulsion
            // branch specifically and not the stamp branch above it.
            (await userManager.UpdateAsync(stored)).Succeeded.Should().BeTrue();
        }

        await AssertCookieRejectedAsync(client);
    }

    [Fact]
    public async Task AdminRoleRemoved_RejectsCookie()
    {
        var (client, user) = await SignInNewUserAsync("sec-admin", "10.30.0.7", role: "Admin");

        // Arm the activity-write throttle first: the security checks must still reject on a
        // request that would skip the LastLoginDate write.
        (await client.GetAsync("/Events")).StatusCode.Should().Be(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var stored = await userManager.FindByIdAsync(user.Id);
            // RemoveFromRoleAsync leaves the security stamp alone, so the stock
            // SecurityStampValidator would NOT catch this — only RTUB's own role probe does.
            (await userManager.RemoveFromRoleAsync(stored!, "Admin")).Succeeded.Should().BeTrue();
        }

        await AssertCookieRejectedAsync(client);
    }
}
