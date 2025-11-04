using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using System.Security.Claims;

namespace RTUB.Application.Tests.Data;

/// <summary>
/// Unit tests for ApplicationDbContext
/// Tests automatic CreatedBy and UpdatedBy field population
/// </summary>
public class ApplicationDbContextTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly string _testUsername = "testuser";

    public ApplicationDbContextTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        SetupMockUser(_testUsername);

        _context = new ApplicationDbContext(options, _httpContextAccessorMock.Object);
    }

    private void SetupMockUser(string username)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(x => x.User).Returns(claimsPrincipal);

        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContextMock.Object);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityCreated_SetsCreatedByField()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        album.CreatedBy.Should().Be(_testUsername);
        album.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        album.UpdatedBy.Should().BeNull();
        album.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityModified_SetsUpdatedByField()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        var originalCreatedBy = album.CreatedBy;
        var originalCreatedAt = album.CreatedAt;

        // Act
        album.UpdateDetails("Updated Album", 2025, "Updated description");
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();

        // Assert
        album.CreatedBy.Should().Be(originalCreatedBy);
        album.CreatedAt.Should().Be(originalCreatedAt);
        album.UpdatedBy.Should().Be(_testUsername);
        album.UpdatedAt.Should().NotBeNull();
        album.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SaveChangesAsync_WhenMultipleEntitiesCreated_SetsCreatedByFieldForAll()
    {
        // Arrange
        var album1 = Album.Create("Album 1", 2024);
        var album2 = Album.Create("Album 2", 2024);

        // Act
        _context.Albums.Add(album1);
        _context.Albums.Add(album2);
        await _context.SaveChangesAsync();

        // Assert
        album1.CreatedBy.Should().Be(_testUsername);
        album2.CreatedBy.Should().Be(_testUsername);
    }

    [Fact]
    public async Task SaveChangesAsync_WithDifferentUser_TracksCorrectUser()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Change user
        var newUsername = "newuser";
        SetupMockUser(newUsername);

        // Act
        album.UpdateDetails("Updated Album", 2025, null);
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();

        // Assert
        album.CreatedBy.Should().Be(_testUsername);
        album.UpdatedBy.Should().Be(newUsername);
    }

    [Fact]
    public async Task SaveChangesAsync_WithNoUser_DoesNotSetCreatedBy()
    {
        // Arrange
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null!);
        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        album.CreatedBy.Should().BeNull();
        album.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SaveChangesAsync_ForVariousEntityTypes_SetsFieldsCorrectly()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        var song = Song.Create("Test Song", 1, 1);
        var label = Label.Create("REF001", "Test Label", "Test content");

        // Act
        _context.Albums.Add(album);
        _context.Songs.Add(song);
        _context.Labels.Add(label);
        await _context.SaveChangesAsync();

        // Assert
        album.CreatedBy.Should().Be(_testUsername);
        song.CreatedBy.Should().Be(_testUsername);
        label.CreatedBy.Should().Be(_testUsername);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityCreated_CreatesAuditLog()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("Album");
        auditLog.EntityId.Should().Be(album.Id);
        auditLog.Action.Should().Be("Created");
        auditLog.UserName.Should().Be(_testUsername);
        auditLog.Changes.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityModified_CreatesAuditLog()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act
        album.UpdateDetails("Updated Album", 2025, "Updated description");
        _context.Albums.Update(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("Album");
        auditLog.EntityId.Should().Be(album.Id);
        auditLog.Action.Should().Be("Modified");
        auditLog.UserName.Should().Be(_testUsername);
        auditLog.Changes.Should().NotBeNullOrEmpty();
        auditLog.Changes.Should().Contain("Title");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEntityDeleted_CreatesAuditLog()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();
        var albumId = album.Id;

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act
        _context.Albums.Remove(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("Album");
        auditLog.EntityId.Should().Be(albumId);
        auditLog.Action.Should().Be("Deleted");
        auditLog.UserName.Should().Be(_testUsername);
        auditLog.IsCriticalAction.Should().BeTrue(); // Deletions are critical
    }

    [Fact]
    public async Task SaveChangesAsync_CriticalEntity_MarksAuditLogAsCritical()
    {
        // Arrange - Report is considered a critical entity
        var report = Report.Create("Financial Report 2024", 2024, "Summary");

        // Act
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.Where(a => a.EntityType == "Report").ToListAsync();
        auditLogs.Should().ContainSingle();
        auditLogs.First().IsCriticalAction.Should().BeTrue();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenJsonPropertyUnchanged_DoesNotLogChange()
    {
        // Arrange - Create a user with Categories and Positions
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789",
            CategoriesJson = "[0]",
            PositionsJson = "[5]"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Load the user from DB, modify one property, then save
        // This simulates a real scenario where EF detects actual changes
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull(); // Explicit null check for clarity
        userFromDb!.PhoneContact = "987654321";
        // Re-set the JSON properties to the same values (this happens in real scenarios
        // when the form submits all fields)
        userFromDb.CategoriesJson = "[0]"; // Same value
        userFromDb.PositionsJson = "[5]"; // Same value
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.Changes.Should().NotBeNullOrEmpty();
        
        // The audit log should only contain PhoneContact change, not Categories or Positions
        auditLog.Changes.Should().Contain("PhoneContact");
        auditLog.Changes.Should().NotContain("Categories");
        auditLog.Changes.Should().NotContain("Positions");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenEmptyJsonArraysUnchanged_DoesNotLogChange()
    {
        // Arrange - Create a user with empty JSON arrays
        var user = new ApplicationUser
        {
            UserName = "testuser2",
            Email = "test2@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789",
            CategoriesJson = "[]",  // Empty array
            PositionsJson = "[]"    // Empty array
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Load the user from DB, modify one property, then save
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.PhoneContact = "987654321";
        // Re-set the empty JSON arrays (form resubmission scenario)
        userFromDb.CategoriesJson = "[]";  // Same empty array
        userFromDb.PositionsJson = "[]";   // Same empty array
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.Changes.Should().NotBeNullOrEmpty();
        
        // The audit log should only contain PhoneContact change, not empty Categories or Positions
        auditLog.Changes.Should().Contain("PhoneContact");
        auditLog.Changes.Should().NotContain("Categories");
        auditLog.Changes.Should().NotContain("Positions");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenNullBecomesEmptyArray_DoesNotLogChange()
    {
        // Arrange - Create a user with null JSON properties
        var user = new ApplicationUser
        {
            UserName = "testuser3",
            Email = "test3@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789",
            CategoriesJson = null,  // Null initially
            PositionsJson = null    // Null initially
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Load the user from DB, modify one property, set nulls to empty arrays
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.PhoneContact = "987654321";
        // Set null to empty array (semantically the same - no values)
        userFromDb.CategoriesJson = "[]";
        userFromDb.PositionsJson = "[]";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.Changes.Should().NotBeNullOrEmpty();
        
        // The audit log should only contain PhoneContact change
        // null -> [] is semantically no change (no values to no values)
        auditLog.Changes.Should().Contain("PhoneContact");
        auditLog.Changes.Should().NotContain("Categories");
        auditLog.Changes.Should().NotContain("Positions");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenRoleAdded_CreatesAuditLogWithUsernameAndRoleName()
    {
        // Arrange
        // Create a test role
        var testRole = new Microsoft.AspNetCore.Identity.IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Member",
            NormalizedName = "MEMBER"
        };
        _context.Roles.Add(testRole);
        
        // Create a test user
        var testUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "jeans",
            Email = "jeans@example.com",
            FirstName = "Jean",
            LastName = "Smith",
            PhoneContact = "123456789"
        };
        _context.Users.Add(testUser);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Add user to role
        var userRole = new Microsoft.AspNetCore.Identity.IdentityUserRole<string>
        {
            UserId = testUser.Id,
            RoleId = testRole.Id
        };
        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("UserRole");
        auditLog.Action.Should().Be("Role Added");
        auditLog.UserName.Should().Be(_testUsername);
        auditLog.IsCriticalAction.Should().BeTrue();
        auditLog.Changes.Should().NotBeNullOrEmpty();
        
        // Verify the changes contain username and role name, not IDs
        auditLog.Changes.Should().Contain("jeans");
        auditLog.Changes.Should().Contain("Member");
        auditLog.Changes.Should().NotContain(testUser.Id);
        auditLog.Changes.Should().NotContain(testRole.Id);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenRoleRemoved_CreatesAuditLogWithUsernameAndRoleName()
    {
        // Arrange
        // Create a test role
        var testRole = new Microsoft.AspNetCore.Identity.IdentityRole
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Admin",
            NormalizedName = "ADMIN"
        };
        _context.Roles.Add(testRole);
        
        // Create a test user
        var testUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "jeans",
            Email = "jeans@example.com",
            FirstName = "Jean",
            LastName = "Smith",
            PhoneContact = "123456789"
        };
        _context.Users.Add(testUser);
        
        // Add user to role
        var userRole = new Microsoft.AspNetCore.Identity.IdentityUserRole<string>
        {
            UserId = testUser.Id,
            RoleId = testRole.Id
        };
        _context.UserRoles.Add(userRole);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Remove user from role
        _context.UserRoles.Remove(userRole);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("UserRole");
        auditLog.Action.Should().Be("Role Removed");
        auditLog.UserName.Should().Be(_testUsername);
        auditLog.IsCriticalAction.Should().BeTrue();
        auditLog.Changes.Should().NotBeNullOrEmpty();
        
        // Verify the changes contain username and role name, not IDs
        auditLog.Changes.Should().Contain("jeans");
        auditLog.Changes.Should().Contain("Admin");
        auditLog.Changes.Should().NotContain(testUser.Id);
        auditLog.Changes.Should().NotContain(testRole.Id);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserProfileInfoChanged_DoesNotMarkAsCritical()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789",
            DateOfBirth = DateTime.Parse("1990-01-01"),
            Degree = "Computer Science"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify only profile information
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.FirstName = "Updated";
        userFromDb.LastName = "Name";
        userFromDb.DateOfBirth = DateTime.Parse("1991-02-02");
        userFromDb.Degree = "Software Engineering";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeFalse(); // Profile changes are NOT critical
        auditLog.Changes.Should().Contain("FirstName");
        auditLog.Changes.Should().Contain("LastName");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserEmailChanged_MarksAsCritical()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify Email (critical field)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.Email = "newemail@example.com";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // Email changes are critical
        auditLog.Changes.Should().Contain("Email");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserNameChanged_MarksAsCritical()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify UserName (critical field)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.UserName = "newusername";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // UserName changes are critical
        auditLog.Changes.Should().Contain("UserName");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserPhoneNumberChanged_MarksAsCritical()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789",
            PhoneNumber = "123456789"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify PhoneNumber (critical field)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.PhoneNumber = "987654321";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // PhoneNumber changes are critical
        auditLog.Changes.Should().Contain("PhoneNumber");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserPasswordHashChanged_MarksAsCriticalButDoesNotLogValue()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789",
            PasswordHash = "oldhash"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify PasswordHash (critical field but excluded from logging)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.PasswordHash = "newhash";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // PasswordHash changes are critical
        // But the actual hash value should NOT be in the changes (excluded field)
        auditLog.Changes.Should().NotContain("PasswordHash");
        auditLog.Changes.Should().NotContain("oldhash");
        auditLog.Changes.Should().NotContain("newhash");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserSecurityStampChanged_MarksAsCriticalButDoesNotLogValue()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789",
            SecurityStamp = "oldstamp"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify SecurityStamp (critical field but excluded from logging)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.SecurityStamp = "newstamp";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // SecurityStamp changes are critical
        // But the actual stamp value should NOT be in the changes (excluded field)
        auditLog.Changes.Should().NotContain("SecurityStamp");
        auditLog.Changes.Should().NotContain("oldstamp");
        auditLog.Changes.Should().NotContain("newstamp");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserProfileAndCriticalFieldChanged_MarksAsCritical()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789",
            PhoneNumber = "123456789"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify both profile info AND a critical field
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.FirstName = "Updated"; // Profile field (non-critical)
        userFromDb.PhoneNumber = "987654321"; // Critical field
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue(); // Should be critical due to PhoneNumber change
        auditLog.Changes.Should().Contain("FirstName");
        auditLog.Changes.Should().Contain("PhoneNumber");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserLastLoginDateChanged_CreatesAuditLog()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify only LastLoginDate
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.LastLoginDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeFalse(); // LastLoginDate is not critical
        auditLog.Changes.Should().Contain("LastLoginDate");
        // Note: The display logic for "Logged in" is in the UI layer, not in the audit log itself
    }

    [Fact]
    public async Task SaveChangesAsync_WhenUserDeleted_MarksAsCritical()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var userId = user.Id;

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Delete the user
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Deleted");
        auditLog.IsCriticalAction.Should().BeTrue(); // User deletions are always critical
        auditLog.Changes.Should().NotBeNullOrEmpty();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
