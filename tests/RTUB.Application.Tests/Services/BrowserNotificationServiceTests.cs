using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for BrowserNotificationService
/// Tests notification sending logic for new events
/// </summary>
public class BrowserNotificationServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly BrowserNotificationService _service;

    public BrowserNotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, 
            Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), 
            new AuditContext());

        // Setup UserManager mock
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        _service = new BrowserNotificationService(_context, _mockUserManager.Object);
    }

    [Fact]
    public async Task SendNewEventNotificationAsync_WithSubscribedUsers_ReturnsSuccessAndCount()
    {
        // Arrange
        var subscribedUsers = new List<ApplicationUser>
        {
            new ApplicationUser 
            { 
                Id = "user1", 
                UserName = "user1@test.com",
                Email = "user1@test.com",
                Subscribed = true, 
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "User1",
                Nickname = "TestUser1",
                PhoneContact = "123456789"
            },
            new ApplicationUser 
            { 
                Id = "user2", 
                UserName = "user2@test.com",
                Email = "user2@test.com",
                Subscribed = true, 
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "User2",
                Nickname = "TestUser2",
                PhoneContact = "987654321"
            }
        };

        var mockDbSet = subscribedUsers.AsQueryable().BuildMockDbSet();
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        var eventId = 1;
        var eventName = "Test Event";
        var eventDate = DateTime.Now.AddDays(7);

        // Act
        var (success, count) = await _service.SendNewEventNotificationAsync(eventId, eventName, eventDate);

        // Assert
        success.Should().BeTrue();
        count.Should().Be(2);
    }

    [Fact]
    public async Task SendNewEventNotificationAsync_WithNoSubscribedUsers_ReturnsZeroCount()
    {
        // Arrange
        var emptyUsers = new List<ApplicationUser>();

        var mockDbSet = emptyUsers.AsQueryable().BuildMockDbSet();
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        var eventId = 1;
        var eventName = "Test Event";
        var eventDate = DateTime.Now.AddDays(7);

        // Act
        var (success, count) = await _service.SendNewEventNotificationAsync(eventId, eventName, eventDate);

        // Assert
        success.Should().BeTrue();
        count.Should().Be(0);
    }

    [Fact]
    public async Task SendNewEventNotificationAsync_OnlyCountsSubscribedAndConfirmedUsers()
    {
        // Arrange
        var users = new List<ApplicationUser>
        {
            new ApplicationUser 
            { 
                Id = "user1", 
                UserName = "user1@test.com",
                Email = "user1@test.com",
                Subscribed = true, 
                EmailConfirmed = true,  // Will be counted
                FirstName = "Test",
                LastName = "User1",
                Nickname = "User1",
                PhoneContact = "123456789"
            },
            new ApplicationUser 
            { 
                Id = "user2", 
                UserName = "user2@test.com",
                Email = "user2@test.com",
                Subscribed = false,  // Not subscribed - won't be counted
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "User2",
                Nickname = "User2",
                PhoneContact = "987654321"
            },
            new ApplicationUser 
            { 
                Id = "user3", 
                UserName = "user3@test.com",
                Email = "user3@test.com",
                Subscribed = true, 
                EmailConfirmed = false,  // Email not confirmed - won't be counted
                FirstName = "Test",
                LastName = "User3",
                Nickname = "User3",
                PhoneContact = "111222333"
            }
        };

        var mockDbSet = users.AsQueryable().BuildMockDbSet();
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        var eventId = 1;
        var eventName = "Test Event";
        var eventDate = DateTime.Now.AddDays(7);

        // Act
        var (success, count) = await _service.SendNewEventNotificationAsync(eventId, eventName, eventDate);

        // Assert
        success.Should().BeTrue();
        count.Should().Be(1, "only user1 is both subscribed and email confirmed");
    }

    [Fact]
    public async Task SendNewEventNotificationAsync_WithValidEventData_ReturnsSuccess()
    {
        // Arrange
        var users = new List<ApplicationUser>
        {
            new ApplicationUser 
            { 
                Id = "user1", 
                UserName = "user1@test.com",
                Email = "user1@test.com",
                Subscribed = true, 
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "User",
                Nickname = "TestUser",
                PhoneContact = "123456789"
            }
        };

        var mockDbSet = users.AsQueryable().BuildMockDbSet();
        _mockUserManager.Setup(x => x.Users).Returns(mockDbSet.Object);

        var eventId = 123;
        var eventName = "Atuação Especial";
        var eventDate = new DateTime(2024, 12, 25, 19, 30, 0);

        // Act
        var (success, count) = await _service.SendNewEventNotificationAsync(eventId, eventName, eventDate);

        // Assert
        success.Should().BeTrue();
        count.Should().BeGreaterThan(0);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
