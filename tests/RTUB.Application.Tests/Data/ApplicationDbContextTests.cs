using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
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
    private readonly AuditContext _auditContext;
    private readonly string _testUsername = "testuser";

    public ApplicationDbContextTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _auditContext = new AuditContext();
        SetupMockUser(_testUsername);

        _context = new ApplicationDbContext(options, _httpContextAccessorMock.Object, _auditContext);
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
        auditLog.Changes.Should().NotContain("oldhash");
        auditLog.Changes.Should().NotContain("newhash");
        // However, we should indicate WHICH critical field was modified
        auditLog.Changes.Should().Contain("_CriticalFieldsModified");
        auditLog.Changes.Should().Contain("PasswordHash");
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
        auditLog.Changes.Should().NotContain("oldstamp");
        auditLog.Changes.Should().NotContain("newstamp");
        // However, we should indicate WHICH critical field was modified
        auditLog.Changes.Should().Contain("_CriticalFieldsModified");
        auditLog.Changes.Should().Contain("SecurityStamp");
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

    [Fact]
    public async Task SaveChangesAsync_WhenUserModified_IncludesTargetUserIdentification()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "targetuser",
            Email = "target@example.com",
            FirstName = "Target",
            LastName = "User",
            PhoneContact = "123456789"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify the user
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.FirstName = "UpdatedTarget";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Changes.Should().NotBeNullOrEmpty();

        // Verify the changes contain _TargetUser identification in simplified string format
        auditLog.Changes.Should().Contain("_TargetUser");
        auditLog.Changes.Should().Contain("targetuser");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenHttpContextUnavailable_UsesAuditContext()
    {
        // Arrange
        // Set HttpContext to null to simulate Blazor InteractiveServer scenario
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null!);

        // Set AuditContext with user information
        var auditUsername = "blazoruser";
        var auditUserId = "blazor123";
        _auditContext.SetUser(auditUsername, auditUserId);

        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.UserName.Should().Be(auditUsername);
        auditLog.UserId.Should().Be(auditUserId);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenBothHttpContextAndAuditContextAvailable_PrefersHttpContext()
    {
        // Arrange
        // Setup HttpContext with one user
        var httpUsername = "httpuser";
        SetupMockUser(httpUsername);

        // Setup AuditContext with different user
        _auditContext.SetUser("audituser", "audit123");

        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        // HttpContext should take precedence over AuditContext
        auditLog.UserName.Should().Be(httpUsername);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenNeitherHttpContextNorAuditContextAvailable_UsesNullUser()
    {
        // Arrange
        // Set HttpContext to null
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext)null!);

        // Clear AuditContext
        _auditContext.Clear();

        var album = Album.Create("Test Album", 2024);

        // Act
        _context.Albums.Add(album);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.UserName.Should().BeNull();
        auditLog.UserId.Should().BeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenMultipleCriticalFieldsChanged_IncludesAllInCriticalFieldsModified()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789",
            PasswordHash = "oldhash",
            SecurityStamp = "oldstamp"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify multiple critical fields at once
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.PasswordHash = "newhash";
        userFromDb.SecurityStamp = "newstamp";
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("ApplicationUser");
        auditLog.Action.Should().Be("Modified");
        auditLog.IsCriticalAction.Should().BeTrue();
        // Both critical fields should be listed in _CriticalFieldsModified
        auditLog.Changes.Should().Contain("_CriticalFieldsModified");
        auditLog.Changes.Should().Contain("PasswordHash");
        auditLog.Changes.Should().Contain("SecurityStamp");
        // But the actual values should NOT be logged
        auditLog.Changes.Should().NotContain("oldhash");
        auditLog.Changes.Should().NotContain("newhash");
        auditLog.Changes.Should().NotContain("oldstamp");
        auditLog.Changes.Should().NotContain("newstamp");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenOnlyExcludedFieldsChanged_DoesNotCreateAuditLog()
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

        // Act - Modify only excluded fields that are NOT critical (EmailConfirmed, PhoneNumberConfirmed)
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.EmailConfirmed = true;
        userFromDb.PhoneNumberConfirmed = true;
        await _context.SaveChangesAsync();

        // Assert - No audit log should be created because only non-critical excluded fields changed
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenExcludedAndNonExcludedFieldsChanged_CreatesAuditLogWithNonExcludedFields()
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

        // Act - Modify both excluded and non-excluded fields
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.SecurityStamp = "newstamp"; // Excluded
        userFromDb.FirstName = "Updated"; // Non-excluded
        await _context.SaveChangesAsync();

        // Assert - Audit log should be created with non-excluded field
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.Changes.Should().Contain("FirstName");
        // SecurityStamp is excluded from logging but appears in _CriticalFieldsModified array
        auditLog.Changes.Should().Contain("_CriticalFieldsModified");
        auditLog.Changes.Should().Contain("SecurityStamp");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenMultipleExcludedFieldsAndOneNonExcluded_CreatesAuditLog()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789",
            SecurityStamp = "oldstamp",
            PasswordHash = "oldhash"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear previous audit logs for cleaner test
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Modify multiple excluded fields AND one non-excluded field
        var userFromDb = await _context.Users.FindAsync(user.Id);
        userFromDb.Should().NotBeNull();
        userFromDb!.SecurityStamp = "newstamp"; // Excluded
        userFromDb.PasswordHash = "newhash"; // Excluded
        userFromDb.EmailConfirmed = true; // Excluded
        userFromDb.Nickname = "UpdatedNick"; // Non-excluded
        await _context.SaveChangesAsync();

        // Assert - Audit log should be created with only the non-excluded field
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.Changes.Should().Contain("Nickname");
        // Critical fields appear in _CriticalFieldsModified array but their values are excluded
        auditLog.Changes.Should().Contain("_CriticalFieldsModified");
        auditLog.Changes.Should().Contain("SecurityStamp");
        auditLog.Changes.Should().Contain("PasswordHash");
        // Non-critical excluded fields like EmailConfirmed don't appear at all
        auditLog.Changes.Should().NotContain("EmailConfirmed");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenOnlyLastLoginDateChanged_CreatesAuditLogForLogin()
    {
        // Arrange - Create a user
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear existing audit logs
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Update only LastLoginDate
        var userFromDb = await _context.Users.FirstAsync(u => u.Id == user.Id);
        userFromDb.LastLoginDate = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Assert - Audit log should be created with only LastLoginDate in non-metadata changes
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.Changes.Should().Contain("LastLoginDate");
        auditLog.Changes.Should().Contain("_TargetUser"); // Metadata field
        
        // Verify that the changes JSON contains LastLoginDate as the only non-metadata field
        if (!string.IsNullOrEmpty(auditLog.Changes))
        {
            var changesDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(auditLog.Changes);
            changesDict.Should().NotBeNull();
            var nonMetadataKeys = changesDict!.Keys.Where(k => !k.StartsWith("_")).ToList();
            nonMetadataKeys.Should().ContainSingle();
            nonMetadataKeys[0].Should().Be("LastLoginDate");
        }
    }

    [Fact]
    public async Task GetEntityDisplayName_OnlyUsesLocalCache_NoDbQueries()
    {
        // Arrange - Create entities but don't load them into context
        var song = new Song { Title = "Test Song", AlbumId = 1 };
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        // Clear the change tracker so Song is not in Local cache
        _context.ChangeTracker.Clear();

        // Create a SongYouTubeUrl that references the song
        var youtubeUrl = new SongYouTubeUrl { SongId = song.Id, Url = "https://youtube.com/test" };
        _context.SongYouTubeUrls.Add(youtubeUrl);
        
        // Act - Save changes which triggers GetEntityDisplayName
        // Since Song is not in Local cache, EntityDisplayName should be null (not fetch from DB)
        await _context.SaveChangesAsync();

        // Assert - Audit log should exist but without resolved entity name
        var auditLogs = await _context.AuditLogs.Where(a => a.EntityType == "SongYouTubeUrl").ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        // EntityDisplayName should be null because Song was not in Local cache
        auditLog.EntityDisplayName.Should().BeNull();
    }

    [Fact]
    public async Task GetEntityDisplayName_UsesLocalCache_WhenEntitiesInCache()
    {
        // Arrange - Create entities and keep them in context
        var song = new Song { Title = "Test Song", AlbumId = 1 };
        _context.Songs.Add(song);
        await _context.SaveChangesAsync();

        // Clear existing audit logs
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Create a SongYouTubeUrl - Song is now in Local cache
        var youtubeUrl = new SongYouTubeUrl { SongId = song.Id, Url = "https://youtube.com/test" };
        _context.SongYouTubeUrls.Add(youtubeUrl);
        
        // Act - Save changes which triggers GetEntityDisplayName
        await _context.SaveChangesAsync();

        // Assert - Audit log should have resolved entity name from Local cache
        var auditLogs = await _context.AuditLogs.Where(a => a.EntityType == "SongYouTubeUrl").ToListAsync();
        auditLogs.Should().ContainSingle();
        
        var auditLog = auditLogs.First();
        auditLog.EntityDisplayName.Should().Be("Test Song");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenMultipleEnrollmentsCreated_DoesNotDetectCreatedAtAsChanged()
    {
        // Arrange - Create test data (simulating seed data scenario)
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789"
        };
        _context.Users.Add(user);
        
        var evt = Event.Create("Test Event", DateTime.UtcNow.AddDays(7), "Test Location", Core.Enums.EventType.Festival);
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();

        // Clear existing audit logs
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Create multiple enrollments at once (like in seed data)
        var enrollments = new List<Enrollment>();
        for (int i = 0; i < 10; i++)
        {
            var enrollment = Enrollment.Create(user.Id, evt.Id, attended: i % 2 == 0);
            enrollments.Add(enrollment);
        }
        
        _context.Enrollments.AddRange(enrollments);
        
        // This should NOT cause an infinite loop or detect CreatedAt as changed
        await _context.SaveChangesAsync();

        // Assert - Verify all enrollments were saved successfully
        var savedEnrollments = await _context.Enrollments.Where(e => e.EventId == evt.Id).ToListAsync();
        savedEnrollments.Should().HaveCount(10);
        
        // Verify CreatedAt was set correctly for all enrollments
        foreach (var enrollment in savedEnrollments)
        {
            enrollment.CreatedAt.Should().NotBe(default(DateTime));
            enrollment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        }
        
        // Verify audit logs were created for all enrollments (no infinite loop)
        var auditLogs = await _context.AuditLogs.Where(a => a.EntityType == "Enrollment").ToListAsync();
        auditLogs.Should().HaveCount(10);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
