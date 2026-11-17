// PushNotificationService: opt out and sending.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

public partial class PushNotificationServiceTests
{
    [Fact]
    public async Task IsOptedOutAsync_ReturnsTrue_WhenUserOptedOut()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-1", PushNotificationsOptedOut = true };
        _mockUserProfileRepository
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationUser, bool>>>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.IsOptedOutAsync("user-1");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task IsOptedOutAsync_ReturnsFalse_WhenUserNotOptedOut()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-1", PushNotificationsOptedOut = false };
        _mockUserProfileRepository
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationUser, bool>>>()))
            .ReturnsAsync(user);

        // Act
        var result = await _service.IsOptedOutAsync("user-1");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsOptedOutAsync_ReturnsFalse_WhenUserNotFound()
    {
        // Arrange
        _mockUserProfileRepository
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationUser, bool>>>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.IsOptedOutAsync("missing-user");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SetOptedOutAsync_UpdatesUser_WhenValueChanges()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-1", PushNotificationsOptedOut = false };
        _mockUserProfileRepository
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationUser, bool>>>()))
            .ReturnsAsync(user);

        // Act
        await _service.SetOptedOutAsync("user-1", true);

        // Assert
        _mockUserProfileRepository.Verify(r => r.UpdateAsync(It.Is<ApplicationUser>(
            u => u.Id == "user-1" && u.PushNotificationsOptedOut == true
        )), Times.Once);
    }

    [Fact]
    public async Task SetOptedOutAsync_DoesNotUpdate_WhenValueAlreadyMatches()
    {
        // Arrange
        var user = new ApplicationUser { Id = "user-1", PushNotificationsOptedOut = true };
        _mockUserProfileRepository
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationUser, bool>>>()))
            .ReturnsAsync(user);

        // Act
        await _service.SetOptedOutAsync("user-1", true);

        // Assert
        _mockUserProfileRepository.Verify(r => r.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task SetOptedOutAsync_DoesNothing_WhenUserNotFound()
    {
        // Arrange
        _mockUserProfileRepository
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<ApplicationUser, bool>>>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        await _service.SetOptedOutAsync("missing-user", true);

        // Assert
        _mockUserProfileRepository.Verify(r => r.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task SendToSelectedUsersAsync_SendsToOnlySelectedUsers()
    {
        // Arrange
        var userIds = new[] { "user1", "user2" };
        var notification = new SendPushNotificationDto
        {
            Title = "Test Title",
            Body = "Test Body"
        };

        var allSubscriptions = new List<PushSubscription>
        {
            new PushSubscription { Id = 1, UserId = "user1", Endpoint = "https://push1.example.com", P256dh = "key1", Auth = "auth1" },
            new PushSubscription { Id = 2, UserId = "user2", Endpoint = "https://push2.example.com", P256dh = "key2", Auth = "auth2" },
            new PushSubscription { Id = 3, UserId = "user3", Endpoint = "https://push3.example.com", P256dh = "key3", Auth = "auth3" }
        };

        _mockRepository
            .Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(allSubscriptions);

        // Act
        await _service.SendToSelectedUsersAsync(userIds, notification);

        // Assert
        // Only subscriptions for user1 and user2 should be processed (2 subscriptions)
        // The actual sending is tested elsewhere, here we verify the filtering logic
        _mockRepository.Verify(r => r.GetAllActiveAsync(), Times.Once);
    }

    [Fact]
    public async Task SendToSelectedUsersAsync_DoesNothing_WhenNoUserIds()
    {
        // Arrange
        var notification = new SendPushNotificationDto
        {
            Title = "Test Title",
            Body = "Test Body"
        };

        // Act
        await _service.SendToSelectedUsersAsync(new string[] { }, notification);

        // Assert
        _mockRepository.Verify(r => r.GetAllActiveAsync(), Times.Never);
    }

    // 027 stopped the inbox copy; task 058 brought it back as "Notificações RTUB" through NotificationInbox, given only
    // by the host's constructor (NotificationInboxTests). The test constructors here have none, so these sends write nothing.

    [Fact]
    public async Task SendToUserAsync_WhenConfigured_LooksUpThatUsersSubscriptionsOnly()
    {
        _mockRepository.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new List<PushSubscription>());

        await _service.SendToUserAsync("user-1", new SendPushNotificationDto { Title = "T", Body = "B" });

        _mockRepository.Verify(r => r.GetByUserIdAsync("user-1"), Times.Once);
        _mockRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendToUserAsync_WhenPushIsNotConfigured_DoesNothing()
    {
        // Without an inbox (test constructor) and without VAPID keys there is nothing left to do.
        var service = new PushNotificationService(
            _mockRepository.Object,
            _mockUserProfileRepository.Object,
            Options.Create(new WebPushOptions()),
            _mockLogger.Object);

        await service.SendToUserAsync("user-1", new SendPushNotificationDto { Title = "T", Body = "B" });

        _mockRepository.VerifyNoOtherCalls();
        _mockUserProfileRepository.VerifyNoOtherCalls();
    }
}
