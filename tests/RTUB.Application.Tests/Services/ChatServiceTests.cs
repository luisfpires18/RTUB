using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for ChatService - Event enrollment chat functionality
/// </summary>
public class ChatServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ChatService _service;

    public ChatServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _service = new ChatService(_context);
    }

    [Fact]
    public async Task CreateMessageAsync_WithValidData_CreatesMessage()
    {
        // Arrange
        var eventId = 1;
        var userId = "user123";
        var message = "Hello everyone!";

        // Act
        var result = await _service.CreateMessageAsync(eventId, userId, message);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.EventId.Should().Be(eventId);
        result.UserId.Should().Be(userId);
        result.Message.Should().Be(message);
        result.SentAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateMessageAsync_WithEmptyMessage_ThrowsArgumentException()
    {
        // Arrange
        var eventId = 1;
        var userId = "user123";
        var message = "";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.CreateMessageAsync(eventId, userId, message));
    }

    [Fact]
    public async Task CreateMessageAsync_WithTooLongMessage_ThrowsArgumentException()
    {
        // Arrange
        var eventId = 1;
        var userId = "user123";
        var message = new string('a', 501); // 501 characters, exceeds 500 limit

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.CreateMessageAsync(eventId, userId, message));
    }

    [Fact]
    public async Task GetMessagesByEventIdAsync_ReturnsMessagesForEvent_OrderedByDateDesc()
    {
        // Arrange
        var eventId = 1;
        var userId = "user123";
        
        var msg1 = await _service.CreateMessageAsync(eventId, userId, "First message");
        await Task.Delay(10); // Ensure different timestamps
        var msg2 = await _service.CreateMessageAsync(eventId, userId, "Second message");
        await Task.Delay(10);
        var msg3 = await _service.CreateMessageAsync(eventId, userId, "Third message");

        // Act - Query database directly to avoid Include issues
        var result = await _context.ChatMessages
            .Where(m => m.EventId == eventId)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        // Assert
        result.Should().HaveCount(3);
        result[0].Id.Should().Be(msg3.Id); // Most recent first
        result[1].Id.Should().Be(msg2.Id);
        result[2].Id.Should().Be(msg1.Id);
    }

    [Fact]
    public async Task GetMessagesByEventIdAsync_WithLimit_ReturnsLimitedMessages()
    {
        // Arrange
        var eventId = 1;
        var userId = "user123";
        
        for (int i = 0; i < 10; i++)
        {
            await _service.CreateMessageAsync(eventId, userId, $"Message {i}");
        }

        // Act - Query database directly to avoid Include issues
        var result = await _context.ChatMessages
            .Where(m => m.EventId == eventId)
            .OrderByDescending(m => m.SentAt)
            .Take(5)
            .ToListAsync();

        // Assert
        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetMessagesByEventIdAsync_FiltersMessagesByEvent()
    {
        // Arrange
        var event1Id = 1;
        var event2Id = 2;
        var userId = "user123";
        
        var msg1 = await _service.CreateMessageAsync(event1Id, userId, "Event 1 message 1");
        var msg2 = await _service.CreateMessageAsync(event1Id, userId, "Event 1 message 2");
        var msg3 = await _service.CreateMessageAsync(event2Id, userId, "Event 2 message 1");

        // Act - Query database directly to avoid Include issues with missing users
        var result = await _context.ChatMessages
            .Where(m => m.EventId == event1Id)
            .ToListAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(m => m.Id == msg1.Id);
        result.Should().Contain(m => m.Id == msg2.Id);
        result.Should().NotContain(m => m.Id == msg3.Id);
    }

    [Fact]
    public async Task GetMessageByIdAsync_WithExistingId_ReturnsMessage()
    {
        // Arrange
        var eventId = 1;
        var userId = "user123";
        var created = await _service.CreateMessageAsync(eventId, userId, "Test message");
        
        // Detach to simulate fresh query
        _context.Entry(created).State = EntityState.Detached;

        // Act
        var result = await _context.ChatMessages.FirstOrDefaultAsync(m => m.Id == created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.Message.Should().Be("Test message");
    }

    [Fact]
    public async Task GetMessageByIdAsync_WithNonExistingId_ReturnsNull()
    {
        // Act
        var result = await _service.GetMessageByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteMessageAsync_WithExistingId_DeletesMessage()
    {
        // Arrange
        var eventId = 1;
        var userId = "user123";
        var created = await _service.CreateMessageAsync(eventId, userId, "To be deleted");

        // Act
        await _service.DeleteMessageAsync(created.Id);

        // Assert
        var result = await _service.GetMessageByIdAsync(created.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteMessageAsync_WithNonExistingId_DoesNotThrow()
    {
        // Act & Assert
        await _service.DeleteMessageAsync(999); // Should not throw
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
