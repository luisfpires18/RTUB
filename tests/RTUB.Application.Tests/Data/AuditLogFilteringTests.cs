using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Data;

/// <summary>
/// Unit tests for ApplicationDbContext audit log filtering
/// Tests that specific entities are excluded from audit logging
/// </summary>
public class AuditLogFilteringTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly AuditContext _auditContext;
    private readonly string _testUsername = "testuser";
    private readonly string _testUserId = "test-user-id";

    public AuditLogFilteringTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _auditContext = new AuditContext();
        SetupMockUser(_testUsername, _testUserId);

        _context = new ApplicationDbContext(options, _httpContextAccessorMock.Object, _auditContext);
    }

    private void SetupMockUser(string username, string userId)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.NameIdentifier, userId)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(x => x.User).Returns(claimsPrincipal);

        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContextMock.Object);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenGalleryMediaPersonTagCreated_DoesNotCreateAuditLog()
    {
        // Arrange
        // First create a user and gallery media
        var user = new ApplicationUser
        {
            Id = _testUserId,
            UserName = _testUsername,
            Email = "test@example.com",
            Nickname = "TestNick",
            FirstName = "Test",
            LastName = "User"
        };
        _context.Users.Add(user);

        var galleryMedia = GalleryMedia.Create(
            uploaderId: _testUserId,
            title: "Test Media",
            mediaType: MediaType.Image,
            mediaUrl: "https://example.com/image.jpg",
            year: 2024
        );
        _context.GalleryMedia.Add(galleryMedia);
        await _context.SaveChangesAsync();

        // Clear the audit logs created for GalleryMedia and User
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Create a GalleryMediaPersonTag
        var personTag = GalleryMediaPersonTag.Create(galleryMedia.Id, _testUserId);
        _context.GalleryMediaPersonTags.Add(personTag);
        await _context.SaveChangesAsync();

        // Assert - No audit log should be created for GalleryMediaPersonTag
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenGalleryMediaPersonTagDeleted_DoesNotCreateAuditLog()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = _testUserId,
            UserName = _testUsername,
            Email = "test@example.com",
            Nickname = "TestNick",
            FirstName = "Test",
            LastName = "User"
        };
        _context.Users.Add(user);

        var galleryMedia = GalleryMedia.Create(
            uploaderId: _testUserId,
            title: "Test Media",
            mediaType: MediaType.Image,
            mediaUrl: "https://example.com/image.jpg",
            year: 2024
        );
        _context.GalleryMedia.Add(galleryMedia);

        var personTag = GalleryMediaPersonTag.Create(galleryMedia.Id, _testUserId);
        _context.GalleryMediaPersonTags.Add(personTag);
        await _context.SaveChangesAsync();

        // Clear audit logs
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Hard delete the person tag
        _context.GalleryMediaPersonTags.Remove(personTag);
        await _context.SaveChangesAsync();

        // Assert - No audit log should be created for GalleryMediaPersonTag deletion
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenGalleryMediaCreated_CreatesAuditLog()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = _testUserId,
            UserName = _testUsername,
            Email = "test@example.com",
            Nickname = "TestNick",
            FirstName = "Test",
            LastName = "User"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear audit logs
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act
        var galleryMedia = GalleryMedia.Create(
            uploaderId: _testUserId,
            title: "Test Media",
            mediaType: MediaType.Image,
            mediaUrl: "https://example.com/image.jpg",
            year: 2024
        );
        _context.GalleryMedia.Add(galleryMedia);
        await _context.SaveChangesAsync();

        // Assert - GalleryMedia should create an audit log
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("GalleryMedia");
        auditLog.Action.Should().Be("Created");
        auditLog.UserName.Should().Be(_testUsername);
    }

    [Fact]
    public async Task SaveChangesAsync_WhenPushSubscriptionCreated_CreatesAuditLogWithUsername()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = _testUserId,
            UserName = _testUsername,
            Email = "test@example.com",
            Nickname = "TestNickname",
            FirstName = "Test",
            LastName = "User"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear audit logs
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act
        var pushSubscription = new PushSubscription
        {
            UserId = _testUserId,
            Endpoint = "https://example.com/push",
            P256dh = "test-p256dh",
            Auth = "test-auth",
            User = user // Set navigation property to help with display name resolution
        };
        _context.PushSubscriptions.Add(pushSubscription);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("PushSubscription");
        auditLog.Action.Should().Be("Created");
        auditLog.UserName.Should().Be(_testUsername);
        // EntityDisplayName should show the username (nickname preferred)
        auditLog.EntityDisplayName.Should().Be("TestNickname");
    }

    [Fact]
    public async Task SaveChangesAsync_WhenPushSubscriptionCreatedWithoutNavigation_UsesLocalCache()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = _testUserId,
            UserName = _testUsername,
            Email = "test@example.com",
            Nickname = "TestNick",
            FirstName = "Test",
            LastName = "User"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Clear audit logs
        var existingLogs = await _context.AuditLogs.ToListAsync();
        _context.AuditLogs.RemoveRange(existingLogs);
        await _context.SaveChangesAsync();

        // Act - Create PushSubscription without setting navigation property
        // The system should still resolve the username from Local cache
        var pushSubscription = new PushSubscription
        {
            UserId = _testUserId,
            Endpoint = "https://example.com/push",
            P256dh = "test-p256dh",
            Auth = "test-auth"
            // Note: User navigation property is NOT set
        };
        _context.PushSubscriptions.Add(pushSubscription);
        await _context.SaveChangesAsync();

        // Assert
        var auditLogs = await _context.AuditLogs.ToListAsync();
        auditLogs.Should().ContainSingle();

        var auditLog = auditLogs.First();
        auditLog.EntityType.Should().Be("PushSubscription");
        auditLog.EntityDisplayName.Should().Be("TestNick"); // Should resolve from Local cache
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
