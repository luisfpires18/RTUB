using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for MessagingService
/// Tests business logic for internal messaging system
/// </summary>
public class MessagingServiceTests
{
    private readonly Mock<IConversationRepository> _mockConversationRepository;
    private readonly Mock<IMessageRepository> _mockMessageRepository;
    private readonly Mock<IPushNotificationService> _mockPushService;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<ILogger<MessagingService>> _mockLogger;
    private readonly MessagingService _service;

    public MessagingServiceTests()
    {
        _mockConversationRepository = new Mock<IConversationRepository>();
        _mockMessageRepository = new Mock<IMessageRepository>();
        _mockPushService = new Mock<IPushNotificationService>();
        _mockLogger = new Mock<ILogger<MessagingService>>();

        // Mock UserManager (requires store mock)
        var store = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _service = new MessagingService(
            _mockConversationRepository.Object,
            _mockMessageRepository.Object,
            _mockPushService.Object,
            _mockUserManager.Object,
            _mockLogger.Object);
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
    public async Task DeleteConversationAsync_ValidConversation_ArchivesConversation()
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
        await _service.DeleteConversationAsync(conversationId, userId);

        // Assert
        _mockConversationRepository.Verify(r => r.ArchiveConversationAsync(conversationId), Times.Once);
    }

    [Fact]
    public async Task DeleteConversationAsync_UserNotParticipant_DoesNotArchive()
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
        await _service.DeleteConversationAsync(conversationId, userId);

        // Assert
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
}
