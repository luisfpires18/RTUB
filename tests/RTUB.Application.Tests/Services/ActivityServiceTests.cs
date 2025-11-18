using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for ActivityService
/// Tests business logic and service layer operations
/// </summary>
public class ActivityServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ActivityService _service;

    public ActivityServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options, Mock.Of<IHttpContextAccessor>(), new AuditContext());
        _service = new ActivityService(_context);
    }

    [Fact]
    public async Task CreateActivityAsync_WithValidData_CreatesActivity()
    {
        // Arrange
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var name = "Test Activity";
        var description = "Test Description";

        // Act
        var result = await _service.CreateActivityAsync(report.Id, name, description);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Description.Should().Be(description);
        result.ReportId.Should().Be(report.Id);
    }

    [Fact]
    public async Task GetActivityByIdAsync_ExistingActivity_ReturnsActivity()
    {
        // Arrange
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var activity = Activity.Create(report.Id, "Test Activity", "Description");
        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetActivityByIdAsync(activity.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(activity.Id);
        result.Name.Should().Be("Test Activity");
    }

    [Fact]
    public async Task GetActivityByIdAsync_NonExistingActivity_ReturnsNull()
    {
        // Act
        var result = await _service.GetActivityByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllActivitiesAsync_ReturnsAllActivities()
    {
        // Arrange
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var activity1 = Activity.Create(report.Id, "Activity 1");
        var activity2 = Activity.Create(report.Id, "Activity 2");
        _context.Activities.AddRange(activity1, activity2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllActivitiesAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetActivitiesByReportIdAsync_ReturnsActivitiesForReport()
    {
        // Arrange
        var report1 = Report.Create("Report 1", 2024);
        var report2 = Report.Create("Report 2", 2023);
        _context.Reports.AddRange(report1, report2);
        await _context.SaveChangesAsync();

        var activity1 = Activity.Create(report1.Id, "Activity 1");
        var activity2 = Activity.Create(report1.Id, "Activity 2");
        var activity3 = Activity.Create(report2.Id, "Activity 3");
        _context.Activities.AddRange(activity1, activity2, activity3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetActivitiesByReportIdAsync(report1.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.ReportId == report1.Id);
    }

    [Fact]
    public async Task UpdateActivityAsync_WithValidData_UpdatesActivity()
    {
        // Arrange
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var activity = Activity.Create(report.Id, "Original Name", "Original Description");
        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // Act
        await _service.UpdateActivityAsync(activity.Id, "Updated Name", "Updated Description");

        // Assert
        var updated = await _context.Activities.FindAsync(activity.Id);
        updated!.Name.Should().Be("Updated Name");
        updated.Description.Should().Be("Updated Description");
    }

    [Fact]
    public async Task UpdateActivityAsync_NonExistingActivity_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.UpdateActivityAsync(999, "Name", "Description");
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task DeleteActivityAsync_ExistingActivity_DeletesActivity()
    {
        // Arrange
        var report = Report.Create("Test Report", 2024);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        var activity = Activity.Create(report.Id, "Test Activity");
        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteActivityAsync(activity.Id);

        // Assert
        var deleted = await _context.Activities.FindAsync(activity.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteActivityAsync_NonExistingActivity_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _service.DeleteActivityAsync(999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
