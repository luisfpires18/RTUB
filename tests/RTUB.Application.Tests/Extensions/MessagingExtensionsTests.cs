using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Tests.Extensions;

/// <summary>
/// Unit tests for MessagingExtensions
/// Tests push notification and messaging integration methods
/// </summary>
public class MessagingExtensionsTests
{
    private readonly Mock<IPushNotificationService> _mockPushService;
    private readonly Mock<IMessagingService> _mockMessagingService;

    public MessagingExtensionsTests()
    {
        _mockPushService = new Mock<IPushNotificationService>();
        _mockMessagingService = new Mock<IMessagingService>();
    }

    [Fact]
    public async Task SendNotificationWithMessageAsync_SendsBothPushAndMessage()
    {
        // Arrange
        var userId = "user123";
        var notification = new SendPushNotificationDto
        {
            Title = "Test Title",
            Body = "Test Body",
            Url = "/test/url"
        };

        _mockPushService.Setup(x => x.SendToUserAsync(userId, notification))
            .Returns(Task.CompletedTask);
        _mockMessagingService.Setup(x => x.SendSystemMessageAsync(userId, It.IsAny<string>(), notification.Url))
            .ReturnsAsync(new MessageDto());

        // Act
        await _mockPushService.Object.SendNotificationWithMessageAsync(
            _mockMessagingService.Object, userId, notification);

        // Assert
        _mockPushService.Verify(x => x.SendToUserAsync(userId, notification), Times.Once);
        _mockMessagingService.Verify(x => x.SendSystemMessageAsync(userId, It.IsAny<string>(), notification.Url), Times.Once);
    }

    [Fact]
    public async Task SendNotificationWithMessageAsync_UsesNotificationBodyForMessage()
    {
        // Arrange
        var userId = "user123";
        var notification = new SendPushNotificationDto
        {
            Title = "Event Reminder",
            Body = "Don't forget your event!",
            Url = "/events/1"
        };
        string? capturedMessage = null;

        _mockMessagingService.Setup(x => x.SendSystemMessageAsync(userId, It.IsAny<string>(), notification.Url))
            .Callback<string, string, string?>((u, m, l) => capturedMessage = m)
            .ReturnsAsync(new MessageDto());

        // Act
        await _mockPushService.Object.SendNotificationWithMessageAsync(
            _mockMessagingService.Object, userId, notification);

        // Assert
        capturedMessage.Should().Contain(notification.Title);
        capturedMessage.Should().Contain(notification.Body);
    }

    [Fact]
    public async Task SendNotificationWithMessageAsync_UsesCustomMessageBody()
    {
        // Arrange
        var userId = "user123";
        var notification = new SendPushNotificationDto
        {
            Title = "Test",
            Body = "Original Body",
            Url = "/test"
        };
        var customBody = "Custom message body";
        string? capturedMessage = null;

        _mockMessagingService.Setup(x => x.SendSystemMessageAsync(userId, It.IsAny<string>(), notification.Url))
            .Callback<string, string, string?>((u, m, l) => capturedMessage = m)
            .ReturnsAsync(new MessageDto());

        // Act
        await _mockPushService.Object.SendNotificationWithMessageAsync(
            _mockMessagingService.Object, userId, notification, customBody);

        // Assert
        capturedMessage.Should().Be(customBody);
    }

    [Fact]
    public async Task SendNotificationWithMessageAsync_DoesNotThrowOnPushError()
    {
        // Arrange
        var userId = "user123";
        var notification = new SendPushNotificationDto { Title = "Test", Body = "Body" };

        _mockPushService.Setup(x => x.SendToUserAsync(userId, notification))
            .ThrowsAsync(new Exception("Push failed"));

        // Act
        var act = async () => await _mockPushService.Object.SendNotificationWithMessageAsync(
            _mockMessagingService.Object, userId, notification);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendNotificationWithMessageAsync_DoesNotThrowOnMessagingError()
    {
        // Arrange
        var userId = "user123";
        var notification = new SendPushNotificationDto { Title = "Test", Body = "Body" };

        _mockMessagingService.Setup(x => x.SendSystemMessageAsync(userId, It.IsAny<string>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception("Messaging failed"));

        // Act
        var act = async () => await _mockPushService.Object.SendNotificationWithMessageAsync(
            _mockMessagingService.Object, userId, notification);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendNotificationWithMessagesAsync_SendsToMultipleUsers()
    {
        // Arrange
        var userIds = new[] { "user1", "user2", "user3" };
        var notification = new SendPushNotificationDto
        {
            Title = "Broadcast",
            Body = "Message for all",
            Url = "/announcements"
        };

        // Act
        await _mockPushService.Object.SendNotificationWithMessagesAsync(
            _mockMessagingService.Object, userIds, notification);

        // Assert
        _mockPushService.Verify(x => x.SendToUserAsync(It.IsIn(userIds), notification), Times.Exactly(3));
        _mockMessagingService.Verify(x => x.SendSystemMessageAsync(It.IsIn(userIds), It.IsAny<string>(), notification.Url), Times.Exactly(3));
    }

    [Fact]
    public async Task SendNotificationWithMessagesAsync_WithEmptyUserList_Completes()
    {
        // Arrange
        var userIds = Enumerable.Empty<string>();
        var notification = new SendPushNotificationDto { Title = "Test", Body = "Body" };

        // Act
        var act = async () => await _mockPushService.Object.SendNotificationWithMessagesAsync(
            _mockMessagingService.Object, userIds, notification);

        // Assert
        await act.Should().NotThrowAsync();
        _mockPushService.Verify(x => x.SendToUserAsync(It.IsAny<string>(), It.IsAny<SendPushNotificationDto>()), Times.Never);
    }

    [Fact]
    public async Task SendNotificationWithMessagesAsync_WithCustomBody_UsesCustomBodyForAll()
    {
        // Arrange
        var userIds = new[] { "user1", "user2" };
        var notification = new SendPushNotificationDto { Title = "Test", Body = "Original", Url = "/test" };
        var customBody = "Custom message for all";

        // Act
        await _mockPushService.Object.SendNotificationWithMessagesAsync(
            _mockMessagingService.Object, userIds, notification, customBody);

        // Assert
        _mockMessagingService.Verify(x => x.SendSystemMessageAsync(It.IsAny<string>(), customBody, notification.Url), Times.Exactly(2));
    }

    [Fact]
    public async Task SendNotificationWithMessagesAsync_ContinuesOnIndividualFailure()
    {
        // Arrange
        var userIds = new[] { "user1", "user2", "user3" };
        var notification = new SendPushNotificationDto { Title = "Test", Body = "Body" };

        _mockPushService.Setup(x => x.SendToUserAsync("user2", notification))
            .ThrowsAsync(new Exception("Failed for user2"));

        // Act
        var act = async () => await _mockPushService.Object.SendNotificationWithMessagesAsync(
            _mockMessagingService.Object, userIds, notification);

        // Assert - Should not throw even if one user fails
        await act.Should().NotThrowAsync();
    }
}
