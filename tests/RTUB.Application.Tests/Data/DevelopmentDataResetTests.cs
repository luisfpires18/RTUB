using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Data;

/// <summary>
/// <c>SeedData.ResetDevDataAsync</c> is destructive: it clears push subscriptions and rewrites
/// every user's password and email. It used to run automatically in every environment except
/// Production, which silently undid the configured seed passwords on the next local startup and
/// would have reset every account on a Staging (Azure DEV) App Service at each restart.
///
/// It is now opt-in and local-Development-only. These tests pin both halves: that nothing is
/// touched by default or outside Development, and that the intentional reset still does its job
/// when it is explicitly switched on.
/// </summary>
public class DevelopmentDataResetTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DevelopmentDataResetTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        _context = new ApplicationDbContext(
            options, httpContextAccessor.Object, new AuditContext(), new AuditLogAppender());

        _userManager = CreateUserManager();

        foreach (var role in new[] { "Owner", "Admin", "Mod", "Member" })
        {
            _context.Roles.Add(new IdentityRole(role)
            {
                NormalizedName = role.ToUpperInvariant()
            });
        }

        _context.SaveChanges();
    }

    // ---- helpers ----

    /// <summary>
    /// A <see cref="UserManager{TUser}"/> over the test context. The password-reset token
    /// provider is registered explicitly because the reset goes through Identity's normal
    /// <c>GeneratePasswordResetToken</c> / <c>ResetPassword</c> flow; the web host gets the same
    /// provider from <c>AddDefaultTokenProviders()</c>.
    /// </summary>
    private UserManager<ApplicationUser> CreateUserManager(
        params IPasswordValidator<ApplicationUser>[] passwordValidators)
    {
        var manager = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser>(_context),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            passwordValidators,
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());

        manager.RegisterTokenProvider(
            TokenOptions.DefaultProvider,
            new DataProtectorTokenProvider<ApplicationUser>(
                new EphemeralDataProtectionProvider(),
                Options.Create(new DataProtectionTokenProviderOptions()),
                Mock.Of<ILogger<DataProtectorTokenProvider<ApplicationUser>>>()));

        return manager;
    }

    private static IHostEnvironment Environment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        return environment.Object;
    }

    private static IConfiguration Configuration(string? enabled, string? resetPassword)
    {
        var values = new Dictionary<string, string?>();

        if (enabled is not null)
        {
            values["DevelopmentDataReset:Enabled"] = enabled;
        }

        if (resetPassword is not null)
        {
            values["DevelopmentDataReset:Password"] = resetPassword;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private Task ResetAsync(
        string environmentName,
        string? enabled,
        string? resetPassword,
        UserManager<ApplicationUser>? userManager = null) =>
        SeedData.ResetDevDataAsync(
            Configuration(enabled, resetPassword),
            _context,
            userManager ?? _userManager,
            Environment(environmentName),
            NullLogger.Instance);

    /// <summary>
    /// One user with a known password and a deliberately non-conforming email, plus one push
    /// subscription. Everything the reset touches is represented, so an "unchanged" assertion is
    /// meaningful.
    /// </summary>
    private async Task<(ApplicationUser User, string Password, string SecurityStamp)> SeedExistingUserAsync(
        string userName = "existing")
    {
        var password = TestSecret.NewPassword();

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@example.com",
            EmailConfirmed = true,
            FirstName = "Existing",
            LastName = "User",
            Nickname = "Existing",
            PhoneNumber = "000000000"
        };

        (await _userManager.CreateAsync(user, password)).Succeeded.Should().BeTrue();

        _context.PushSubscriptions.Add(new PushSubscription
        {
            UserId = user.Id,
            Endpoint = "https://push.example.com/subscription",
            // Generated, not literal: the push keys are credential-shaped and a committed value
            // is indistinguishable from a real one to a secret scanner. Same rule as TestSecret.
            P256dh = Guid.NewGuid().ToString("N"),
            Auth = Guid.NewGuid().ToString("N")
        });
        await _context.SaveChangesAsync();

        return (user, password, user.SecurityStamp!);
    }

    // ---- the reset must not run ----

    /// <summary>
    /// The default path. Production and Staging must never reset whatever the configuration says —
    /// the planned Azure DEV App Service runs as Staging and has to keep its users across
    /// restarts — and Development itself does nothing unless the reset is explicitly enabled.
    /// </summary>
    [Theory]
    [InlineData("Development", "false", "setting present but off")]
    [InlineData("Development", null, "setting absent entirely")]
    [InlineData("Staging", "true", "Staging ignores the opt-in — this is the future Azure DEV App Service")]
    [InlineData("Production", "true", "Production ignores the opt-in")]
    [InlineData("Test", "true", "the test host ignores the opt-in")]
    public async Task ResetDoesNotRun_LeavesCredentialsEmailsAndPushSubscriptionsUntouched(
        string environmentName, string? enabled, string because)
    {
        var (user, password, securityStamp) = await SeedExistingUserAsync();
        var resetPassword = TestSecret.NewPassword();

        await ResetAsync(environmentName, enabled, resetPassword);

        var after = await _userManager.FindByNameAsync("existing");
        after.Should().NotBeNull();

        (await _userManager.CheckPasswordAsync(after!, password)).Should().BeTrue(because);
        (await _userManager.CheckPasswordAsync(after!, resetPassword)).Should().BeFalse(because);
        after!.Email.Should().Be("existing@example.com", because);
        after.SecurityStamp.Should().Be(securityStamp, because);
        (await _context.PushSubscriptions.CountAsync()).Should().Be(1, because);

        user.Should().NotBeNull();
    }

    /// <summary>
    /// Fail closed: the password is validated before the first write, so a missing or placeholder
    /// value can never leave a partially reset database behind. The error names both the
    /// configuration key and its environment-variable form, and never echoes the supplied value.
    /// </summary>
    [Theory]
    [InlineData(null)]          // setting absent entirely
    [InlineData("")]            // set but empty
    [InlineData("   ")]         // whitespace only
    [InlineData("changeme")]    // a documented placeholder
    [InlineData("CHANGEME")]    // placeholder match is case-insensitive
    public async Task Development_EnabledWithoutUsablePassword_ThrowsBeforeAnyMutation(
        string? resetPassword)
    {
        var (_, password, securityStamp) = await SeedExistingUserAsync();

        var act = () => ResetAsync("Development", "true", resetPassword);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.WithMessage("*DevelopmentDataReset:Password*");
        thrown.WithMessage("*DevelopmentDataReset__Password*");

        if (!string.IsNullOrWhiteSpace(resetPassword))
        {
            thrown.Which.Message.Should().NotContain(
                resetPassword, "the supplied value must never appear in an error or a log");
        }

        var after = await _userManager.FindByNameAsync("existing");
        (await _userManager.CheckPasswordAsync(after!, password)).Should().BeTrue();
        after!.Email.Should().Be("existing@example.com");
        after.SecurityStamp.Should().Be(securityStamp);
        (await _context.PushSubscriptions.CountAsync()).Should().Be(1,
            "validation must precede the first mutation");
    }

    // ---- the reset must run ----

    [Fact]
    public async Task Development_EnabledWithValidPassword_ResetsPasswordsEmailsAndPushSubscriptions()
    {
        var (_, oldPassword, securityStamp) = await SeedExistingUserAsync();
        var resetPassword = TestSecret.NewPassword();

        await ResetAsync("Development", "true", resetPassword);

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

    [Fact]
    public async Task Development_EnabledWithValidPassword_ResetsEveryUser()
    {
        await SeedExistingUserAsync("first");
        await SeedExistingUserAsync("second");
        var resetPassword = TestSecret.NewPassword();

        await ResetAsync("Development", "true", resetPassword);

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

    public void Dispose()
    {
        _context.Dispose();
        _userManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
