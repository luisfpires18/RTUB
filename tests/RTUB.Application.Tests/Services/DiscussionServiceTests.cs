using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Tests.Fixtures;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for DiscussionService
/// Tests business logic and service layer operations
/// </summary>
public class DiscussionServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly DiscussionService _service;

    public DiscussionServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();
        
        _fixture = fixture;
        _context = _fixture.CreateContext();
        _service = new DiscussionService(_context);
    }

    [Fact]
    public async Task CreateForEventAsync_WithValidEventId_CreatesDiscussion()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.CreateForEventAsync(eventEntity.Id);

        // Assert
        result.Should().NotBeNull();
        result.EventId.Should().Be(eventEntity.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingDiscussion_ReturnsDiscussion()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByIdAsync(discussion.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(discussion.Id);
        result.EventId.Should().Be(eventEntity.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingDiscussion_ReturnsNull()
    {
        // Act
        var result = await _service.GetByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEventIdAsync_ExistingDiscussion_ReturnsDiscussion()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByEventIdAsync(eventEntity.Id);

        // Assert
        result.Should().NotBeNull();
        result!.EventId.Should().Be(eventEntity.Id);
    }

    [Fact]
    public async Task GetByEventIdAsync_NonExistingDiscussion_ReturnsNull()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetByEventIdAsync(eventEntity.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrCreateForEventAsync_ExistingDiscussion_ReturnsExisting()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        var discussion = Discussion.Create(eventEntity.Id);
        _context.Discussions.Add(discussion);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetOrCreateForEventAsync(eventEntity.Id);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(discussion.Id);
        
        // Verify no new discussion was created
        var allDiscussions = await _context.Discussions.ToListAsync();
        allDiscussions.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOrCreateForEventAsync_NoExistingDiscussion_CreatesNew()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetOrCreateForEventAsync(eventEntity.Id);

        // Assert
        result.Should().NotBeNull();
        result.EventId.Should().Be(eventEntity.Id);
        
        // Verify discussion was created
        var allDiscussions = await _context.Discussions.ToListAsync();
        allDiscussions.Should().HaveCount(1);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
