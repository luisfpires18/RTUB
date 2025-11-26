using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for UserRoleQueryService
/// Tests business logic for user role queries
/// </summary>
public class UserRoleQueryServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly UserRoleQueryService _service;

    public UserRoleQueryServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _service = new UserRoleQueryService(_context);
    }

    [Fact]
    public async Task GetUserRolesAsync_WithEmptyUserIdList_ReturnsEmptyList()
    {
        // Arrange
        var userIds = new List<string>();

        // Act
        var result = await _service.GetUserRolesAsync(userIds);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_WithNullUserIdList_ReturnsEmptyList()
    {
        // Act
        var result = await _service.GetUserRolesAsync(null!);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_WithNoMatchingRoles_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId}@test.com",
            Email = $"user_{userId}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "User",
            PhoneNumber = "123456789"
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var userIds = new List<string> { user.Id };

        // Act
        var result = await _service.GetUserRolesAsync(userIds);

        // Assert
        result.Should().BeEmpty(); // User has no roles assigned
    }

    [Fact]
    public async Task GetUserRolesAsync_WithSingleUserAndSingleRole_ReturnsCorrectData()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId}@test.com",
            Email = $"user_{userId}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "User",
            PhoneNumber = "123456789"
        };

        _context.Users.Add(user);

        var role = new IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Admin",
            NormalizedName = "ADMIN"
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var userRole = new IdentityUserRole<string>
        {
            UserId = user.Id,
            RoleId = role.Id
        };

        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();

        var userIds = new List<string> { user.Id };

        // Act
        var result = await _service.GetUserRolesAsync(userIds);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].UserId.Should().Be(user.Id);
        result[0].RoleName.Should().Be("Admin");
    }

    [Fact]
    public async Task GetUserRolesAsync_WithSingleUserAndMultipleRoles_ReturnsAllRoles()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId}@test.com",
            Email = $"user_{userId}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "User",
            PhoneNumber = "123456789"
        };

        _context.Users.Add(user);

        var adminRole = new IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Admin",
            NormalizedName = "ADMIN"
        };

        var moderatorRole = new IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Moderator",
            NormalizedName = "MODERATOR"
        };

        _context.Roles.AddRange(adminRole, moderatorRole);
        await _context.SaveChangesAsync();

        var userRole1 = new IdentityUserRole<string>
        {
            UserId = user.Id,
            RoleId = adminRole.Id
        };

        var userRole2 = new IdentityUserRole<string>
        {
            UserId = user.Id,
            RoleId = moderatorRole.Id
        };

        _context.UserRoles.AddRange(userRole1, userRole2);
        await _context.SaveChangesAsync();

        var userIds = new List<string> { user.Id };

        // Act
        var result = await _service.GetUserRolesAsync(userIds);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.UserId == user.Id && r.RoleName == "Admin");
        result.Should().Contain(r => r.UserId == user.Id && r.RoleName == "Moderator");
    }

    [Fact]
    public async Task GetUserRolesAsync_WithMultipleUsersAndRoles_ReturnsAllCorrectCombinations()
    {
        // Arrange
        var userId1 = Guid.NewGuid().ToString();
        var userId2 = Guid.NewGuid().ToString();
        var userId3 = Guid.NewGuid().ToString();

        var user1 = new ApplicationUser
        {
            Id = userId1,
            UserName = $"user1_{userId1}@test.com",
            Email = $"user1_{userId1}@test.com",
            FirstName = "Test",
            LastName = "User1",
            Nickname = "User1",
            PhoneNumber = "123456789"
        };

        var user2 = new ApplicationUser
        {
            Id = userId2,
            UserName = $"user2_{userId2}@test.com",
            Email = $"user2_{userId2}@test.com",
            FirstName = "Test",
            LastName = "User2",
            Nickname = "User2",
            PhoneNumber = "987654321"
        };

        var user3 = new ApplicationUser
        {
            Id = userId3,
            UserName = $"user3_{userId3}@test.com",
            Email = $"user3_{userId3}@test.com",
            FirstName = "Test",
            LastName = "User3",
            Nickname = "User3",
            PhoneNumber = "111222333"
        };

        _context.Users.AddRange(user1, user2, user3);

        var adminRole = new IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Admin",
            NormalizedName = "ADMIN"
        };

        var moderatorRole = new IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Moderator",
            NormalizedName = "MODERATOR"
        };

        var memberRole = new IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Member",
            NormalizedName = "MEMBER"
        };

        _context.Roles.AddRange(adminRole, moderatorRole, memberRole);
        await _context.SaveChangesAsync();

        // User1: Admin + Member
        _context.UserRoles.Add(new IdentityUserRole<string> { UserId = user1.Id, RoleId = adminRole.Id });
        _context.UserRoles.Add(new IdentityUserRole<string> { UserId = user1.Id, RoleId = memberRole.Id });

        // User2: Moderator
        _context.UserRoles.Add(new IdentityUserRole<string> { UserId = user2.Id, RoleId = moderatorRole.Id });

        // User3: Member
        _context.UserRoles.Add(new IdentityUserRole<string> { UserId = user3.Id, RoleId = memberRole.Id });

        await _context.SaveChangesAsync();

        var userIds = new List<string> { user1.Id, user2.Id, user3.Id };

        // Act
        var result = await _service.GetUserRolesAsync(userIds);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(4); // Total of 4 user-role combinations

        // Verify User1 has 2 roles
        var user1Roles = result.Where(r => r.UserId == user1.Id).ToList();
        user1Roles.Should().HaveCount(2);
        user1Roles.Should().Contain(r => r.RoleName == "Admin");
        user1Roles.Should().Contain(r => r.RoleName == "Member");

        // Verify User2 has 1 role
        var user2Roles = result.Where(r => r.UserId == user2.Id).ToList();
        user2Roles.Should().HaveCount(1);
        user2Roles.Should().Contain(r => r.RoleName == "Moderator");

        // Verify User3 has 1 role
        var user3Roles = result.Where(r => r.UserId == user3.Id).ToList();
        user3Roles.Should().HaveCount(1);
        user3Roles.Should().Contain(r => r.RoleName == "Member");
    }

    [Fact]
    public async Task GetUserRolesAsync_WithUserIdsNotInDatabase_ReturnsEmptyList()
    {
        // Arrange
        var userIds = new List<string> { "nonexistent1", "nonexistent2" };

        // Act
        var result = await _service.GetUserRolesAsync(userIds);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_WithMixOfExistingAndNonExistingUsers_ReturnsOnlyExistingUserRoles()
    {
        // Arrange
        var userId = Guid.NewGuid().ToString();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"user_{userId}@test.com",
            Email = $"user_{userId}@test.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "User",
            PhoneNumber = "123456789"
        };

        _context.Users.Add(user);

        var role = new IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Admin",
            NormalizedName = "ADMIN"
        };

        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var userRole = new IdentityUserRole<string>
        {
            UserId = user.Id,
            RoleId = role.Id
        };

        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();

        var userIds = new List<string> { user.Id, "nonexistent" };

        // Act
        var result = await _service.GetUserRolesAsync(userIds);

        // Assert
        result.Should().HaveCount(1);
        result[0].UserId.Should().Be(user.Id);
        result[0].RoleName.Should().Be("Admin");
    }

    [Fact]
    public async Task Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new UserRoleQueryService(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    public void Dispose()
    {
        _context?.Dispose();
        GC.SuppressFinalize(this);
    }
}
