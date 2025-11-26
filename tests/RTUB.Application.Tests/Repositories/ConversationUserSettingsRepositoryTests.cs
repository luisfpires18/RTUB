using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Repositories;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Repositories;

/// <summary>
/// Unit tests for ConversationUserSettingsRepository
/// Tests repository layer operations including batch operations
/// </summary>
public class ConversationUserSettingsRepositoryTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly ConversationUserSettingsRepository _repository;

    public ConversationUserSettingsRepositoryTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        CleanConversationData(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _repository = new ConversationUserSettingsRepository(_context);
    }

    private static async Task CleanConversationData(ApplicationDbContext context)
    {
        context.ConversationUserSettings.RemoveRange(context.ConversationUserSettings);
        context.Messages.RemoveRange(context.Messages);
        context.Conversations.RemoveRange(context.Conversations);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetMutedUserIdsAsync_WithEmptyUserIds_ReturnsEmptyHashSet()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2", LastMessageAt = DateTime.UtcNow };
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMutedUserIdsAsync(conversation.Id, Enumerable.Empty<string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMutedUserIdsAsync_WithNoMutedUsers_ReturnsEmptyHashSet()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2;user3", LastMessageAt = DateTime.UtcNow };
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        // Create settings but none are muted
        var settings1 = new ConversationUserSettings
        {
            ConversationId = conversation.Id,
            UserId = "user1",
            IsMuted = false,
            CreatedAt = DateTime.UtcNow
        };
        var settings2 = new ConversationUserSettings
        {
            ConversationId = conversation.Id,
            UserId = "user2",
            IsMuted = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.ConversationUserSettings.AddRange(settings1, settings2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMutedUserIdsAsync(conversation.Id, new[] { "user1", "user2", "user3" });

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMutedUserIdsAsync_WithSingleMutedUser_ReturnsMutedUserId()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2;user3", LastMessageAt = DateTime.UtcNow };
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        var settings = new ConversationUserSettings
        {
            ConversationId = conversation.Id,
            UserId = "user2",
            IsMuted = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ConversationUserSettings.Add(settings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMutedUserIdsAsync(conversation.Id, new[] { "user1", "user2", "user3" });

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("user2");
    }

    [Fact]
    public async Task GetMutedUserIdsAsync_WithMultipleMutedUsers_ReturnsAllMutedUserIds()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2;user3;user4", LastMessageAt = DateTime.UtcNow };
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        var settings1 = new ConversationUserSettings
        {
            ConversationId = conversation.Id,
            UserId = "user1",
            IsMuted = true,
            CreatedAt = DateTime.UtcNow
        };
        var settings2 = new ConversationUserSettings
        {
            ConversationId = conversation.Id,
            UserId = "user2",
            IsMuted = false,
            CreatedAt = DateTime.UtcNow
        };
        var settings3 = new ConversationUserSettings
        {
            ConversationId = conversation.Id,
            UserId = "user3",
            IsMuted = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ConversationUserSettings.AddRange(settings1, settings2, settings3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMutedUserIdsAsync(conversation.Id, new[] { "user1", "user2", "user3", "user4" });

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain("user1");
        result.Should().Contain("user3");
        result.Should().NotContain("user2");
        result.Should().NotContain("user4");
    }

    [Fact]
    public async Task GetMutedUserIdsAsync_OnlyReturnsUsersInProvidedList()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2;user3", LastMessageAt = DateTime.UtcNow };
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        var settings1 = new ConversationUserSettings
        {
            ConversationId = conversation.Id,
            UserId = "user1",
            IsMuted = true,
            CreatedAt = DateTime.UtcNow
        };
        var settings2 = new ConversationUserSettings
        {
            ConversationId = conversation.Id,
            UserId = "user2",
            IsMuted = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ConversationUserSettings.AddRange(settings1, settings2);
        await _context.SaveChangesAsync();

        // Act - Only request user1
        var result = await _repository.GetMutedUserIdsAsync(conversation.Id, new[] { "user1" });

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("user1");
        result.Should().NotContain("user2"); // user2 is muted but wasn't in the requested list
    }

    [Fact]
    public async Task GetMutedUserIdsAsync_DoesNotReturnSettingsFromOtherConversations()
    {
        // Arrange
        var conversation1 = new Conversation { Participants = "user1;user2", LastMessageAt = DateTime.UtcNow };
        var conversation2 = new Conversation { Participants = "user1;user2", LastMessageAt = DateTime.UtcNow };
        _context.Conversations.AddRange(conversation1, conversation2);
        await _context.SaveChangesAsync();

        // user1 muted in conversation1, user2 muted in conversation2
        var settings1 = new ConversationUserSettings
        {
            ConversationId = conversation1.Id,
            UserId = "user1",
            IsMuted = true,
            CreatedAt = DateTime.UtcNow
        };
        var settings2 = new ConversationUserSettings
        {
            ConversationId = conversation2.Id,
            UserId = "user2",
            IsMuted = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ConversationUserSettings.AddRange(settings1, settings2);
        await _context.SaveChangesAsync();

        // Act - Get muted users for conversation1
        var result = await _repository.GetMutedUserIdsAsync(conversation1.Id, new[] { "user1", "user2" });

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("user1");
        result.Should().NotContain("user2"); // user2 is muted in conversation2, not conversation1
    }

    [Fact]
    public async Task GetMutedUserIdsAsync_ReturnsHashSetForFastLookup()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2;user3", LastMessageAt = DateTime.UtcNow };
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        var settings = new ConversationUserSettings
        {
            ConversationId = conversation.Id,
            UserId = "user2",
            IsMuted = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ConversationUserSettings.Add(settings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMutedUserIdsAsync(conversation.Id, new[] { "user1", "user2", "user3" });

        // Assert - Verify it's a HashSet and can be used for fast lookups
        result.Should().BeOfType<HashSet<string>>();
        result.Contains("user2").Should().BeTrue();
        result.Contains("user1").Should().BeFalse();
    }

    [Fact]
    public async Task GetMutedUserIdsAsync_HandlesUsersWithNoSettings()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2;user3", LastMessageAt = DateTime.UtcNow };
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        // Only create settings for user1 (muted)
        var settings = new ConversationUserSettings
        {
            ConversationId = conversation.Id,
            UserId = "user1",
            IsMuted = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.ConversationUserSettings.Add(settings);
        await _context.SaveChangesAsync();

        // Act - Request all users, including those without settings
        var result = await _repository.GetMutedUserIdsAsync(conversation.Id, new[] { "user1", "user2", "user3" });

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain("user1");
        // user2 and user3 have no settings, so they're not muted
    }

    [Fact]
    public async Task GetMutedUserIdsAsync_HandlesPinnedButNotMuted()
    {
        // Arrange
        var conversation = new Conversation { Participants = "user1;user2", LastMessageAt = DateTime.UtcNow };
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();

        var settings = new ConversationUserSettings
        {
            ConversationId = conversation.Id,
            UserId = "user1",
            IsMuted = false,
            IsPinned = true, // Pinned but not muted
            CreatedAt = DateTime.UtcNow
        };
        _context.ConversationUserSettings.Add(settings);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetMutedUserIdsAsync(conversation.Id, new[] { "user1", "user2" });

        // Assert
        result.Should().BeEmpty(); // Neither user is muted
    }

    public void Dispose()
    {
        CleanConversationData(_context).GetAwaiter().GetResult();
        _fixture.CleanDatabase(_context).GetAwaiter().GetResult();
        _context.Dispose();
    }
}
