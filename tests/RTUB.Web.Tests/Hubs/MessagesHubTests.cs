using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Web.Hubs;
using RTUB.Web.Services;
using System.Security.Claims;

namespace RTUB.Web.Tests.Hubs;

public class MessagesHubTests
{
    private readonly Mock<IConversationRepository> _mockConversationRepository;
    private readonly MessagesNotificationService _notificationService;
    private readonly Mock<ILogger<MessagesHub>> _mockLogger;
    private readonly Mock<IHubCallerClients<IMessagesHubClient>> _mockClients;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;
    private readonly TestableMessagesHub _hub;

    // Track notification service events
    private readonly List<(int conversationId, string userId)> _typingStartedNotifications = [];
    private readonly List<(int conversationId, string userId)> _typingStoppedNotifications = [];

    public MessagesHubTests()
    {
        _mockConversationRepository = new Mock<IConversationRepository>();
        _notificationService = new MessagesNotificationService();
        _mockLogger = new Mock<ILogger<MessagesHub>>();
        _mockClients = new Mock<IHubCallerClients<IMessagesHubClient>>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();

        // Subscribe to notification service events
        _notificationService.OnTypingStarted += (conversationId, userId) =>
        {
            _typingStartedNotifications.Add((conversationId, userId));
            return Task.CompletedTask;
        };
        _notificationService.OnTypingStopped += (conversationId, userId) =>
        {
            _typingStoppedNotifications.Add((conversationId, userId));
            return Task.CompletedTask;
        };

        _hub = new TestableMessagesHub(
            _mockConversationRepository.Object,
            _notificationService,
            _mockLogger.Object,
            _mockClients.Object,
            _mockGroups.Object,
            _mockContext.Object);
    }

    private void SetupAuthenticatedUser(string userId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
    }

    private void SetupUnauthenticatedUser()
    {
        var claimsPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        _mockContext.Setup(c => c.User).Returns(claimsPrincipal);
        _mockContext.Setup(c => c.ConnectionId).Returns("test-connection-id");
    }

    private Conversation CreateTestConversation(int id, params string[] participantIds)
    {
        var conversation = new Conversation
        {
            Id = id,
            Participants = string.Join(";", participantIds)
        };
        return conversation;
    }

    #region JoinConversation Tests

    [Fact]
    public async Task JoinConversation_WithValidUserAndConversation_JoinsGroup()
    {
        // Arrange
        const string userId = "user-123";
        const int conversationId = 1;
        var conversation = CreateTestConversation(conversationId, userId, "other-user");

        SetupAuthenticatedUser(userId);
        _mockConversationRepository
            .Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act
        await _hub.JoinConversation(conversationId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync("test-connection-id", $"conversation-{conversationId}", default),
            Times.Once);
    }

    [Fact]
    public async Task JoinConversation_WithNoUserId_DoesNotJoin()
    {
        // Arrange
        const int conversationId = 1;
        SetupUnauthenticatedUser();

        // Act
        await _hub.JoinConversation(conversationId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
        _mockConversationRepository.Verify(
            r => r.GetByIdAsync(It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task JoinConversation_WithNonExistentConversation_DoesNotJoin()
    {
        // Arrange
        const string userId = "user-123";
        const int conversationId = 999;

        SetupAuthenticatedUser(userId);
        _mockConversationRepository
            .Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync((Conversation?)null);

        // Act
        await _hub.JoinConversation(conversationId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
    }

    [Fact]
    public async Task JoinConversation_WhenUserNotParticipant_DoesNotJoin()
    {
        // Arrange
        const string userId = "user-123";
        const int conversationId = 1;
        var conversation = CreateTestConversation(conversationId, "other-user-1", "other-user-2");

        SetupAuthenticatedUser(userId);
        _mockConversationRepository
            .Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act
        await _hub.JoinConversation(conversationId);

        // Assert
        _mockGroups.Verify(
            g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Never);
    }

    #endregion

    #region LeaveConversation Tests

    [Fact]
    public async Task LeaveConversation_RemovesFromGroup()
    {
        // Arrange
        const string userId = "user-123";
        const int conversationId = 1;

        SetupAuthenticatedUser(userId);

        // Act
        await _hub.LeaveConversation(conversationId);

        // Assert
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync("test-connection-id", $"conversation-{conversationId}", default),
            Times.Once);
    }

    [Fact]
    public async Task LeaveConversation_WithNoUserId_StillRemovesFromGroup()
    {
        // Arrange
        const int conversationId = 1;
        SetupUnauthenticatedUser();

        // Act
        await _hub.LeaveConversation(conversationId);

        // Assert
        // LeaveConversation doesn't validate user - it just removes from group
        _mockGroups.Verify(
            g => g.RemoveFromGroupAsync("test-connection-id", $"conversation-{conversationId}", default),
            Times.Once);
    }

    #endregion

    #region SendTypingStarted Tests

    [Fact]
    public async Task SendTypingStarted_WithValidUser_NotifiesGroup()
    {
        // Arrange
        const string userId = "user-123";
        const int conversationId = 1;
        var conversation = CreateTestConversation(conversationId, userId, "other-user");

        SetupAuthenticatedUser(userId);
        _mockConversationRepository
            .Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        var mockHubClient = new Mock<IMessagesHubClient>();
        _mockClients
            .Setup(c => c.OthersInGroup($"conversation-{conversationId}"))
            .Returns(mockHubClient.Object);

        // Act
        await _hub.SendTypingStarted(conversationId);

        // Assert
        mockHubClient.Verify(
            c => c.TypingStarted(conversationId, userId),
            Times.Once);
        _typingStartedNotifications.Should().ContainSingle()
            .Which.Should().Be((conversationId, userId));
    }

    [Fact]
    public async Task SendTypingStarted_WithNoUserId_DoesNotNotify()
    {
        // Arrange
        const int conversationId = 1;
        SetupUnauthenticatedUser();

        // Act
        await _hub.SendTypingStarted(conversationId);

        // Assert
        _mockClients.Verify(
            c => c.OthersInGroup(It.IsAny<string>()),
            Times.Never);
        _typingStartedNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task SendTypingStarted_WhenUserNotParticipant_DoesNotNotify()
    {
        // Arrange
        const string userId = "user-123";
        const int conversationId = 1;
        var conversation = CreateTestConversation(conversationId, "other-user-1", "other-user-2");

        SetupAuthenticatedUser(userId);
        _mockConversationRepository
            .Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act
        await _hub.SendTypingStarted(conversationId);

        // Assert
        _mockClients.Verify(
            c => c.OthersInGroup(It.IsAny<string>()),
            Times.Never);
        _typingStartedNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task SendTypingStarted_WhenConversationNotExists_DoesNotNotify()
    {
        // Arrange
        const string userId = "user-123";
        const int conversationId = 999;

        SetupAuthenticatedUser(userId);
        _mockConversationRepository
            .Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync((Conversation?)null);

        // Act
        await _hub.SendTypingStarted(conversationId);

        // Assert
        _mockClients.Verify(
            c => c.OthersInGroup(It.IsAny<string>()),
            Times.Never);
        _typingStartedNotifications.Should().BeEmpty();
    }

    #endregion

    #region SendTypingStopped Tests

    [Fact]
    public async Task SendTypingStopped_WithValidUser_NotifiesGroup()
    {
        // Arrange
        const string userId = "user-123";
        const int conversationId = 1;

        SetupAuthenticatedUser(userId);

        var mockHubClient = new Mock<IMessagesHubClient>();
        _mockClients
            .Setup(c => c.OthersInGroup($"conversation-{conversationId}"))
            .Returns(mockHubClient.Object);

        // Act
        await _hub.SendTypingStopped(conversationId);

        // Assert
        mockHubClient.Verify(
            c => c.TypingStopped(conversationId, userId),
            Times.Once);
        _typingStoppedNotifications.Should().ContainSingle()
            .Which.Should().Be((conversationId, userId));
    }

    [Fact]
    public async Task SendTypingStopped_WithNoUserId_DoesNotNotify()
    {
        // Arrange
        const int conversationId = 1;
        SetupUnauthenticatedUser();

        // Act
        await _hub.SendTypingStopped(conversationId);

        // Assert
        _mockClients.Verify(
            c => c.OthersInGroup(It.IsAny<string>()),
            Times.Never);
        _typingStoppedNotifications.Should().BeEmpty();
    }

    #endregion

    #region OnConnectedAsync Tests

    [Fact]
    public async Task OnConnectedAsync_LogsConnection()
    {
        // Arrange
        const string userId = "user-123";
        SetupAuthenticatedUser(userId);

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("connected")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnConnectedAsync_WithUnauthenticatedUser_StillLogs()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        await _hub.OnConnectedAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("connected")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region OnDisconnectedAsync Tests

    [Fact]
    public async Task OnDisconnectedAsync_WithException_LogsWarning()
    {
        // Arrange
        const string userId = "user-123";
        SetupAuthenticatedUser(userId);
        var exception = new Exception("Test exception");

        // Act
        await _hub.OnDisconnectedAsync(exception);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disconnected")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithoutException_LogsDebug()
    {
        // Arrange
        const string userId = "user-123";
        SetupAuthenticatedUser(userId);

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disconnected")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task OnDisconnectedAsync_WithUnauthenticatedUser_StillLogs()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        await _hub.OnDisconnectedAsync(null);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("disconnected")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}

/// <summary>
/// Testable MessagesHub that allows setting the protected properties
/// </summary>
internal class TestableMessagesHub : MessagesHub
{
    private const System.Reflection.BindingFlags DeclaredFlags =
        System.Reflection.BindingFlags.Instance |
        System.Reflection.BindingFlags.Public |
        System.Reflection.BindingFlags.DeclaredOnly;

    public TestableMessagesHub(
        IConversationRepository conversationRepository,
        MessagesNotificationService notificationService,
        ILogger<MessagesHub> logger,
        IHubCallerClients<IMessagesHubClient> clients,
        IGroupManager groups,
        HubCallerContext context)
        : base(conversationRepository, notificationService, logger)
    {
        SetClients(clients);
        SetGroups(groups);
        SetContext(context);
    }

    private void SetClients(IHubCallerClients<IMessagesHubClient> clients)
    {
        // Hub<T>.Clients shadows Hub.Clients - we need to set the one declared in Hub<T>
        typeof(Hub<IMessagesHubClient>)
            .GetProperty("Clients", DeclaredFlags)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(this, [clients]);
    }

    private void SetGroups(IGroupManager groups)
    {
        // Groups is declared only in Hub base class
        typeof(Hub)
            .GetProperty("Groups", DeclaredFlags)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(this, [groups]);
    }

    private void SetContext(HubCallerContext context)
    {
        // Context is declared only in Hub base class
        typeof(Hub)
            .GetProperty("Context", DeclaredFlags)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(this, [context]);
    }
}
