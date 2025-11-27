using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class PushSubscriptionTests
{
    [Fact]
    public void Constructor_WithDefaults_ShouldCreateInstance()
    {
        // Act
        var subscription = new PushSubscription();

        // Assert
        subscription.Should().NotBeNull();
        subscription.UserId.Should().Be(string.Empty);
        subscription.Endpoint.Should().Be(string.Empty);
        subscription.P256dh.Should().Be(string.Empty);
        subscription.Auth.Should().Be(string.Empty);
    }

    [Fact]
    public void Properties_WhenSet_ShouldReturnCorrectValues()
    {
        // Arrange
        var expirationTime = new DateTime(2025, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var subscription = new PushSubscription
        {
            Id = 1,
            UserId = "user-123",
            Endpoint = "https://fcm.googleapis.com/fcm/send/abc123",
            P256dh = "BLBsY9NpGt2-M2i3...p256dh_key",
            Auth = "auth_secret_here",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0)",
            ExpirationTime = expirationTime
        };

        // Assert
        subscription.Id.Should().Be(1);
        subscription.UserId.Should().Be("user-123");
        subscription.Endpoint.Should().Be("https://fcm.googleapis.com/fcm/send/abc123");
        subscription.P256dh.Should().Be("BLBsY9NpGt2-M2i3...p256dh_key");
        subscription.Auth.Should().Be("auth_secret_here");
        subscription.UserAgent.Should().Be("Mozilla/5.0 (Windows NT 10.0)");
        subscription.ExpirationTime.Should().Be(expirationTime);
    }

    [Fact]
    public void UserAgent_WhenNull_ShouldAllowNullValue()
    {
        // Arrange
        var subscription = new PushSubscription
        {
            UserId = "user-123",
            UserAgent = null
        };

        // Assert
        subscription.UserAgent.Should().BeNull();
    }

    [Fact]
    public void ExpirationTime_WhenNull_ShouldAllowNullValue()
    {
        // Arrange
        var subscription = new PushSubscription
        {
            UserId = "user-123",
            ExpirationTime = null
        };

        // Assert
        subscription.ExpirationTime.Should().BeNull();
    }

    [Fact]
    public void User_NavigationProperty_WhenNotSet_ShouldBeNull()
    {
        // Arrange
        var subscription = new PushSubscription();

        // Assert
        subscription.User.Should().BeNull();
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc")]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc")]
    [InlineData("https://wns2-par02.notify.windows.com/w/?token=abc")]
    public void Endpoint_WithDifferentServiceEndpoints_ShouldStore(string endpoint)
    {
        // Arrange
        var subscription = new PushSubscription { Endpoint = endpoint };

        // Assert
        subscription.Endpoint.Should().Be(endpoint);
    }

    [Fact]
    public void BaseEntityProperties_ShouldBeAccessible()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var subscription = new PushSubscription
        {
            CreatedAt = now,
            CreatedBy = "creator",
            UpdatedAt = now.AddHours(1),
            UpdatedBy = "updater"
        };

        // Assert
        subscription.CreatedAt.Should().Be(now);
        subscription.CreatedBy.Should().Be("creator");
        subscription.UpdatedAt.Should().Be(now.AddHours(1));
        subscription.UpdatedBy.Should().Be("updater");
    }
}
