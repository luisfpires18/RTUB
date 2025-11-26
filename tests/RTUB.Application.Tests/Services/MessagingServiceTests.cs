using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MessagingService
/// Tests business logic for internal messaging system
/// </summary>
public class MessagingServiceTests
{
    private readonly Mock<IConversationRepository> _mockConversationRepository;
    private readonly Mock<IMessageRepository> _mockMessageRepository;
    private readonly Mock<IConversationUserSettingsRepository> _mockSettingsRepository;
    private readonly Mock<IRoleAssignmentRepository> _mockRoleAssignmentRepository;
    private readonly Mock<IPushNotificationService> _mockPushService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<ILogger<MessagingService>> _mockLogger;
    private readonly MessagingService _service;

    public MessagingServiceTests()
    {
        _mockConversationRepository = new Mock<IConversationRepository>();
        _mockMessageRepository = new Mock<IMessageRepository>();
        _mockSettingsRepository = new Mock<IConversationUserSettingsRepository>();
        _mockRoleAssignmentRepository = new Mock<IRoleAssignmentRepository>();
        _mockPushService = new Mock<IPushNotificationService>();
        _mockLogger = new Mock<ILogger<MessagingService>>();
        _mockUserManager = MockHelpers.CreateMockUserManager();

        _service = new MessagingService(
            _mockConversationRepository.Object,
            _mockMessageRepository.Object,
            _mockSettingsRepository.Object,
            _mockRoleAssignmentRepository.Object,
            _mockPushService.Object,
            _mockUserManager.Object,
            _mockLogger.Object);
    }

    /// <summary>
    /// Helper method to get the current fiscal year boundaries
    /// </summary>
    private static (int StartYear, int EndYear) GetCurrentFiscalYear()
    {
        var now = DateTime.UtcNow;
        var startYear = now.Month >= 9 ? now.Year : now.Year - 1;
        var endYear = startYear + 1;
        return (startYear, endYear);
    }

    [Fact]
    public async Task GetUnreadCountAsync_WithUnreadMessages_ReturnsCount()
    {
        // Arrange
        var userId = "user123";
        _mockMessageRepository.Setup(r => r.GetUnreadCountForUserAsync(userId))
            .ReturnsAsync(5);

        // Act
        var result = await _service.GetUnreadCountAsync(userId);

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public async Task GetUnreadCountAsync_WithNoUnreadMessages_ReturnsZero()
    {
        // Arrange
        var userId = "user123";
        _mockMessageRepository.Setup(r => r.GetUnreadCountForUserAsync(userId))
            .ReturnsAsync(0);

        // Act
        var result = await _service.GetUnreadCountAsync(userId);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task SendDirectMessageAsync_ValidMessage_CreatesMessageAndSendsNotification()
    {
        // Arrange
        var senderId = "sender123";
        var receiverId = "receiver456";
        var messageText = "Test message";

        var conversation = new Conversation
        {
            Id = 1,
            Participants = $"{senderId};{receiverId}",
            LastMessageAt = DateTime.UtcNow
        };

        var sender = new ApplicationUser
        {
            Id = senderId,
            Nickname = "Sender Nick",
            FirstName = "John",
            LastName = "Doe"
        };

        _mockConversationRepository.Setup(r => r.GetOrCreateOneToOneAsync(senderId, receiverId))
            .ReturnsAsync(conversation);

        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m);

        _mockUserManager.Setup(um => um.FindByIdAsync(senderId))
            .ReturnsAsync(sender);

        var messageDto = new SendMessageDto
        {
            ReceiverId = receiverId,
            Body = messageText
        };

        // Act
        var result = await _service.SendDirectMessageAsync(senderId, messageDto);

        // Assert
        result.Should().NotBeNull();
        result.Body.Should().Be(messageText);
        result.SenderId.Should().Be(senderId);

        // Direct messages should use SendPushOnlyAsync (not SendToUserAsync which creates inbox message)
        _mockPushService.Verify(p => p.SendPushOnlyAsync(
            receiverId,
            It.Is<SendPushNotificationDto>(n => n.Title.Contains("Sender Nick"))),
            Times.Once);
    }

    [Fact]
    public async Task SendSystemMessageAsync_ValidMessage_CreatesSystemMessage()
    {
        // Arrange
        var receiverId = "receiver456";
        var messageText = "System notification";
        var link = "/events/123";

        var conversation = new Conversation
        {
            Id = 1,
            Participants = receiverId,
            IsSystemConversation = true,
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetSystemConversationForUserAsync(receiverId))
            .ReturnsAsync((Conversation?)null);

        _mockConversationRepository.Setup(r => r.AddAsync(It.IsAny<Conversation>()))
            .ReturnsAsync(conversation);

        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m);

        // Act
        var result = await _service.SendSystemMessageAsync(receiverId, messageText, link);

        // Assert
        result.Should().NotBeNull();
        result.Body.Should().Be(messageText);
        result.IsSystem.Should().BeTrue();
        result.Link.Should().Be(link);
        result.SenderId.Should().BeNull();
    }

    [Fact]
    public async Task MarkConversationAsReadAsync_ValidConversation_MarksMessagesAsRead()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        _mockMessageRepository.Setup(r => r.MarkConversationAsReadAsync(conversationId, userId))
            .Returns(Task.CompletedTask);

        // Act
        await _service.MarkConversationAsReadAsync(conversationId, userId);

        // Assert
        _mockMessageRepository.Verify(r => r.MarkConversationAsReadAsync(conversationId, userId), Times.Once);
    }

    [Fact]
    public async Task DeleteConversationAsync_ValidConversation_ArchivesConversationAndReturnsTrue()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = $"{userId};otherUser",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockConversationRepository.Setup(r => r.ArchiveConversationAsync(conversationId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeleteConversationAsync(conversationId, userId);

        // Assert
        result.Should().BeTrue();
        _mockConversationRepository.Verify(r => r.ArchiveConversationAsync(conversationId), Times.Once);
    }

    [Fact]
    public async Task DeleteConversationAsync_UserNotParticipant_DoesNotArchiveAndReturnsFalse()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = "otherUser1;otherUser2",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act
        var result = await _service.DeleteConversationAsync(conversationId, userId);

        // Assert
        result.Should().BeFalse();
        _mockConversationRepository.Verify(r => r.ArchiveConversationAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteConversationAsync_SystemConversation_ReturnsFalseAndDoesNotArchive()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = userId,
            IsSystemConversation = true,
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act
        var result = await _service.DeleteConversationAsync(conversationId, userId);

        // Assert
        result.Should().BeFalse();
        _mockConversationRepository.Verify(r => r.ArchiveConversationAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteConversationAsync_SystemGroup_ReturnsFalseAndDoesNotArchive()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = $"{userId};otherUser",
            IsGroup = true,
            CreatedByUserId = "system",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act
        var result = await _service.DeleteConversationAsync(conversationId, userId);

        // Assert
        result.Should().BeFalse();
        _mockConversationRepository.Verify(r => r.ArchiveConversationAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task DeleteConversationAsync_GroupCreator_ArchivesConversationAndReturnsTrue()
    {
        // Arrange
        var conversationId = 1;
        var creatorId = "creator123";

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = $"{creatorId};user1;user2",
            IsGroup = true,
            CreatedByUserId = creatorId,
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockConversationRepository.Setup(r => r.ArchiveConversationAsync(conversationId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.DeleteConversationAsync(conversationId, creatorId);

        // Assert
        result.Should().BeTrue();
        _mockConversationRepository.Verify(r => r.ArchiveConversationAsync(conversationId), Times.Once);
    }

    [Fact]
    public async Task DeleteConversationAsync_GroupNonCreator_ReturnsFalseAndDoesNotArchive()
    {
        // Arrange
        var conversationId = 1;
        var nonCreatorId = "user123";

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = $"creator;{nonCreatorId};user2",
            IsGroup = true,
            CreatedByUserId = "creator",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act
        var result = await _service.DeleteConversationAsync(conversationId, nonCreatorId);

        // Assert
        result.Should().BeFalse();
        _mockConversationRepository.Verify(r => r.ArchiveConversationAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateConversationAsync_ExistingConversation_ReturnsExisting()
    {
        // Arrange
        var user1Id = "user1";
        var user2Id = "user2";

        var conversation = new Conversation
        {
            Id = 1,
            Participants = $"{user1Id};{user2Id}",
            LastMessageAt = DateTime.UtcNow
        };

        var user2 = new ApplicationUser
        {
            Id = user2Id,
            Nickname = "User2Nick",
            FirstName = "Jane",
            LastName = "Smith",
            ImageUrl = "/images/user2.jpg"
        };

        _mockConversationRepository.Setup(r => r.GetOrCreateOneToOneAsync(user1Id, user2Id))
            .ReturnsAsync(conversation);

        _mockUserManager.Setup(um => um.FindByIdAsync(user2Id))
            .ReturnsAsync(user2);

        _mockMessageRepository.Setup(r => r.GetUnreadCountForConversationAsync(conversation.Id, user1Id))
            .ReturnsAsync(0);

        // Act
        var result = await _service.GetOrCreateConversationAsync(user1Id, user2Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);
        result.OtherParticipantId.Should().Be(user2Id);
        result.OtherParticipantNickname.Should().Be("User2Nick");
    }

    [Fact]
    public async Task MarkConversationAsUnreadAsync_LastMessageExists_MarksAsUnread()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        var lastMessage = new Message
        {
            Id = 1,
            ConversationId = conversationId,
            SenderId = "otherUser",
            Body = "Last message",
            CreatedAt = DateTime.UtcNow,
            ReadBy = $"{userId}"
        };

        _mockMessageRepository.Setup(r => r.GetLatestMessageAsync(conversationId))
            .ReturnsAsync(lastMessage);

        _mockMessageRepository.Setup(r => r.UpdateAsync(It.IsAny<Message>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.MarkConversationAsUnreadAsync(conversationId, userId);

        // Assert
        _mockMessageRepository.Verify(r => r.UpdateAsync(It.Is<Message>(m => !m.ReadBy.Contains(userId))), Times.Once);
    }

    // Group chat tests

    [Fact]
    public async Task CreateGroupConversationAsync_ValidInput_CreatesGroupAndReturnsDto()
    {
        // Arrange
        var creatorId = "creator123";
        var groupName = "Test Group";
        var participantIds = new List<string> { "user1", "user2", "user3" };

        var creator = new ApplicationUser
        {
            Id = creatorId,
            Nickname = "Creator Nick",
            FirstName = "John",
            LastName = "Creator"
        };

        _mockConversationRepository.Setup(r => r.AddAsync(It.IsAny<Conversation>()))
            .ReturnsAsync((Conversation c) => { c.Id = 1; return c; });

        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => { m.Id = 1; return m; });

        _mockUserManager.Setup(um => um.FindByIdAsync(creatorId))
            .ReturnsAsync(creator);

        _mockUserManager.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationUser { Id = id, FirstName = "User", LastName = id });

        _mockMessageRepository.Setup(r => r.GetUnreadCountForConversationAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.CreateGroupConversationAsync(creatorId, groupName, participantIds);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(groupName);
        result.IsGroup.Should().BeTrue();
        result.CreatedByUserId.Should().Be(creatorId);

        _mockConversationRepository.Verify(r => r.AddAsync(It.Is<Conversation>(c =>
            c.IsGroup == true &&
            c.Title == groupName &&
            c.CreatedByUserId == creatorId)), Times.Once);
    }

    [Fact]
    public async Task CreateGroupConversationAsync_CreatorNotInList_AddsCreatorAutomatically()
    {
        // Arrange
        var creatorId = "creator123";
        var groupName = "Test Group";
        var participantIds = new List<string> { "user1", "user2" }; // Creator not included

        var creator = new ApplicationUser
        {
            Id = creatorId,
            Nickname = "Creator Nick",
            FirstName = "John",
            LastName = "Creator"
        };

        Conversation? capturedConversation = null;
        _mockConversationRepository.Setup(r => r.AddAsync(It.IsAny<Conversation>()))
            .Callback<Conversation>(c => capturedConversation = c)
            .ReturnsAsync((Conversation c) => { c.Id = 1; return c; });

        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => { m.Id = 1; return m; });

        _mockUserManager.Setup(um => um.FindByIdAsync(creatorId))
            .ReturnsAsync(creator);

        _mockUserManager.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationUser { Id = id, FirstName = "User", LastName = id });

        _mockMessageRepository.Setup(r => r.GetUnreadCountForConversationAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(0);

        // Act
        await _service.CreateGroupConversationAsync(creatorId, groupName, participantIds);

        // Assert
        capturedConversation.Should().NotBeNull();
        capturedConversation!.GetParticipantIds().Should().Contain(creatorId);
    }

    [Fact]
    public async Task SendGroupMessageAsync_ValidGroupConversation_SendsToAllParticipants()
    {
        // Arrange
        var senderId = "sender123";
        var conversationId = 1;
        var messageBody = "Hello group!";

        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "Test Group",
            IsGroup = true,
            Participants = $"{senderId};user1;user2",
            LastMessageAt = DateTime.UtcNow
        };

        var sender = new ApplicationUser
        {
            Id = senderId,
            Nickname = "Sender Nick",
            FirstName = "John",
            LastName = "Doe"
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => { m.Id = 1; return m; });

        _mockUserManager.Setup(um => um.FindByIdAsync(senderId))
            .ReturnsAsync(sender);

        // Act
        var result = await _service.SendGroupMessageAsync(senderId, conversationId, messageBody);

        // Assert
        result.Should().NotBeNull();
        result.Body.Should().Be(messageBody);
        result.ConversationId.Should().Be(conversationId);

        // Verify push notifications sent to other participants (using SendPushOnlyAsync, not SendToUserAsync)
        _mockPushService.Verify(p => p.SendPushOnlyAsync(
            "user1",
            It.Is<SendPushNotificationDto>(n => n.Title.Contains("Test Group"))),
            Times.Once);
        _mockPushService.Verify(p => p.SendPushOnlyAsync(
            "user2",
            It.Is<SendPushNotificationDto>(n => n.Title.Contains("Test Group"))),
            Times.Once);
    }

    [Fact]
    public async Task SendGroupMessageAsync_UserNotParticipant_ThrowsException()
    {
        // Arrange
        var senderId = "sender123";
        var conversationId = 1;
        var messageBody = "Hello group!";

        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "Test Group",
            IsGroup = true,
            Participants = "user1;user2", // Sender not in participants
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SendGroupMessageAsync(senderId, conversationId, messageBody));
    }

    [Fact]
    public async Task SendGroupMessageAsync_NotAGroup_ThrowsException()
    {
        // Arrange
        var senderId = "sender123";
        var conversationId = 1;
        var messageBody = "Hello!";

        var conversation = new Conversation
        {
            Id = conversationId,
            IsGroup = false,
            Participants = $"{senderId};user1",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SendGroupMessageAsync(senderId, conversationId, messageBody));
    }

    [Fact]
    public async Task GetOrCreateSystemGroupAsync_NewGroup_CreatesWithSystemCreator()
    {
        // Arrange
        var groupTitle = "TUNOSSAUROS";
        var participantIds = new List<string> { "user1", "user2", "user3" };

        _mockConversationRepository.Setup(r => r.GetGroupByTitleAsync(groupTitle))
            .ReturnsAsync((Conversation?)null);

        _mockConversationRepository.Setup(r => r.AddAsync(It.IsAny<Conversation>()))
            .ReturnsAsync((Conversation c) => { c.Id = 1; return c; });

        _mockUserManager.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationUser { Id = id, FirstName = "User", LastName = id });

        _mockMessageRepository.Setup(r => r.GetUnreadCountForConversationAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.GetOrCreateSystemGroupAsync(groupTitle, participantIds);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(groupTitle);
        result.IsGroup.Should().BeTrue();

        _mockConversationRepository.Verify(r => r.AddAsync(It.Is<Conversation>(c =>
            c.CreatedByUserId == "system" &&
            c.Title == groupTitle &&
            c.IsGroup == true)), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateSystemGroupAsync_ExistingGroup_ReturnsExisting()
    {
        // Arrange
        var groupTitle = "TUNOSSAUROS";
        var participantIds = new List<string> { "user1", "user2", "user3" };

        var existingConversation = new Conversation
        {
            Id = 1,
            Title = groupTitle,
            IsGroup = true,
            CreatedByUserId = "system",
            Participants = "user1;user2;user3",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetGroupByTitleAsync(groupTitle))
            .ReturnsAsync(existingConversation);

        _mockUserManager.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationUser { Id = id, FirstName = "User", LastName = id });

        _mockMessageRepository.Setup(r => r.GetUnreadCountForConversationAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(0);

        // Act
        var result = await _service.GetOrCreateSystemGroupAsync(groupTitle, participantIds);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(1);

        // Should not create new conversation
        _mockConversationRepository.Verify(r => r.AddAsync(It.IsAny<Conversation>()), Times.Never);
    }

    [Fact]
    public async Task UpdateGroupParticipantsAsync_ValidGroup_UpdatesParticipants()
    {
        // Arrange
        var conversationId = 1;
        var newParticipantIds = new List<string> { "user1", "user2", "user3", "user4" };

        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "Test Group",
            IsGroup = true,
            Participants = "user1;user2;user3",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act
        await _service.UpdateGroupParticipantsAsync(conversationId, newParticipantIds);

        // Assert
        _mockConversationRepository.Verify(r => r.UpdateAsync(It.Is<Conversation>(c =>
            c.GetParticipantIds().Count == 4)), Times.Once);
    }

    [Fact]
    public async Task UpdateGroupParticipantsAsync_NotAGroup_ThrowsException()
    {
        // Arrange
        var conversationId = 1;
        var newParticipantIds = new List<string> { "user1", "user2" };

        var conversation = new Conversation
        {
            Id = conversationId,
            IsGroup = false,
            Participants = "user1;user2",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateGroupParticipantsAsync(conversationId, newParticipantIds));
    }

    // Mute/Pin tests

    [Fact]
    public async Task ToggleMuteAsync_NotMuted_SetsMutedTrue()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        var settings = new ConversationUserSettings
        {
            Id = 1,
            ConversationId = conversationId,
            UserId = userId,
            IsMuted = false,
            IsPinned = false
        };

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(userId, conversationId))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.ToggleMuteAsync(conversationId, userId);

        // Assert
        result.Should().BeTrue();
        settings.IsMuted.Should().BeTrue();
        _mockSettingsRepository.Verify(r => r.UpdateAsync(It.Is<ConversationUserSettings>(s => s.IsMuted == true)), Times.Once);
    }

    [Fact]
    public async Task ToggleMuteAsync_AlreadyMuted_SetsMutedFalse()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        var settings = new ConversationUserSettings
        {
            Id = 1,
            ConversationId = conversationId,
            UserId = userId,
            IsMuted = true,
            IsPinned = false
        };

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(userId, conversationId))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.ToggleMuteAsync(conversationId, userId);

        // Assert
        result.Should().BeFalse();
        settings.IsMuted.Should().BeFalse();
        _mockSettingsRepository.Verify(r => r.UpdateAsync(It.Is<ConversationUserSettings>(s => s.IsMuted == false)), Times.Once);
    }

    [Fact]
    public async Task TogglePinAsync_NotPinned_SetsPinnedTrue()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        var settings = new ConversationUserSettings
        {
            Id = 1,
            ConversationId = conversationId,
            UserId = userId,
            IsMuted = false,
            IsPinned = false
        };

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(userId, conversationId))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.TogglePinAsync(conversationId, userId);

        // Assert
        result.Should().BeTrue();
        settings.IsPinned.Should().BeTrue();
        _mockSettingsRepository.Verify(r => r.UpdateAsync(It.Is<ConversationUserSettings>(s => s.IsPinned == true)), Times.Once);
    }

    [Fact]
    public async Task TogglePinAsync_AlreadyPinned_SetsPinnedFalse()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        var settings = new ConversationUserSettings
        {
            Id = 1,
            ConversationId = conversationId,
            UserId = userId,
            IsMuted = false,
            IsPinned = true
        };

        _mockSettingsRepository.Setup(r => r.GetOrCreateAsync(userId, conversationId))
            .ReturnsAsync(settings);

        // Act
        var result = await _service.TogglePinAsync(conversationId, userId);

        // Assert
        result.Should().BeFalse();
        settings.IsPinned.Should().BeFalse();
        _mockSettingsRepository.Verify(r => r.UpdateAsync(It.Is<ConversationUserSettings>(s => s.IsPinned == false)), Times.Once);
    }

    [Fact]
    public async Task IsConversationMutedAsync_WhenMuted_ReturnsTrue()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        _mockSettingsRepository.Setup(r => r.IsConversationMutedAsync(userId, conversationId))
            .ReturnsAsync(true);

        // Act
        var result = await _service.IsConversationMutedAsync(conversationId, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsConversationMutedAsync_WhenNotMuted_ReturnsFalse()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        _mockSettingsRepository.Setup(r => r.IsConversationMutedAsync(userId, conversationId))
            .ReturnsAsync(false);

        // Act
        var result = await _service.IsConversationMutedAsync(conversationId, userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendDirectMessageAsync_ReceiverHasMuted_SkipsPushNotification()
    {
        // Arrange
        var senderId = "sender123";
        var receiverId = "receiver456";
        var messageText = "Test message";

        var conversation = new Conversation
        {
            Id = 1,
            Participants = $"{senderId};{receiverId}",
            LastMessageAt = DateTime.UtcNow
        };

        var sender = new ApplicationUser
        {
            Id = senderId,
            Nickname = "Sender Nick",
            FirstName = "John",
            LastName = "Doe"
        };

        _mockConversationRepository.Setup(r => r.GetOrCreateOneToOneAsync(senderId, receiverId))
            .ReturnsAsync(conversation);

        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m);

        _mockUserManager.Setup(um => um.FindByIdAsync(senderId))
            .ReturnsAsync(sender);

        // Receiver has muted the conversation
        _mockSettingsRepository.Setup(r => r.IsConversationMutedAsync(receiverId, conversation.Id))
            .ReturnsAsync(true);

        var messageDto = new SendMessageDto
        {
            ReceiverId = receiverId,
            Body = messageText
        };

        // Act
        var result = await _service.SendDirectMessageAsync(senderId, messageDto);

        // Assert
        result.Should().NotBeNull();
        result.Body.Should().Be(messageText);

        // Push notification should NOT be sent because receiver has muted
        _mockPushService.Verify(p => p.SendPushOnlyAsync(
            It.IsAny<string>(),
            It.IsAny<SendPushNotificationDto>()),
            Times.Never);
    }

    [Fact]
    public async Task SendGroupMessageAsync_ParticipantHasMuted_SkipsPushForMutedUser()
    {
        // Arrange
        var senderId = "sender123";
        var conversationId = 1;
        var messageBody = "Hello group!";

        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "Test Group",
            IsGroup = true,
            Participants = $"{senderId};user1;user2",
            LastMessageAt = DateTime.UtcNow
        };

        var sender = new ApplicationUser
        {
            Id = senderId,
            Nickname = "Sender Nick",
            FirstName = "John",
            LastName = "Doe"
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Simulate AddAsync assigning an ID to the message (as database would do)
        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => { m.Id = 1; return m; });

        _mockUserManager.Setup(um => um.FindByIdAsync(senderId))
            .ReturnsAsync(sender);

        // user1 has muted, user2 has not
        _mockSettingsRepository.Setup(r => r.IsConversationMutedAsync("user1", conversationId))
            .ReturnsAsync(true);
        _mockSettingsRepository.Setup(r => r.IsConversationMutedAsync("user2", conversationId))
            .ReturnsAsync(false);

        // Act
        var result = await _service.SendGroupMessageAsync(senderId, conversationId, messageBody);

        // Assert
        result.Should().NotBeNull();
        result.Body.Should().Be(messageBody);

        // Push notification should NOT be sent to user1 (muted)
        _mockPushService.Verify(p => p.SendPushOnlyAsync(
            "user1",
            It.IsAny<SendPushNotificationDto>()),
            Times.Never);

        // Push notification SHOULD be sent to user2 (not muted)
        _mockPushService.Verify(p => p.SendPushOnlyAsync(
            "user2",
            It.IsAny<SendPushNotificationDto>()),
            Times.Once);
    }

    // Announcement channel tests

    [Fact]
    public async Task CanUserSendMessageAsync_AnnouncementChannel_UserWithMagisterPosition_ReturnsTrue()
    {
        // Arrange
        var userId = "user123";
        var conversationId = 1;
        var (startYear, endYear) = GetCurrentFiscalYear();

        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "ANUNCIOS",
            IsGroup = true,
            IsAnnouncementOnly = true,
            Participants = $"{userId};user2;user3",
            LastMessageAt = DateTime.UtcNow
        };

        var roleAssignments = new List<RoleAssignment>
        {
            new RoleAssignment { UserId = userId, Position = Position.Magister, StartYear = startYear, EndYear = endYear }
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockRoleAssignmentRepository.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(roleAssignments);

        // Act
        var result = await _service.CanUserSendMessageAsync(conversationId, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserSendMessageAsync_AnnouncementChannel_UserWithoutRequiredPosition_ReturnsFalse()
    {
        // Arrange
        var userId = "user123";
        var conversationId = 1;
        var (startYear, endYear) = GetCurrentFiscalYear();

        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "ANUNCIOS",
            IsGroup = true,
            IsAnnouncementOnly = true,
            Participants = $"{userId};user2;user3",
            LastMessageAt = DateTime.UtcNow
        };

        // User has a position but not one that allows sending to announcement channels
        var roleAssignments = new List<RoleAssignment>
        {
            new RoleAssignment { UserId = userId, Position = Position.Secretario, StartYear = startYear, EndYear = endYear }
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockRoleAssignmentRepository.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(roleAssignments);

        // Act
        var result = await _service.CanUserSendMessageAsync(conversationId, userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserSendMessageAsync_AnnouncementChannel_UserWithNoPosition_ReturnsFalse()
    {
        // Arrange
        var userId = "user123";
        var conversationId = 1;

        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "ANUNCIOS",
            IsGroup = true,
            IsAnnouncementOnly = true,
            Participants = $"{userId};user2;user3",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockRoleAssignmentRepository.Setup(r => r.GetByUserIdAsync(userId))
            .ReturnsAsync(new List<RoleAssignment>());

        // Act
        var result = await _service.CanUserSendMessageAsync(conversationId, userId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserSendMessageAsync_NormalGroupConversation_ReturnsTrue()
    {
        // Arrange
        var userId = "user123";
        var conversationId = 1;

        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "Test Group",
            IsGroup = true,
            IsAnnouncementOnly = false,
            Participants = $"{userId};user2;user3",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        // Act
        var result = await _service.CanUserSendMessageAsync(conversationId, userId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendGroupMessageAsync_AnnouncementChannel_UserWithoutPermission_ThrowsException()
    {
        // Arrange
        var senderId = "sender123";
        var conversationId = 1;
        var messageBody = "Hello!";

        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "ANUNCIOS",
            IsGroup = true,
            IsAnnouncementOnly = true,
            Participants = $"{senderId};user2;user3",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockRoleAssignmentRepository.Setup(r => r.GetByUserIdAsync(senderId))
            .ReturnsAsync(new List<RoleAssignment>());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.SendGroupMessageAsync(senderId, conversationId, messageBody));
    }

    [Fact]
    public async Task SendGroupMessageAsync_AnnouncementChannel_UserWithMagisterPosition_SendsSuccessfully()
    {
        // Arrange
        var senderId = "sender123";
        var conversationId = 1;
        var messageBody = "Important announcement!";
        var (startYear, endYear) = GetCurrentFiscalYear();

        var conversation = new Conversation
        {
            Id = conversationId,
            Title = "ANUNCIOS",
            IsGroup = true,
            IsAnnouncementOnly = true,
            Participants = $"{senderId};user2;user3",
            LastMessageAt = DateTime.UtcNow
        };

        var sender = new ApplicationUser
        {
            Id = senderId,
            Nickname = "Magister Nick",
            FirstName = "John",
            LastName = "Doe"
        };

        var roleAssignments = new List<RoleAssignment>
        {
            new RoleAssignment { UserId = senderId, Position = Position.Magister, StartYear = startYear, EndYear = endYear }
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockRoleAssignmentRepository.Setup(r => r.GetByUserIdAsync(senderId))
            .ReturnsAsync(roleAssignments);

        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => { m.Id = 1; return m; });

        _mockUserManager.Setup(um => um.FindByIdAsync(senderId))
            .ReturnsAsync(sender);

        // Act
        var result = await _service.SendGroupMessageAsync(senderId, conversationId, messageBody);

        // Assert
        result.Should().NotBeNull();
        result.Body.Should().Be(messageBody);
    }

    [Fact]
    public async Task GetOrCreateAnnouncementGroupAsync_NewGroup_CreatesWithAnnouncementFlag()
    {
        // Arrange
        var groupTitle = "ANUNCIOS";
        var participantIds = new List<string> { "user1", "user2", "user3" };

        _mockConversationRepository.Setup(r => r.GetGroupByTitleAsync(groupTitle))
            .ReturnsAsync((Conversation?)null);

        _mockConversationRepository.Setup(r => r.AddAsync(It.IsAny<Conversation>()))
            .ReturnsAsync((Conversation c) => { c.Id = 1; return c; });

        _mockUserManager.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationUser { Id = id, FirstName = "User", LastName = id });

        _mockMessageRepository.Setup(r => r.GetUnreadCountForConversationAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(0);

        _mockRoleAssignmentRepository.Setup(r => r.GetByUserIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<RoleAssignment>());

        // Act
        var result = await _service.GetOrCreateAnnouncementGroupAsync(groupTitle, participantIds);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(groupTitle);
        result.IsGroup.Should().BeTrue();
        result.IsAnnouncementOnly.Should().BeTrue();

        _mockConversationRepository.Verify(r => r.AddAsync(It.Is<Conversation>(c =>
            c.IsAnnouncementOnly == true &&
            c.Title == groupTitle &&
            c.CreatedByUserId == "system")), Times.Once);
    }

    // CanDelete property tests

    [Fact]
    public async Task GetConversationAsync_OneToOneConversation_CanDeleteIsTrue()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";
        var otherUserId = "otherUser";

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = $"{userId};{otherUserId}",
            LastMessageAt = DateTime.UtcNow
        };

        var otherUser = new ApplicationUser
        {
            Id = otherUserId,
            FirstName = "Other",
            LastName = "User"
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockSettingsRepository.Setup(r => r.GetByUserAndConversationAsync(userId, conversationId))
            .ReturnsAsync((ConversationUserSettings?)null);

        _mockUserManager.Setup(um => um.FindByIdAsync(otherUserId))
            .ReturnsAsync(otherUser);

        _mockMessageRepository.Setup(r => r.GetUnreadCountForConversationAsync(conversationId, userId))
            .ReturnsAsync(0);

        // Act
        var result = await _service.GetConversationAsync(conversationId, userId);

        // Assert
        result.Should().NotBeNull();
        result!.CanDelete.Should().BeTrue();
    }

    [Fact]
    public async Task GetConversationAsync_SystemConversation_CanDeleteIsFalse()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = userId,
            IsSystemConversation = true,
            Title = "Sistema RTUB",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockSettingsRepository.Setup(r => r.GetByUserAndConversationAsync(userId, conversationId))
            .ReturnsAsync((ConversationUserSettings?)null);

        _mockMessageRepository.Setup(r => r.GetUnreadCountForConversationAsync(conversationId, userId))
            .ReturnsAsync(0);

        // Act
        var result = await _service.GetConversationAsync(conversationId, userId);

        // Assert
        result.Should().NotBeNull();
        result!.CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task GetConversationAsync_SystemGroup_CanDeleteIsFalse()
    {
        // Arrange
        var conversationId = 1;
        var userId = "user123";

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = $"{userId};user2;user3",
            IsGroup = true,
            CreatedByUserId = "system",
            Title = "TUNOSSAUROS",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockSettingsRepository.Setup(r => r.GetByUserAndConversationAsync(userId, conversationId))
            .ReturnsAsync((ConversationUserSettings?)null);

        _mockUserManager.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationUser { Id = id, FirstName = "User", LastName = id });

        _mockMessageRepository.Setup(r => r.GetUnreadCountForConversationAsync(conversationId, userId))
            .ReturnsAsync(0);

        // Act
        var result = await _service.GetConversationAsync(conversationId, userId);

        // Assert
        result.Should().NotBeNull();
        result!.CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task GetConversationAsync_GroupCreatedByUser_CanDeleteIsTrue()
    {
        // Arrange
        var conversationId = 1;
        var creatorId = "creator123";

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = $"{creatorId};user2;user3",
            IsGroup = true,
            CreatedByUserId = creatorId,
            Title = "My Group",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockSettingsRepository.Setup(r => r.GetByUserAndConversationAsync(creatorId, conversationId))
            .ReturnsAsync((ConversationUserSettings?)null);

        _mockUserManager.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationUser { Id = id, FirstName = "User", LastName = id });

        _mockMessageRepository.Setup(r => r.GetUnreadCountForConversationAsync(conversationId, creatorId))
            .ReturnsAsync(0);

        // Act
        var result = await _service.GetConversationAsync(conversationId, creatorId);

        // Assert
        result.Should().NotBeNull();
        result!.CanDelete.Should().BeTrue();
    }

    [Fact]
    public async Task GetConversationAsync_GroupNotCreatedByUser_CanDeleteIsFalse()
    {
        // Arrange
        var conversationId = 1;
        var nonCreatorId = "user123";

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = $"creator;{nonCreatorId};user3",
            IsGroup = true,
            CreatedByUserId = "creator",
            Title = "Other's Group",
            LastMessageAt = DateTime.UtcNow
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);

        _mockSettingsRepository.Setup(r => r.GetByUserAndConversationAsync(nonCreatorId, conversationId))
            .ReturnsAsync((ConversationUserSettings?)null);

        _mockUserManager.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => new ApplicationUser { Id = id, FirstName = "User", LastName = id });

        _mockMessageRepository.Setup(r => r.GetUnreadCountForConversationAsync(conversationId, nonCreatorId))
            .ReturnsAsync(0);

        // Act
        var result = await _service.GetConversationAsync(conversationId, nonCreatorId);

        // Assert
        result.Should().NotBeNull();
        result!.CanDelete.Should().BeFalse();
    }

    #region SignalR Hub Integration Tests

    [Fact]
    public async Task SendDirectMessageAsync_WithHubService_BroadcastsMessage()
    {
        // Arrange
        var senderId = "sender123";
        var receiverId = "receiver456";
        var messageText = "Test message";
        var conversationId = 1;

        var mockHubService = new Mock<IMessagesHubService>();
        var serviceWithHub = new MessagingService(
            _mockConversationRepository.Object,
            _mockMessageRepository.Object,
            _mockSettingsRepository.Object,
            _mockRoleAssignmentRepository.Object,
            _mockPushService.Object,
            _mockUserManager.Object,
            _mockLogger.Object,
            mockHubService.Object);

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = $"{senderId};{receiverId}",
            LastMessageAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var sender = new ApplicationUser
        {
            Id = senderId,
            FirstName = "Test",
            LastName = "Sender",
            Email = "sender@test.com",
            UserName = "sender@test.com"
        };

        _mockConversationRepository.Setup(r => r.GetOrCreateOneToOneAsync(senderId, receiverId))
            .ReturnsAsync(conversation);
        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m);
        _mockConversationRepository.Setup(r => r.UpdateAsync(It.IsAny<Conversation>()))
            .Returns(Task.CompletedTask);
        _mockUserManager.Setup(um => um.FindByIdAsync(senderId))
            .ReturnsAsync(sender);
        _mockSettingsRepository.Setup(r => r.IsConversationMutedAsync(receiverId, conversationId))
            .ReturnsAsync(false);

        var messageDto = new SendMessageDto
        {
            ReceiverId = receiverId,
            Body = messageText
        };

        // Act
        var result = await serviceWithHub.SendDirectMessageAsync(senderId, messageDto);

        // Assert
        result.Should().NotBeNull();
        result.Body.Should().Be(messageText);
        mockHubService.Verify(
            h => h.BroadcastMessageAsync(conversationId, It.Is<MessageDto>(m => m.Body == messageText)),
            Times.Once,
            "Hub service should broadcast the message to conversation participants");
    }

    [Fact]
    public async Task SendGroupMessageAsync_WithHubService_BroadcastsMessage()
    {
        // Arrange
        var senderId = "sender123";
        var conversationId = 1;
        var messageText = "Group message";

        var mockHubService = new Mock<IMessagesHubService>();
        var serviceWithHub = new MessagingService(
            _mockConversationRepository.Object,
            _mockMessageRepository.Object,
            _mockSettingsRepository.Object,
            _mockRoleAssignmentRepository.Object,
            _mockPushService.Object,
            _mockUserManager.Object,
            _mockLogger.Object,
            mockHubService.Object);

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = $"{senderId};user2;user3",
            IsGroup = true,
            Title = "Test Group",
            LastMessageAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var sender = new ApplicationUser
        {
            Id = senderId,
            FirstName = "Test",
            LastName = "Sender",
            Email = "sender@test.com",
            UserName = "sender@test.com"
        };

        _mockConversationRepository.Setup(r => r.GetByIdAsync(conversationId))
            .ReturnsAsync(conversation);
        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m);
        _mockConversationRepository.Setup(r => r.UpdateAsync(It.IsAny<Conversation>()))
            .Returns(Task.CompletedTask);
        _mockUserManager.Setup(um => um.FindByIdAsync(senderId))
            .ReturnsAsync(sender);
        _mockSettingsRepository.Setup(r => r.IsConversationMutedAsync(It.IsAny<string>(), conversationId))
            .ReturnsAsync(false);

        // Act
        var result = await serviceWithHub.SendGroupMessageAsync(senderId, conversationId, messageText);

        // Assert
        result.Should().NotBeNull();
        result.Body.Should().Be(messageText);
        mockHubService.Verify(
            h => h.BroadcastMessageAsync(conversationId, It.Is<MessageDto>(m => m.Body == messageText)),
            Times.Once,
            "Hub service should broadcast the message to group participants");
    }

    [Fact]
    public async Task MarkConversationAsReadAsync_WithHubService_NotifiesMessageSeen()
    {
        // Arrange
        var userId = "user123";
        var conversationId = 1;

        var mockHubService = new Mock<IMessagesHubService>();
        var serviceWithHub = new MessagingService(
            _mockConversationRepository.Object,
            _mockMessageRepository.Object,
            _mockSettingsRepository.Object,
            _mockRoleAssignmentRepository.Object,
            _mockPushService.Object,
            _mockUserManager.Object,
            _mockLogger.Object,
            mockHubService.Object);

        _mockMessageRepository.Setup(r => r.MarkConversationAsReadAsync(conversationId, userId))
            .Returns(Task.CompletedTask);

        // Act
        await serviceWithHub.MarkConversationAsReadAsync(conversationId, userId);

        // Assert
        mockHubService.Verify(
            h => h.NotifyMessageSeenAsync(conversationId, userId, It.IsAny<DateTime>()),
            Times.Once,
            "Hub service should notify that messages were seen");
    }

    [Fact]
    public async Task SendDirectMessageAsync_WithoutHubService_StillWorksCorrectly()
    {
        // Arrange - service without hub (null)
        var senderId = "sender123";
        var receiverId = "receiver456";
        var messageText = "Test message";
        var conversationId = 1;

        var conversation = new Conversation
        {
            Id = conversationId,
            Participants = $"{senderId};{receiverId}",
            LastMessageAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var sender = new ApplicationUser
        {
            Id = senderId,
            FirstName = "Test",
            LastName = "Sender",
            Email = "sender@test.com",
            UserName = "sender@test.com"
        };

        _mockConversationRepository.Setup(r => r.GetOrCreateOneToOneAsync(senderId, receiverId))
            .ReturnsAsync(conversation);
        _mockMessageRepository.Setup(r => r.AddAsync(It.IsAny<Message>()))
            .ReturnsAsync((Message m) => m);
        _mockConversationRepository.Setup(r => r.UpdateAsync(It.IsAny<Conversation>()))
            .Returns(Task.CompletedTask);
        _mockUserManager.Setup(um => um.FindByIdAsync(senderId))
            .ReturnsAsync(sender);
        _mockSettingsRepository.Setup(r => r.IsConversationMutedAsync(receiverId, conversationId))
            .ReturnsAsync(false);

        var messageDto = new SendMessageDto
        {
            ReceiverId = receiverId,
            Body = messageText
        };

        // Act
        var result = await _service.SendDirectMessageAsync(senderId, messageDto);

        // Assert
        result.Should().NotBeNull();
        result.Body.Should().Be(messageText);
        // No exception should be thrown even without hub service
    }

    #endregion
}
