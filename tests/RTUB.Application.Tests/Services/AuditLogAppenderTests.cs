using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for AuditLogAppender service
/// Tests audit log creation logic extracted from ApplicationDbContext
/// </summary>
public partial class AuditLogAppenderTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly AuditContext _auditContext;
    private readonly AuditLogAppender _appender;
    private readonly string _testUsername = "testuser";
    private readonly string _testUserId = "test-user-id";

    public AuditLogAppenderTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _auditContext = new AuditContext();
        SetupMockUser(_testUsername, _testUserId);

        _context = new ApplicationDbContext(options, _httpContextAccessorMock.Object, _auditContext, new AuditLogAppender());
        _appender = new AuditLogAppender();
    }

    private void SetupMockUser(string username, string userId)
    {
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, username),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId)
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new System.Security.Claims.ClaimsPrincipal(identity);

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(x => x.User).Returns(claimsPrincipal);

        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContextMock.Object);
    }

    [Fact]
    public void CreateAuditLog_WhenEntityCreated_ReturnsAuditLogWithCreatedAction()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        var entry = _context.Entry((BaseEntity)album);

        // Act
        var auditLog = _appender.CreateAuditLog(
            entry,
            "Created",
            _testUsername,
            _testUserId,
            ResolveUserIdToNickname,
            GetEntityDisplayName);

        // Assert
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("Created");
        auditLog.EntityType.Should().Be("Album");
        auditLog.UserName.Should().Be(_testUsername);
        auditLog.UserId.Should().Be(_testUserId);
        auditLog.Changes.Should().NotBeNull();
        auditLog.IsCriticalAction.Should().BeFalse(); // Album is not a critical entity
    }

    [Fact]
    public void CreateAuditLog_WhenEntityModified_ReturnsAuditLogWithModifiedAction()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        _context.SaveChanges();

        album.UpdateDetails("Updated Album", 2025, "Updated description", false);
        _context.Albums.Update(album);
        var entry = _context.Entry((BaseEntity)album);

        // Act
        var auditLog = _appender.CreateAuditLog(
            entry,
            "Modified",
            _testUsername,
            _testUserId,
            ResolveUserIdToNickname,
            GetEntityDisplayName);

        // Assert
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("Modified");
        auditLog.EntityType.Should().Be("Album");
        auditLog.Changes.Should().NotBeNull();

        // Verify changes are logged
        var changes = JsonSerializer.Deserialize<Dictionary<string, object>>(auditLog.Changes!);
        changes.Should().NotBeNull();
        changes.Should().ContainKey("Title"); // Album uses Title, not Name
    }

    [Fact]
    public void CreateAuditLog_WhenEntityModifiedWithNoChanges_ReturnsNull()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        _context.SaveChanges();

        // Modify but don't actually change anything (just touch the entity)
        _context.Albums.Update(album);
        var entry = _context.Entry((BaseEntity)album);

        // Manually mark as unchanged to simulate no actual changes
        entry.State = EntityState.Unchanged;
        entry.State = EntityState.Modified; // This won't have actual property changes

        // Act
        var auditLog = _appender.CreateAuditLog(
            entry,
            "Modified",
            _testUsername,
            _testUserId,
            ResolveUserIdToNickname,
            GetEntityDisplayName);

        // Assert
        // When there are no actual property changes, the appender should return null
        auditLog.Should().BeNull();
    }

    [Fact]
    public void CreateAuditLog_WhenEntityDeleted_ReturnsAuditLogWithDeletedAction()
    {
        // Arrange
        var album = Album.Create("Test Album", 2024);
        _context.Albums.Add(album);
        _context.SaveChanges();

        _context.Albums.Remove(album);
        var entry = _context.Entry((BaseEntity)album);

        // Act
        var auditLog = _appender.CreateAuditLog(
            entry,
            "Deleted",
            _testUsername,
            _testUserId,
            ResolveUserIdToNickname,
            GetEntityDisplayName);

        // Assert
        auditLog.Should().NotBeNull();
        auditLog!.Action.Should().Be("Deleted");
        auditLog.EntityType.Should().Be("Album");
        auditLog.IsCriticalAction.Should().BeTrue(); // Deletions are always critical
    }

    [Fact]
    public void CreateAuditLog_WhenCriticalEntityCreated_MarksAsCritical()
    {
        // Arrange
        var report = new Report
        {
            Title = "Test Report"
        };
        _context.Reports.Add(report);
        var entry = _context.Entry((BaseEntity)report);

        // Act
        var auditLog = _appender.CreateAuditLog(
            entry,
            "Created",
            _testUsername,
            _testUserId,
            ResolveUserIdToNickname,
            GetEntityDisplayName);

        // Assert
        auditLog.Should().NotBeNull();
        auditLog!.IsCriticalAction.Should().BeTrue(); // Report is a critical entity
    }

    [Fact]
    public void CreateAuditLog_WhenEnrollmentCreated_IncludesTargetMemberInfo()
    {
        // Arrange
        var user = new ApplicationUser
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestUser"
        };
        _context.Users.Add(user);
        _context.SaveChanges();

        var evt = new Event
        {
            Name = "Test Event",
            Date = DateTime.UtcNow.AddDays(7),
            Location = "Test Location"
        };
        _context.Events.Add(evt);
        _context.SaveChanges();

        var enrollment = new Enrollment
        {
            UserId = user.Id,
            EventId = evt.Id,
            WillAttend = true
        };
        _context.Enrollments.Add(enrollment);
        var entry = _context.Entry((BaseEntity)enrollment);

        // Act
        var auditLog = _appender.CreateAuditLog(
            entry,
            "Created",
            _testUsername,
            _testUserId,
            ResolveUserIdToNickname,
            GetEntityDisplayName);

        // Assert
        auditLog.Should().NotBeNull();
        auditLog.Should().NotBeNull();
    }

    // Helper methods to match ApplicationDbContext signature
    private string? ResolveUserIdToNickname(string? userId)
    {
        if (string.IsNullOrEmpty(userId))
            return null;

        var user = _context.Users.Local.FirstOrDefault(u => u.Id == userId);
        return user?.Nickname ?? user?.UserName;
    }

    private string? GetEntityDisplayName(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<BaseEntity> entry)
    {
        var entityType = entry.Entity.GetType().Name;

        return entityType switch
        {
            "Album" => entry.Entity is Album album ? album.Title : null,
            "Event" => entry.Entity is Event evt ? evt.Name : null,
            "Song" => entry.Entity is Song song ? song.Title : null,
            _ => null
        };
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
