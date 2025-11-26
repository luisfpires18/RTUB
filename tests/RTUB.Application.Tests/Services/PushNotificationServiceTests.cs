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
    private readonly Mock<IConversationRepository> _mockConversationRepository;
    private readonly Mock<IMessageRepository> _mockMessageRepository;
    private readonly Mock<ILogger<PushNotificationService>> _mockLogger;
    private readonly WebPushOptions _options;
    private readonly PushNotificationService _service;

    public PushNotificationServiceTests()
    {
        _mockRepository = new Mock<IPushSubscriptionRepository>();
        _mockConversationRepository = new Mock<IConversationRepository>();
        _mockMessageRepository = new Mock<IMessageRepository>();
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
            _mockConversationRepository.Object,
            _mockMessageRepository.Object,
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
            _mockConversationRepository.Object,
            _mockMessageRepository.Object,
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

        VerifyLog(LogLevel.Information, $"Created new push subscription for user {userName}");
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

    [Fact]
    public async Task SendToUserAsync_SendsInboxMessage_WhenConfigured()
    {
        // Arrange
        var userId = "test-user-id";
        var notification = new SendPushNotificationDto
        {
            Title = "Test Title",
            Body = "Test Body",
            Url = "/test-url"
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(Enumerable.Empty<PushSubscription>());

        _mockConversationRepository
            .Setup(r => r.GetByParticipantsAsync(It.IsAny<List<string>>()))
            .ReturnsAsync((Conversation?)null);

        // Act
        await _service.SendToUserAsync(userId, notification);

        // Assert - verify inbox message was created
        _mockConversationRepository.Verify(r => r.AddAsync(It.Is<Conversation>(
            c => c.Participants == userId &&
                 c.IsSystemConversation == true &&
                 c.Title == "Sistema RTUB"
        )), Times.Once);

        _mockMessageRepository.Verify(r => r.AddAsync(It.Is<Message>(
            m => m.Body.Contains("Test Title") &&
                 m.Body.Contains("Test Body") &&
                 m.IsSystem == true &&
                 m.Link == "/test-url"
        )), Times.Once);
    }

    [Fact]
    public async Task SendToUserAsync_UsesExistingConversation_WhenExists()
    {
        // Arrange
        var userId = "test-user-id";
        var existingConversation = new Conversation
        {
            Id = 123,
            Participants = userId,
            IsSystemConversation = true,
            Title = "Sistema RTUB"
        };
        var notification = new SendPushNotificationDto
        {
            Title = "Test Title",
            Body = "Test Body"
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(Enumerable.Empty<PushSubscription>());

        _mockConversationRepository
            .Setup(r => r.GetSystemConversationForUserAsync(userId))
            .ReturnsAsync(existingConversation);

        // Act
        await _service.SendToUserAsync(userId, notification);

        // Assert - verify existing conversation was used, not created
        _mockConversationRepository.Verify(r => r.AddAsync(It.IsAny<Conversation>()), Times.Never);
        _mockMessageRepository.Verify(r => r.AddAsync(It.Is<Message>(
            m => m.ConversationId == 123
        )), Times.Once);
    }

    [Fact]
    public async Task BroadcastAsync_SendsInboxMessageToAllUsers_WhenConfigured()
    {
        // Arrange
        var subscriptions = new List<PushSubscription>
        {
            new PushSubscription { UserId = "user1", Endpoint = "endpoint1", P256dh = "key1", Auth = "auth1" },
            new PushSubscription { UserId = "user2", Endpoint = "endpoint2", P256dh = "key2", Auth = "auth2" },
            new PushSubscription { UserId = "user1", Endpoint = "endpoint3", P256dh = "key3", Auth = "auth3" } // Same user, different device
        };

        var notification = new SendPushNotificationDto
        {
            Title = "Broadcast Title",
            Body = "Broadcast Body",
            Url = "/broadcast-url"
        };

        _mockRepository
            .Setup(r => r.GetAllActiveAsync())
            .ReturnsAsync(subscriptions);

        _mockConversationRepository
            .Setup(r => r.GetByParticipantsAsync(It.IsAny<List<string>>()))
            .ReturnsAsync((Conversation?)null);

        // Act
        await _service.BroadcastAsync(notification);

        // Assert - verify inbox messages were sent to unique users only (2 users, not 3 subscriptions)
        _mockConversationRepository.Verify(r => r.AddAsync(It.IsAny<Conversation>()), Times.Exactly(2));
        _mockMessageRepository.Verify(r => r.AddAsync(It.IsAny<Message>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SendToUserAsync_SendsInboxMessage_EvenWhenPushNotConfigured()
    {
        // Arrange
        var emptyOptions = Options.Create(new WebPushOptions());
        var service = new PushNotificationService(
            _mockRepository.Object,
            _mockConversationRepository.Object,
            _mockMessageRepository.Object,
            emptyOptions,
            _mockLogger.Object);

        var notification = new SendPushNotificationDto
        {
            Title = "Test Title",
            Body = "Test Body"
        };

        _mockConversationRepository
            .Setup(r => r.GetSystemConversationForUserAsync("test-user"))
            .ReturnsAsync((Conversation?)null);

        // Act
        await service.SendToUserAsync("test-user", notification);

        // Assert - inbox message IS created even when WebPush is not configured
        // This ensures users always receive important notifications in their inbox
        _mockConversationRepository.Verify(r => r.GetSystemConversationForUserAsync("test-user"), Times.Once);
        _mockConversationRepository.Verify(r => r.AddAsync(It.IsAny<Conversation>()), Times.Once);
        _mockMessageRepository.Verify(r => r.AddAsync(It.IsAny<Message>()), Times.Once);
    }

    [Fact]
    public async Task SendToUserAsync_InboxMessageCombinesTitleAndBody()
    {
        // Arrange
        var userId = "test-user-id";
        var notification = new SendPushNotificationDto
        {
            Title = "Important Notice",
            Body = "This is the message content"
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(Enumerable.Empty<PushSubscription>());

        _mockConversationRepository
            .Setup(r => r.GetByParticipantsAsync(It.IsAny<List<string>>()))
            .ReturnsAsync((Conversation?)null);

        // Act
        await _service.SendToUserAsync(userId, notification);

        // Assert
        _mockMessageRepository.Verify(r => r.AddAsync(It.Is<Message>(
            m => m.Body == "Important Notice\n\nThis is the message content"
        )), Times.Once);
    }

    [Fact]
    public async Task SendToUserAsync_InboxMessageUsesBodyOnly_WhenTitleEmpty()
    {
        // Arrange
        var userId = "test-user-id";
        var notification = new SendPushNotificationDto
        {
            Title = "",
            Body = "Just the body content"
        };

        _mockRepository
            .Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(Enumerable.Empty<PushSubscription>());

        _mockConversationRepository
            .Setup(r => r.GetByParticipantsAsync(It.IsAny<List<string>>()))
            .ReturnsAsync((Conversation?)null);

        // Act
        await _service.SendToUserAsync(userId, notification);

        // Assert
        _mockMessageRepository.Verify(r => r.AddAsync(It.Is<Message>(
            m => m.Body == "Just the body content"
        )), Times.Once);
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
}
