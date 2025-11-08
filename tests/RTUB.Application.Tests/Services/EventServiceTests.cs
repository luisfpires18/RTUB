using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
    private readonly Mock<IImageService> _mockImageService;
    private readonly Mock<IEventStorageService> _mockEventStorageService;

    public EventServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _mockImageService = new Mock<IImageService>();
        _mockEventStorageService = new Mock<IEventStorageService>();
        _eventService = new EventService(_context, _mockImageService.Object, _mockEventStorageService.Object);
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

    public void Dispose()
    {
        _context?.Dispose();
    }
}
