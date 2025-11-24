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
    private readonly Mock<ILogger<PushNotificationService>> _mockLogger;
    private readonly WebPushOptions _options;
    private readonly PushNotificationService _service;

    public PushNotificationServiceTests()
    {
        _mockRepository = new Mock<IPushSubscriptionRepository>();
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
        _service = new PushNotificationService(_mockRepository.Object, optionsWrapper, _mockLogger.Object);
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
        var service = new PushNotificationService(_mockRepository.Object, emptyOptions, _mockLogger.Object);

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
        await _service.SubscribeAsync(userId, subscriptionDto);

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
        await _service.SubscribeAsync(userId, subscriptionDto);

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
}
