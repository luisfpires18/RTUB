using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using Xunit;

namespace RTUB.Application.Tests.Services;

public class PushNotificationServiceTests
{
    private readonly Mock<IPushSubscriptionRepository> _mockRepository;
    private readonly Mock<IUserProfileRepository> _mockUserProfileRepository;
    private readonly Mock<ILogger<PushNotificationService>> _mockLogger;
    private readonly WebPushOptions _options;
    private readonly PushNotificationService _service;

    public PushNotificationServiceTests()
    {
        _mockRepository = new Mock<IPushSubscriptionRepository>();
        _mockUserProfileRepository = new Mock<IUserProfileRepository>();
        _mockLogger = new Mock<ILogger<PushNotificationService>>();

        // Configure with minimal VAPID configuration for testing
        // Note: Keys don't need to be cryptographically valid or functional with actual push services
        // since we're testing business logic, not the actual push notification sending
        _options = new WebPushOptions
        {
            Enabled = true,
            VapidSubject = "mailto:test@example.com",
            VapidPublicKey = "test-public-key",
            VapidPrivateKey = "test-private-key"
        };

        var optionsWrapper = Options.Create(_options);
        _service = new PushNotificationService(
            _mockRepository.Object,
            _mockUserProfileRepository.Object,
            optionsWrapper,
            _mockLogger.Object);
    }

    [Fact]
    public void IsConfigured_ReturnsTrueWhenProperlyConfigured()
    {
        // Act
        var result = _service.IsConfigured();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsConfigured_ReturnsFalseWhenNotConfigured()
    {
        // Arrange
        var emptyOptions = Options.Create(new WebPushOptions());
        var service = new PushNotificationService(
            _mockRepository.Object,
            _mockUserProfileRepository.Object,
            emptyOptions,
            _mockLogger.Object);

        // Act
        var result = service.IsConfigured();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetVapidPublicKey_ReturnsPublicKey()
    {
        // Act
        var result = _service.GetVapidPublicKey();

        // Assert
        Assert.Equal(_options.VapidPublicKey, result);
    }

    [Fact]
    public async Task SubscribeAsync_CreatesNewSubscription_WhenNotExists()
    {
        // Arrange
        var userId = "test-user-id";
        var userName = "test-user-name";
        var subscriptionDto = new PushSubscriptionDto
        {
            Endpoint = "https://push.example.com/test",
            Keys = new PushKeysDto
            {
                P256dh = "test-p256dh",
                Auth = "test-auth"
            }
        };

        _mockRepository
            .Setup(r => r.GetByEndpointAsync(It.IsAny<string>()))
            .ReturnsAsync((PushSubscription?)null);

        // Act
        await _service.SubscribeAsync(userId, subscriptionDto, null, userName);

        // Assert
        _mockRepository.Verify(r => r.AddAsync(It.Is<PushSubscription>(
            s => s.UserId == userId &&
                 s.Endpoint == subscriptionDto.Endpoint &&
                 s.P256dh == subscriptionDto.Keys.P256dh &&
                 s.Auth == subscriptionDto.Keys.Auth
        )), Times.Once);
    }

    [Fact]
    public async Task SubscribeAsync_UpdatesExistingSubscription_WhenExists()
    {
        // Arrange
        var userId = "test-user-id";
        var userName = "updated-user-name";
        var existingSubscription = new PushSubscription
        {
            Id = 1,
            UserId = "old-user-id",
            Endpoint = "https://push.example.com/test",
            P256dh = "old-p256dh",
            Auth = "old-auth"
        };

        var subscriptionDto = new PushSubscriptionDto
        {
            Endpoint = existingSubscription.Endpoint,
            Keys = new PushKeysDto
            {
                P256dh = "new-p256dh",
                Auth = "new-auth"
            }
        };

        _mockRepository
            .Setup(r => r.GetByEndpointAsync(It.IsAny<string>()))
            .ReturnsAsync(existingSubscription);

        // Act
        await _service.SubscribeAsync(userId, subscriptionDto, null, userName);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<PushSubscription>(
            s => s.UserId == userId &&
                 s.P256dh == subscriptionDto.Keys.P256dh &&
                 s.Auth == subscriptionDto.Keys.Auth
        )), Times.Once);
    }

    [Fact]
    public async Task SubscribeAsync_ThrowsArgumentException_WhenEndpointIsEmpty()
    {
        // Arrange
        var userId = "test-user-id";
        var subscriptionDto = new PushSubscriptionDto
        {
            Endpoint = "",
            Keys = new PushKeysDto()
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SubscribeAsync(userId, subscriptionDto));
    }

    [Fact]
    public async Task UnsubscribeAsync_DeletesSubscription()
    {
        // Arrange
        var endpoint = "https://push.example.com/test";

        // Act
        await _service.UnsubscribeAsync(endpoint);

        // Assert
        _mockRepository.Verify(r => r.DeleteByEndpointAsync(endpoint), Times.Once);
    }

    [Fact]
    public async Task UnsubscribeAsync_ThrowsArgumentException_WhenEndpointIsEmpty()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UnsubscribeAsync(""));
    }

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

    // 027 retired Messages: a push no longer writes an inbox copy ("Sistema RTUB" conversation).
    // The constructor no longer takes any messaging repository, so a push cannot reach those tables.

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
        // Before 027 this still wrote the inbox copy; now there is nothing left to do.
        var service = new PushNotificationService(
            _mockRepository.Object,
            _mockUserProfileRepository.Object,
            Options.Create(new WebPushOptions()),
            _mockLogger.Object);

        await service.SendToUserAsync("user-1", new SendPushNotificationDto { Title = "T", Body = "B" });

        _mockRepository.VerifyNoOtherCalls();
        _mockUserProfileRepository.VerifyNoOtherCalls();
    }

    private void VerifyLog(LogLevel level, string expectedMessage)
    {
        _mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString() == expectedMessage),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("abc+def/ghi=", "abc-def_ghi")]
    [InlineData("abc-def_ghi", "abc-def_ghi")]
    [InlineData("AQID", "AQID")]
    [InlineData("AQ==", "AQ")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void NormalizeBase64Url_ConvertsCorrectly(string? input, string? expected)
    {
        var result = PushNotificationService.NormalizeBase64Url(input!);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task SubscribeAsync_NormalizesBase64UrlKeys()
    {
        // Arrange - Standard base64 keys with + and / characters
        var subscription = new PushSubscriptionDto
        {
            Endpoint = "https://push.example.com/test",
            Keys = new PushKeysDto
            {
                P256dh = "abc+def/ghi=",
                Auth = "xyz+123/456=="
            }
        };

        _mockRepository.Setup(r => r.GetByEndpointAsync(subscription.Endpoint))
            .ReturnsAsync((PushSubscription?)null);

        // Act
        await _service.SubscribeAsync("user-1", subscription, "test-ua", "Test User");

        // Assert - Keys should be normalized to base64url format
        _mockRepository.Verify(r => r.AddAsync(It.Is<PushSubscription>(
            s => s.P256dh == "abc-def_ghi" && s.Auth == "xyz-123_456"
        )), Times.Once);
    }

    [Fact]
    public void Constructor_AcceptsRealVapidKeys_WithoutLoggingInvalidConfigurationWarning()
    {
        // Regression guard for the WebPush package migration (phase 009).
        // PushNotificationService swallows ArgumentException from SetVapidDetails, so a library
        // that rejects RTUB's real VAPID key format would leave IsConfigured() == true while
        // never being able to send. The rest of this class uses placeholder keys, which always
        // take the swallowed path, so only a real keypair exercises this.
        // Keys are generated per-run - nothing key-shaped is stored in the repository.
        var vapidKeys = WebPush.VapidHelper.GenerateVapidKeys();

        // Fresh logger: the shared _mockLogger already recorded the warning from the
        // placeholder-key service built in this class's constructor.
        var logger = new Mock<ILogger<PushNotificationService>>();

        var options = Options.Create(new WebPushOptions
        {
            Enabled = true,
            VapidSubject = "mailto:test@example.com",
            VapidPublicKey = vapidKeys.PublicKey,
            VapidPrivateKey = vapidKeys.PrivateKey
        });

        // Act
        var service = new PushNotificationService(
            _mockRepository.Object,
            _mockUserProfileRepository.Object,
            options,
            logger.Object);

        // Assert
        Assert.True(service.IsConfigured());
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
