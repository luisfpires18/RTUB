using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for EventService
/// Tests business logic and service layer operations
/// </summary>
public class EventServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly EventService _eventService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;

    public EventServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), new AuditContext());
        _mockImageStorageService = new Mock<IImageStorageService>();
        _eventService = new EventService(_context, _mockImageStorageService.Object);
    }

    [Fact]
    public async Task CreateEventAsync_WithValidData_ReturnsEvent()
    {
        // Arrange
        var name = "Test Event";
        var date = DateTime.Now.AddDays(7);
        var location = "Test Location";
        var type = EventType.Festival;
        var description = "Test Description";

        // Act
        var result = await _eventService.CreateEventAsync(name, date, location, type, description);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Location.Should().Be(location);
        result.Type.Should().Be(type);
        result.Description.Should().Be(description);
    }

    [Fact]
    public async Task GetEventByIdAsync_ExistingEvent_ReturnsEvent()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now, "Test Location", EventType.Festival);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetEventByIdAsync(eventEntity.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(eventEntity.Id);
        result.Name.Should().Be("Test Event");
    }

    [Fact]
    public async Task GetAllEventsAsync_WithMultipleEvents_ReturnsAllEvents()
    {
        // Arrange
        _context.Events.Add(Event.Create("Event 1", DateTime.Now, "Location 1", EventType.Festival));
        _context.Events.Add(Event.Create("Event 2", DateTime.Now.AddDays(1), "Location 2", EventType.Atuacao));
        await _context.SaveChangesAsync();

        // Act
        var result = await _eventService.GetAllEventsAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_WithTodayEvent_IncludesEvent()
    {
        // Arrange
        var today = DateTime.Today;
        _context.Events.Add(Event.Create("Today Event", today, "Location", EventType.Festival));
        _context.Events.Add(Event.Create("Future Event", today.AddDays(1), "Location", EventType.Festival));
        _context.Events.Add(Event.Create("Past Event", today.AddDays(-1), "Location", EventType.Festival));
        await _context.SaveChangesAsync();

        // Act
        var result = (await _eventService.GetUpcomingEventsAsync(10)).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Name == "Today Event");
        result.Should().Contain(e => e.Name == "Future Event");
        result.Should().NotContain(e => e.Name == "Past Event");
    }

    [Fact]
    public async Task GetPastEventsAsync_WithTodayEvent_ExcludesEvent()
    {
        // Arrange
        var today = DateTime.Today;
        _context.Events.Add(Event.Create("Today Event", today, "Location", EventType.Festival));
        _context.Events.Add(Event.Create("Future Event", today.AddDays(1), "Location", EventType.Festival));
        _context.Events.Add(Event.Create("Past Event", today.AddDays(-1), "Location", EventType.Festival));
        await _context.SaveChangesAsync();

        // Act
        var result = (await _eventService.GetPastEventsAsync(10)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(e => e.Name == "Past Event");
        result.Should().NotContain(e => e.Name == "Today Event");
        result.Should().NotContain(e => e.Name == "Future Event");
    }

    [Fact]
    public async Task GetUpcomingEventsAsync_WithEventEndingToday_IncludesEvent()
    {
        // Arrange
        var today = DateTime.Today;
        var eventEntity = Event.Create("Multi-day Event", today.AddDays(-2), "Location", EventType.Festival);
        eventEntity.SetEndDate(today);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _eventService.GetUpcomingEventsAsync(10)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(e => e.Name == "Multi-day Event");
    }

    [Fact]
    public async Task GetPastEventsAsync_WithEventEndingYesterday_IncludesEvent()
    {
        // Arrange
        var today = DateTime.Today;
        var eventEntity = Event.Create("Multi-day Event", today.AddDays(-3), "Location", EventType.Festival);
        eventEntity.SetEndDate(today.AddDays(-1));
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();

        // Act
        var result = (await _eventService.GetPastEventsAsync(10)).ToList();

        // Assert
        result.Should().HaveCount(1);
        result.Should().Contain(e => e.Name == "Multi-day Event");
    }
    
    [Fact]
    public async Task CancelEventAsync_WithValidReason_CancelsEvent()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();
        var eventId = eventEntity.Id;
        var cancellationReason = "Mau tempo previsto";

        // Act
        await _eventService.CancelEventAsync(eventId, cancellationReason);

        // Assert
        var cancelledEvent = await _context.Events.FindAsync(eventId);
        cancelledEvent.Should().NotBeNull();
        cancelledEvent!.IsCancelled.Should().BeTrue();
        cancelledEvent.CancellationReason.Should().Be(cancellationReason);
    }
    
    [Fact]
    public async Task CancelEventAsync_WithNonExistentEvent_ThrowsException()
    {
        // Arrange
        var nonExistentEventId = 9999;
        var cancellationReason = "Mau tempo previsto";

        // Act & Assert
        var act = async () => await _eventService.CancelEventAsync(nonExistentEventId, cancellationReason);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Event with ID {nonExistentEventId} not found");
    }
    
    [Fact]
    public async Task UncancelEventAsync_WithCancelledEvent_UncancelsEvent()
    {
        // Arrange
        var eventEntity = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        eventEntity.Cancel("Mau tempo previsto");
        _context.Events.Add(eventEntity);
        await _context.SaveChangesAsync();
        var eventId = eventEntity.Id;

        // Act
        await _eventService.UncancelEventAsync(eventId);

        // Assert
        var uncancelledEvent = await _context.Events.FindAsync(eventId);
        uncancelledEvent.Should().NotBeNull();
        uncancelledEvent!.IsCancelled.Should().BeFalse();
        uncancelledEvent.CancellationReason.Should().BeNull();
    }
    
    [Fact]
    public async Task UncancelEventAsync_WithNonExistentEvent_ThrowsException()
    {
        // Arrange
        var nonExistentEventId = 9999;

        // Act & Assert
        var act = async () => await _eventService.UncancelEventAsync(nonExistentEventId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Event with ID {nonExistentEventId} not found");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
