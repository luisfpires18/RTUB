// PushNotificationService: subscriptions.
using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

public partial class PushNotificationServiceTests
{
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

    // 038: removal is scoped to the member's own subscriptions (the old UnsubscribeAsync deleted any endpoint).

    [Fact]
    public async Task RemoveForUserAsync_DeletesOnlyTheMembersOwnEndpoint_AndCountsWhatIsLeft()
    {
        _mockRepository.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new[]
        {
            new PushSubscription { UserId = "user-1", Endpoint = "https://fcm.googleapis.com/fcm/send/a" },
            new PushSubscription { UserId = "user-1", Endpoint = "https://fcm.googleapis.com/fcm/send/b" },
        });

        (await _service.RemoveForUserAsync("user-1", "https://fcm.googleapis.com/fcm/send/a")).Should().Be(1);
        (await _service.RemoveForUserAsync("user-1", "https://fcm.googleapis.com/fcm/send/someone-elses")).Should().Be(2);

        _mockRepository.Verify(r => r.DeleteByEndpointAsync("https://fcm.googleapis.com/fcm/send/a"), Times.Once);
        _mockRepository.Verify(r => r.DeleteByEndpointAsync("https://fcm.googleapis.com/fcm/send/someone-elses"), Times.Never);
    }

    [Fact]
    public async Task RemoveForUserAsync_WithoutAnEndpoint_DeletesEveryOneOfTheMembers()
    {
        _mockRepository.Setup(r => r.GetByUserIdAsync("user-1")).ReturnsAsync(new[]
        {
            new PushSubscription { UserId = "user-1", Endpoint = "https://fcm.googleapis.com/fcm/send/a" },
            new PushSubscription { UserId = "user-1", Endpoint = "https://updates.push.services.mozilla.com/wpush/v2/b" },
        });

        (await _service.RemoveForUserAsync("user-1", null)).Should().Be(0);

        _mockRepository.Verify(r => r.DeleteByEndpointAsync(It.IsAny<string>()), Times.Exactly(2));
    }

    [Fact]
    public async Task IsRegisteredAsync_IsTrueOnlyForTheMembersOwnEndpoint()
    {
        _mockRepository.Setup(r => r.GetByEndpointAsync("https://fcm.googleapis.com/fcm/send/a"))
            .ReturnsAsync(new PushSubscription { UserId = "user-1", Endpoint = "https://fcm.googleapis.com/fcm/send/a" });

        (await _service.IsRegisteredAsync("user-1", "https://fcm.googleapis.com/fcm/send/a")).Should().BeTrue();
        (await _service.IsRegisteredAsync("user-2", "https://fcm.googleapis.com/fcm/send/a")).Should().BeFalse();
        (await _service.IsRegisteredAsync("user-1", "https://fcm.googleapis.com/fcm/send/unknown")).Should().BeFalse();
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
}
