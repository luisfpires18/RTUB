using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Repositories;

/// <summary>
/// Unit tests for MessageRepository
/// Tests repository layer operations including batch loading and AsNoTracking usage
/// </summary>
public class MessageRepositoryTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly MessageRepository _repository;

    public MessageRepositoryTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _repository = new MessageRepository(_context);
    }

    private ApplicationUser CreateTestUser(string userId, string email = "test@test.com")
    {
        // Check if user already exists
        var existingUser = _context.Users.Find(userId);
        if (existingUser != null)
        {
            return existingUser;
        }

        var user = new ApplicationUser
        {
            Id = userId,
            UserName = userId,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestUser"
        };
        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private Conversation CreateTestConversation(string participants, bool isGroup = false)
    {
        var conversation = new Conversation
        {
            Participants = participants,
            LastMessageAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsGroup = isGroup
        };
        _context.Conversations.Add(conversation);
        _context.SaveChanges();
        return conversation;
    }

    private Message CreateTestMessage(int conversationId, string? senderId, string body = "Test message")
    {
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Body = body,
            CreatedAt = DateTime.UtcNow
        };
        _context.Messages.Add(message);
        _context.SaveChanges();
        return message;
    }

    [Fact]
    public async Task GetConversationMessagesAsync_WithMessages_ReturnsMessages()
    {
        // Arrange
        var user1 = CreateTestUser("user1");
        var conversation = CreateTestConversation("user1;user2");
        var message1 = CreateTestMessage(conversation.Id, user1.Id, "Message 1");
        var message2 = CreateTestMessage(conversation.Id, user1.Id, "Message 2");
        var message3 = CreateTestMessage(conversation.Id, user1.Id, "Message 3");

        // Act
        var result = await _repository.GetConversationMessagesAsync(conversation.Id);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInDescendingOrder(m => m.CreatedAt);
        result.First().Id.Should().Be(message3.Id);
    }

    [Fact]
    public async Task GetConversationMessagesAsync_WithLimit_ReturnsLimitedMessages()
    {
        // Arrange
        var user1 = CreateTestUser("user1");
        var conversation = CreateTestConversation("user1;user2");
        CreateTestMessage(conversation.Id, user1.Id, "Message 1");
        CreateTestMessage(conversation.Id, user1.Id, "Message 2");
        CreateTestMessage(conversation.Id, user1.Id, "Message 3");

        // Act
        var result = await _repository.GetConversationMessagesAsync(conversation.Id, limit: 2);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetConversationMessagesAsync_WithOffset_ReturnsMessagesWithOffset()
    {
        // Arrange
        var user1 = CreateTestUser("user1");
        var conversation = CreateTestConversation("user1;user2");
        var message1 = CreateTestMessage(conversation.Id, user1.Id, "Message 1");
        var message2 = CreateTestMessage(conversation.Id, user1.Id, "Message 2");
        var message3 = CreateTestMessage(conversation.Id, user1.Id, "Message 3");

        // Act
        var result = await _repository.GetConversationMessagesAsync(conversation.Id, limit: 2, offset: 1);

        // Assert
        result.Should().HaveCount(2);
        result.Should().NotContain(m => m.Id == message3.Id);
    }

    [Fact]
    public async Task GetConversationMessagesAsync_IncludesSender()
    {
        // Arrange - Use unique IDs to avoid conflicts
        var user1 = CreateTestUser("sender-test-user1", "sender1@test.com");
        var conversation = CreateTestConversation($"sender-test-user1;user2-{Guid.NewGuid()}");
        var message = CreateTestMessage(conversation.Id, user1.Id);

        // Act
        var result = await _repository.GetConversationMessagesAsync(conversation.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().Sender.Should().NotBeNull();
        result.First().Sender!.Id.Should().Be(user1.Id);
        result.First().Sender!.Email.Should().Be("sender1@test.com");
    }

    [Fact]
    public async Task GetUnreadCountForUserAsync_WithUnreadMessages_ReturnsCount()
    {
        // Arrange - Use unique IDs to avoid conflicts with other tests
        var user1 = CreateTestUser("unread-test-user1", "unread1@test.com");
        var user2 = CreateTestUser("unread-test-user2", "unread2@test.com");
        var conversation = CreateTestConversation($"unread-test-user1;unread-test-user2");
        CreateTestMessage(conversation.Id, user2.Id, "Unread 1"); // user2 sent, user1 hasn't read
        CreateTestMessage(conversation.Id, user2.Id, "Unread 2");
        CreateTestMessage(conversation.Id, user1.Id, "Read"); // user1 sent, doesn't count

        // Act
        var result = await _repository.GetUnreadCountForUserAsync(user1.Id);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetUnreadCountForConversationAsync_WithUnreadMessages_ReturnsCount()
    {
        // Arrange
        var user1 = CreateTestUser("user1");
        var user2 = CreateTestUser("user2");
        var conversation = CreateTestConversation("user1;user2");
        CreateTestMessage(conversation.Id, user2.Id, "Unread 1");
        CreateTestMessage(conversation.Id, user2.Id, "Unread 2");

        // Act
        var result = await _repository.GetUnreadCountForConversationAsync(conversation.Id, user1.Id);

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetUnreadCountsForConversationsAsync_WithMultipleConversations_ReturnsCounts()
    {
        // Arrange
        var user1 = CreateTestUser("user1");
        var user2 = CreateTestUser("user2");
        var conversation1 = CreateTestConversation("user1;user2");
        var conversation2 = CreateTestConversation("user1;user2");
        CreateTestMessage(conversation1.Id, user2.Id, "Unread 1");
        CreateTestMessage(conversation1.Id, user2.Id, "Unread 2");
        CreateTestMessage(conversation2.Id, user2.Id, "Unread 3");

        // Act
        var result = await _repository.GetUnreadCountsForConversationsAsync(
            new[] { conversation1.Id, conversation2.Id }, user1.Id);

        // Assert
        result.Should().HaveCount(2);
        result[conversation1.Id].Should().Be(2);
        result[conversation2.Id].Should().Be(1);
    }

    [Fact]
    public async Task GetUnreadCountsForConversationsAsync_WithEmptyList_ReturnsEmptyDictionary()
    {
        // Arrange
        var user1 = CreateTestUser("user1");

        // Act
        var result = await _repository.GetUnreadCountsForConversationsAsync(Enumerable.Empty<int>(), user1.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUnreadCountsForConversationsAsync_WithNoUnreadMessages_ReturnsZeroCounts()
    {
        // Arrange
        var user1 = CreateTestUser("user1");
        var user2 = CreateTestUser("user2");
        var conversation = CreateTestConversation("user1;user2");
        var message = CreateTestMessage(conversation.Id, user2.Id, "Message");
        message.MarkAsReadBy(user1.Id);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetUnreadCountsForConversationsAsync(
            new[] { conversation.Id }, user1.Id);

        // Assert
        result.Should().HaveCount(1);
        result[conversation.Id].Should().Be(0);
    }

    [Fact]
    public async Task GetLatestMessagesForConversationsAsync_WithMultipleConversations_ReturnsLatestMessages()
    {
        // Arrange - Use unique IDs to avoid conflicts
        var user1 = CreateTestUser("latest-test-user1", "latest1@test.com");
        var conversation1 = CreateTestConversation($"latest-test-user1;user2-{Guid.NewGuid()}");
        var conversation2 = CreateTestConversation($"latest-test-user1;user2-{Guid.NewGuid()}");
        var message1 = CreateTestMessage(conversation1.Id, user1.Id, "Old");
        await Task.Delay(10); // Ensure different timestamps
        var message2 = CreateTestMessage(conversation1.Id, user1.Id, "Latest");
        var message3 = CreateTestMessage(conversation2.Id, user1.Id, "Only");

        // Act
        var result = await _repository.GetLatestMessagesForConversationsAsync(
            new[] { conversation1.Id, conversation2.Id });

        // Assert
        result.Should().HaveCount(2);
        result[conversation1.Id]!.Id.Should().Be(message2.Id);
        result[conversation2.Id]!.Id.Should().Be(message3.Id);
    }

    [Fact]
    public async Task GetLatestMessagesForConversationsAsync_WithEmptyList_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _repository.GetLatestMessagesForConversationsAsync(Enumerable.Empty<int>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLatestMessagesForConversationsAsync_WithNoMessages_ReturnsNullForConversation()
    {
        // Arrange
        var conversation = CreateTestConversation("user1;user2");

        // Act
        var result = await _repository.GetLatestMessagesForConversationsAsync(new[] { conversation.Id });

        // Assert
        result.Should().HaveCount(1);
        result[conversation.Id].Should().BeNull();
    }

    [Fact]
    public async Task MarkConversationAsReadAsync_MarksMessagesAsRead()
    {
        // Arrange
        var user1 = CreateTestUser("user1");
        var user2 = CreateTestUser("user2");
        var conversation = CreateTestConversation("user1;user2");
        var message1 = CreateTestMessage(conversation.Id, user2.Id, "Unread 1");
        var message2 = CreateTestMessage(conversation.Id, user2.Id, "Unread 2");

        // Act
        await _repository.MarkConversationAsReadAsync(conversation.Id, user1.Id);
        await _context.SaveChangesAsync();

        // Assert
        var updatedMessage1 = await _context.Messages.FindAsync(message1.Id);
        var updatedMessage2 = await _context.Messages.FindAsync(message2.Id);
        updatedMessage1!.ReadBy.Should().Contain(user1.Id);
        updatedMessage2!.ReadBy.Should().Contain(user1.Id);
    }

    [Fact]
    public async Task GetConversationMessagesAsync_DoesNotTrackApplicationUser()
    {
        // Arrange - GetConversationMessagesAsync uses AsNoTracking() to prevent tracking conflicts
        var user1 = CreateTestUser("tracking-test-user1", "tracking1@test.com");
        var conversation = CreateTestConversation("tracking-test-user1;user2");
        var message = CreateTestMessage(conversation.Id, user1.Id);

        // Act
        var result = await _repository.GetConversationMessagesAsync(conversation.Id);
        var firstMessage = result.First();

        // Modify the user through the non-tracked message
        firstMessage.Sender!.Email = "modified@test.com";
        await _context.SaveChangesAsync();

        // Verify user was NOT tracked (AsNoTracking() prevents tracking)
        var freshUser = await _context.Users.FindAsync(user1.Id);
        freshUser!.Email.Should().Be("tracking1@test.com", "ApplicationUser should NOT be tracked when using AsNoTracking()");
    }

    [Fact]
    public async Task GetLatestMessageAsync_ReturnsLatestMessage()
    {
        // Arrange
        var user1 = CreateTestUser("user1");
        var conversation = CreateTestConversation("user1;user2");
        var message1 = CreateTestMessage(conversation.Id, user1.Id, "Old");
        await Task.Delay(10);
        var message2 = CreateTestMessage(conversation.Id, user1.Id, "Latest");

        // Act
        var result = await _repository.GetLatestMessageAsync(conversation.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(message2.Id);
        result.Body.Should().Be("Latest");
    }

    [Fact]
    public async Task GetLatestMessageAsync_WithNoMessages_ReturnsNull()
    {
        // Arrange
        var conversation = CreateTestConversation("user1;user2");

        // Act
        var result = await _repository.GetLatestMessageAsync(conversation.Id);

        // Assert
        result.Should().BeNull();
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
