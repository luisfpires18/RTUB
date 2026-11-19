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
/// Production, which silently undid the configured seed passwords on the next local startup.
/// Unit 017 made it opt-in and restricted it to Development.
///
/// Unit 027 widened the allow-list to <b>Development and Staging</b>, because Staging is the
/// Azure DEV App Service and its users exist precisely to be reset to one shared development
/// password on demand. Production and every other environment remain a hard no-op.
///
/// These tests pin all of it: that the allowed environments do the work when explicitly switched
/// on, and that nothing is touched by default, outside the allow-list, or without a usable
/// password.
/// </summary>
public partial class DevelopmentDataResetTests : IDisposable
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
    /// The default path. Production must never reset whatever the configuration says, and neither
    /// must any environment outside the Development/Staging allow-list. Inside the allow-list,
    /// nothing happens unless the reset is explicitly enabled.
    ///
    /// The guard is an allow-list rather than "not Production", so an environment name nobody
    /// anticipated is inert by default — <c>QA</c> below stands for that whole class.
    /// </summary>
    [Theory]
    [InlineData("Development", "false", "setting present but off")]
    [InlineData("Development", null, "setting absent entirely")]
    [InlineData("Staging", "false", "Staging is allowed to reset, but only when switched on")]
    [InlineData("Staging", null, "Staging with the setting absent must not reset")]
    [InlineData("Staging", "not-a-bool", "an unparseable opt-in is not an opt-in")]
    [InlineData("Production", "true", "Production ignores the opt-in")]
    [InlineData("Test", "true", "the test host ignores the opt-in")]
    [InlineData("QA", "true", "an environment outside the allow-list ignores the opt-in")]
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
    [InlineData("Development", null)]        // setting absent entirely
    [InlineData("Development", "")]          // set but empty
    [InlineData("Development", "   ")]       // whitespace only
    [InlineData("Development", "changeme")]  // a documented placeholder
    [InlineData("Development", "CHANGEME")]  // placeholder match is case-insensitive
    [InlineData("Staging", null)]            // the Azure DEV case: fail closed there too
    [InlineData("Staging", "changeme")]
    public async Task EnabledWithoutUsablePassword_ThrowsBeforeAnyMutation(
        string environmentName, string? resetPassword)
    {
        var (_, password, securityStamp) = await SeedExistingUserAsync();

        var act = () => ResetAsync(environmentName, "true", resetPassword);

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

    public void Dispose()
    {
        _context.Dispose();
        _userManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
