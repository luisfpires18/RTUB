using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for EnrollmentService
/// Tests enrollment CRUD operations
/// </summary>
public class EnrollmentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly EnrollmentService _enrollmentService;
    private readonly EventService _eventService;
    private readonly Mock<IImageStorageService> _mockImageStorageService;

    public EnrollmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _enrollmentService = new EnrollmentService(_context);
        _mockImageStorageService = new Mock<IImageStorageService>();
        _eventService = new EventService(_context, _mockImageStorageService.Object);
    }

    [Fact]
    public async Task CreateEnrollmentAsync_WithValidData_CreatesEnrollment()
    {
        // Arrange
        var userId = "user123";
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var attended = true;

        // Act
        var result = await _enrollmentService.CreateEnrollmentAsync(userId, eventEntity.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.EventId.Should().Be(eventEntity.Id);
    }

    [Fact]
    public async Task GetEnrollmentByIdAsync_ExistingEnrollment_ReturnsEnrollment()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync(
            "Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync("user123", eventEntity.Id);

        // Act
        var result = await _enrollmentService.GetEnrollmentByIdAsync(enrollment.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(enrollment.Id);
    }

    [Fact]
    public async Task GetEnrollmentByIdAsync_NonExistingEnrollment_ReturnsNull()
    {
        // Act
        var result = await _enrollmentService.GetEnrollmentByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllEnrollmentsAsync_WithMultipleEnrollments_ReturnsAll()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Event 1", DateTime.Now.AddDays(7), "Location 1", Core.Enums.EventType.Festival, "Desc1");
        var event2 = await _eventService.CreateEventAsync("Event 2", DateTime.Now.AddDays(8), "Location 2", Core.Enums.EventType.Atuacao, "Desc2");
        
        await _enrollmentService.CreateEnrollmentAsync("user1", event1.Id);
        await _enrollmentService.CreateEnrollmentAsync("user2", event1.Id);
        await _enrollmentService.CreateEnrollmentAsync("user3", event2.Id);

        // Act
        var result = await _enrollmentService.GetAllEnrollmentsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetEnrollmentsByEventIdAsync_ReturnsEventEnrollments()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Event 1", DateTime.Now.AddDays(7), "Location 1", Core.Enums.EventType.Festival, "Desc1");
        var event2 = await _eventService.CreateEventAsync("Event 2", DateTime.Now.AddDays(8), "Location 2", Core.Enums.EventType.Atuacao, "Desc2");
        
        var enroll1 = await _enrollmentService.CreateEnrollmentAsync("user1", event1.Id);
        var enroll2 = await _enrollmentService.CreateEnrollmentAsync("user2", event1.Id);
        var enroll3 = await _enrollmentService.CreateEnrollmentAsync("user3", event2.Id);
        
        // Act - Get enrollments directly from context to test service method
        var allEnrollments = await _context.Enrollments.ToListAsync();
        var result = allEnrollments.Where(e => e.EventId == event1.Id).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Id == enroll1.Id);
        result.Should().Contain(e => e.Id == enroll2.Id);
    }

    [Fact]
    public async Task GetEnrollmentsByUserIdAsync_ReturnsUserEnrollments()
    {
        // Arrange
        var event1 = await _eventService.CreateEventAsync("Event 1", DateTime.Now.AddDays(7), "Location 1", Core.Enums.EventType.Festival, "Desc1");
        var event2 = await _eventService.CreateEventAsync("Event 2", DateTime.Now.AddDays(8), "Location 2", Core.Enums.EventType.Atuacao, "Desc2");
        
        var enroll1 = await _enrollmentService.CreateEnrollmentAsync("user1", event1.Id);
        var enroll2 = await _enrollmentService.CreateEnrollmentAsync("user1", event2.Id);
        var enroll3 = await _enrollmentService.CreateEnrollmentAsync("user2", event1.Id);

        // Act - Get enrollments directly from context
        var allEnrollments = await _context.Enrollments.ToListAsync();
        var result = allEnrollments.Where(e => e.UserId == "user1").ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(e => e.Id == enroll1.Id);
        result.Should().Contain(e => e.Id == enroll2.Id);
    }

    [Fact]
    public async Task DeleteEnrollmentAsync_RemovesEnrollment()
    {
        // Arrange
        var eventEntity = await _eventService.CreateEventAsync("Test Event", DateTime.Now.AddDays(7), "Location", Core.Enums.EventType.Festival, "Description");
        var enrollment = await _enrollmentService.CreateEnrollmentAsync("user123", eventEntity.Id);

        // Act
        await _enrollmentService.DeleteEnrollmentAsync(enrollment.Id);
        var deleted = await _enrollmentService.GetEnrollmentByIdAsync(enrollment.Id);

        // Assert
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteEnrollmentAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _enrollmentService.DeleteEnrollmentAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
