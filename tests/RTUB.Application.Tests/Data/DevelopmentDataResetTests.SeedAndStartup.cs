using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Data;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Data;

// DevelopmentDataResetTests: Seed And Startup tests.
public partial class DevelopmentDataResetTests
{
    // ---- the reset must run ----

    /// <summary>
    /// Both allowed environments do the full job. Staging is the one that matters operationally —
    /// it is the Azure DEV App Service, and this is the path that resets its existing users to a
    /// single shared development password.
    /// </summary>
    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public async Task EnabledWithValidPassword_ResetsPasswordsEmailsAndPushSubscriptions(
        string environmentName)
    {
        var (_, oldPassword, securityStamp) = await SeedExistingUserAsync();
        var resetPassword = TestSecret.NewPassword();

        await ResetAsync(environmentName, "true", resetPassword);

        var after = await _userManager.FindByNameAsync("existing");
        after.Should().NotBeNull();

        (await _userManager.CheckPasswordAsync(after!, resetPassword)).Should().BeTrue(
            "the configured reset password is what every account gets");
        (await _userManager.CheckPasswordAsync(after!, oldPassword)).Should().BeFalse(
            "the previous password must stop working");

        after!.Email.Should().Be("existing@rtub.pt");
        after.NormalizedEmail.Should().Be("EXISTING@RTUB.PT");
        after.EmailConfirmed.Should().BeTrue(
            "normalising the address must not lock the account out of a confirmed-account sign-in");

        after.SecurityStamp.Should().NotBe(securityStamp,
            "rotating the stamp invalidates authentication cookies issued before the reset");

        (await _context.PushSubscriptions.AnyAsync()).Should().BeFalse(
            "stale browser subscriptions are cleared by the reset");

        after.PasswordHash.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Every user, not just the first. This is the Azure DEV case in miniature: that database has
    /// 83 seeded members, and the point of the reset is that all of them end up on one password.
    /// </summary>
    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public async Task EnabledWithValidPassword_ResetsEveryUser(string environmentName)
    {
        await SeedExistingUserAsync("first");
        await SeedExistingUserAsync("second");
        var resetPassword = TestSecret.NewPassword();

        await ResetAsync(environmentName, "true", resetPassword);

        foreach (var userName in new[] { "first", "second" })
        {
            var user = await _userManager.FindByNameAsync(userName);
            (await _userManager.CheckPasswordAsync(user!, resetPassword)).Should().BeTrue();
            user!.Email.Should().Be($"{userName}@rtub.pt");
        }

        (await _context.PushSubscriptions.AnyAsync()).Should().BeFalse();
    }

    /// <summary>
    /// A failed reset must be surfaced, and must not damage the account. Identity's
    /// <c>ResetPasswordAsync</c> validates before it writes, so a rejected password leaves the
    /// existing hash in place — which is exactly why the reset is one call and not
    /// <c>RemovePasswordAsync</c> followed by <c>AddPasswordAsync</c>: a failure between those
    /// two would leave the user with no password at all.
    /// </summary>
    [Fact]
    public async Task Development_WhenIdentityRejectsTheReset_ThrowsAndLeavesTheAccountUsable()
    {
        var (_, oldPassword, securityStamp) = await SeedExistingUserAsync();
        var resetPassword = TestSecret.NewPassword();

        var rejecting = new Mock<IPasswordValidator<ApplicationUser>>();
        rejecting
            .Setup(v => v.ValidateAsync(
                It.IsAny<UserManager<ApplicationUser>>(),
                It.IsAny<ApplicationUser>(),
                It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordRejectedByTest",
                Description = "Password rejected by the test validator."
            }));

        using var strictManager = CreateUserManager(rejecting.Object);

        var act = () => ResetAsync("Development", "true", resetPassword, strictManager);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.WithMessage("*PasswordRejectedByTest*",
            "the Identity failure must be surfaced, not silently ignored");
        thrown.WithMessage("*existing*", "the message names the affected account");
        thrown.Which.Message.Should().NotContain(
            resetPassword, "the supplied value must never appear in an error or a log");

        var after = await _userManager.FindByNameAsync("existing");
        after!.PasswordHash.Should().NotBeNullOrWhiteSpace(
            "a rejected reset must never leave an account without a password");
        (await _userManager.CheckPasswordAsync(after, oldPassword)).Should().BeTrue(
            "the existing password is still the account's password after a rejected reset");
        after.SecurityStamp.Should().Be(securityStamp);
        after.Email.Should().Be("existing@example.com",
            "the email is normalised only after the password reset succeeds");
    }

    // ---- interaction with unit 016's configured seed passwords ----

    /// <summary>
    /// The regression this unit exists for. Unit 016 made the full seed create the Owner from
    /// <c>AdminUser:Password</c> and every member from <c>SeedData:MemberPassword</c>, kept
    /// separate — and the next non-Production startup used to overwrite all of it with one shared
    /// hardcoded hash. A default startup must now leave both configured passwords working.
    ///
    /// This runs the real bulk seed (the <c>isEmptyDb = false</c> path) rather than a hand-built
    /// user, so it is the genuine post-016 state, then replays what startup does next.
    /// </summary>
    [Fact]
    public async Task FullSeed_ThenDefaultDevelopmentStartup_KeepsBothConfiguredSeedPasswords()
    {
        var adminPassword = TestSecret.NewPassword();
        var memberPassword = TestSecret.NewPassword();

        var seedConfiguration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AdminUser:Username"] = "seedowner",
                ["AdminUser:Email"] = "seedowner@example.com",
                ["AdminUser:Password"] = adminPassword,
                ["SeedData:MemberPassword"] = memberPassword,
            }).Build();

        await SeedData.SeedMembersAsync(seedConfiguration, _context, _userManager, isEmptyDb: false);

        // What InitializeAsync does on the next Development startup, with the reset left at its
        // default (no DevelopmentDataReset section configured at all).
        await ResetAsync("Development", enabled: null, resetPassword: null);

        var owner = await _userManager.FindByNameAsync("seedowner");
        (await _userManager.CheckPasswordAsync(owner!, adminPassword)).Should().BeTrue(
            "the Owner's configured bootstrap password must survive a restart");
        owner!.Email.Should().Be("seedowner@example.com",
            "a default startup must not rewrite emails either");

        var member = await _userManager.FindByNameAsync("nabo");
        (await _userManager.CheckPasswordAsync(member!, memberPassword)).Should().BeTrue(
            "the members' configured seed password must survive a restart");
    }
}
