using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Data.Builders;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Data;

/// <summary>
/// The Owner bootstrap must fail closed. On an empty database the Owner is created only from an
/// externally supplied <c>AdminUser:Password</c>; there is no fallback default, so a missing or
/// placeholder value must leave the database without a privileged account rather than create one
/// with a password anybody can guess. An existing database must still start without the setting.
/// </summary>
public class SeedDataBootstrapTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public SeedDataBootstrapTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        _context = new ApplicationDbContext(
            options, httpContextAccessor.Object, new AuditContext(), new AuditLogAppender());

        _userManager = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser>(_context),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            Mock.Of<ILogger<UserManager<ApplicationUser>>>());

        foreach (var role in new[] { "Owner", "Admin", "Mod", "Member" })
        {
            _context.Roles.Add(new IdentityRole(role)
            {
                NormalizedName = role.ToUpperInvariant()
            });
        }

        _context.SaveChanges();
    }

    private static IConfiguration Configuration(string? adminPassword, string? memberPassword = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["AdminUser:Username"] = "bootstrapowner",
            ["AdminUser:Email"] = "bootstrapowner@example.com",
        };

        if (adminPassword is not null)
        {
            values["AdminUser:Password"] = adminPassword;
        }

        if (memberPassword is not null)
        {
            values["SeedData:MemberPassword"] = memberPassword;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private Task SeedAsync(IConfiguration configuration, bool isEmptyDb = true) =>
        SeedData.SeedMembersAsync(configuration, _context, _userManager, isEmptyDb);

    [Theory]
    [InlineData(null)]          // setting absent entirely
    [InlineData("")]            // set but empty
    [InlineData("   ")]         // whitespace only
    [InlineData("your-admin-password")] // the README placeholder
    [InlineData("changeme")]
    [InlineData("CHANGEME")]    // placeholder match is case-insensitive
    public async Task EmptyDatabase_WithoutUsableAdminPassword_CreatesNoOwnerAndFailsClearly(
        string? adminPassword)
    {
        var act = () => SeedAsync(Configuration(adminPassword));

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*AdminUser:Password*");

        (await _context.Users.AnyAsync()).Should().BeFalse(
            "no privileged account may be created without an externally supplied password");
    }

    [Fact]
    public async Task EmptyDatabase_WithSuppliedAdminPassword_CreatesOwnerWithBothRoles()
    {
        var password = TestSecret.NewPassword();

        await SeedAsync(Configuration(password));

        var owner = await _userManager.FindByNameAsync("bootstrapowner");
        owner.Should().NotBeNull();
        owner!.Email.Should().Be("bootstrapowner@example.com");

        (await _userManager.CheckPasswordAsync(owner, password)).Should().BeTrue();
        (await _userManager.IsInRoleAsync(owner, "Owner")).Should().BeTrue();
        (await _userManager.IsInRoleAsync(owner, "Admin")).Should().BeTrue();

        // The empty-database bootstrap seeds the Owner and stops.
        (await _context.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task PopulatedDatabase_DoesNotRequireBootstrapPassword()
    {
        var existing = new ApplicationUser
        {
            UserName = "bootstrapowner",
            Email = "bootstrapowner@example.com",
            EmailConfirmed = true,
            FirstName = "Existing",
            LastName = "Owner",
            Nickname = "Existing",
            PhoneNumber = "000000000"
        };

        (await _userManager.CreateAsync(existing, TestSecret.NewPassword()))
            .Succeeded.Should().BeTrue();

        var act = () => SeedAsync(Configuration(adminPassword: null));

        await act.Should().NotThrowAsync(
            "an existing account must never be re-bootstrapped, so no password is needed");

        (await _context.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task MemberBuilder_WithoutExplicitPassword_CreatesNoUserAndFailsClearly()
    {
        var act = () => new MemberBuilder(_userManager)
            .Nickname("Sem Senha")
            .Name("Sem", "Senha")
            .Role("Member")
            .CreateAsync();

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*no password was supplied*");

        (await _context.Users.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task MemberBuilder_WithExplicitPassword_CreatesTheUser()
    {
        var password = TestSecret.NewPassword();

        var user = await new MemberBuilder(_userManager)
            .Nickname("Com Senha")
            .Name("Com", "Senha")
            .Role("Member")
            .Password(password)
            .CreateAsync();

        user.Should().NotBeNull();
        (await _userManager.CheckPasswordAsync(user!, password)).Should().BeTrue();
    }

    /// <summary>
    /// The bulk member seed is switched on by flipping <c>isEmptyDb</c> to <c>false</c> by hand
    /// when a full development database is wanted. It must refuse to run without a configured
    /// member password, and refuse *before* writing anything — a half-seeded database would be
    /// skipped by <c>InitializeAsync</c> on the next start, which returns as soon as any user
    /// exists.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("changeme")]
    public async Task BulkSeed_WithoutUsableMemberPassword_WritesNothingAndFailsClearly(
        string? memberPassword)
    {
        var configuration = Configuration(TestSecret.NewPassword(), memberPassword);

        var act = () => SeedAsync(configuration, isEmptyDb: false);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*SeedData:MemberPassword*");

        (await _context.Users.AnyAsync()).Should().BeFalse(
            "the member password is validated before the Owner is created, so a failed bulk "
            + "seed leaves no partially seeded database behind");
    }

    [Fact]
    public async Task BulkSeed_WithMemberPassword_SeedsOwnerAndMembersWithThatPassword()
    {
        var adminPassword = TestSecret.NewPassword();
        var memberPassword = TestSecret.NewPassword();

        await SeedAsync(Configuration(adminPassword, memberPassword), isEmptyDb: false);

        var owner = await _userManager.FindByNameAsync("bootstrapowner");
        owner.Should().NotBeNull();
        (await _userManager.CheckPasswordAsync(owner!, adminPassword)).Should().BeTrue(
            "the Owner keeps its own password, separate from the seeded members");

        (await _context.Users.CountAsync()).Should().BeGreaterThan(1,
            "the bulk member seed must actually run when isEmptyDb is false");

        var member = await _userManager.FindByNameAsync("nabo");
        member.Should().NotBeNull();
        (await _userManager.CheckPasswordAsync(member!, memberPassword)).Should().BeTrue();
        (await _userManager.IsInRoleAsync(member!, "Member")).Should().BeTrue();
    }

    public void Dispose()
    {
        _context.Dispose();
        _userManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
