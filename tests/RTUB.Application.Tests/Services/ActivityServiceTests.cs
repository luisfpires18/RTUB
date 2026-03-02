using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for ActivityService
/// Tests business logic and service layer operations
/// </summary>
public class ActivityServiceTests
{
    private readonly Mock<IActivityRepository> _mockActivityRepository;
    private readonly ActivityService _service;
    private static readonly DateTime TestDate = new DateTime(2024, 6, 15);

    public ActivityServiceTests()
    {
        _mockActivityRepository = new Mock<IActivityRepository>();
        _service = new ActivityService(_mockActivityRepository.Object);
    }

    [Fact]
    public async Task CreateActivityAsync_WithValidData_CreatesActivity()
    {
        // Arrange
        var reportId = 1;
        var name = "Test Activity";
        var startDate = TestDate;
        var description = "Test Description";
        var expectedActivity = Activity.Create(reportId, name, startDate, description);

        _mockActivityRepository.Setup(r => r.AddAsync(It.IsAny<Activity>()))
            .ReturnsAsync(expectedActivity);

        // Act
        var result = await _service.CreateActivityAsync(reportId, name, startDate, description);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Description.Should().Be(description);
        result.ReportId.Should().Be(reportId);
        result.StartDate.Should().Be(startDate);
    }

    [Fact]
    public async Task GetActivityByIdAsync_ExistingActivity_ReturnsActivity()
    {
        // Arrange
        var activity = Activity.Create(1, "Test Activity", TestDate, "Description");
        _mockActivityRepository.Setup(r => r.GetWithTransactionsAsync(activity.Id))
            .ReturnsAsync(activity);

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
        // Arrange
        _mockActivityRepository.Setup(r => r.GetWithTransactionsAsync(999))
            .ReturnsAsync((Activity?)null);

        // Act
        var result = await _service.GetActivityByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllActivitiesAsync_ReturnsAllActivities()
    {
        // Arrange
        var activities = new List<Activity>
        {
            Activity.Create(1, "Activity 1", TestDate),
            Activity.Create(1, "Activity 2", TestDate.AddDays(1))
        };

        var mockDbSet = activities.BuildMockDbSet();
        _mockActivityRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<Activity>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockDbSet.Object)!;
            }));

        // Act
        var result = await _service.GetAllActivitiesAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetActivitiesByReportIdAsync_ReturnsActivitiesForReport()
    {
        // Arrange
        var reportId = 1;
        var allActivities = new List<Activity>
        {
            Activity.Create(reportId, "Activity 1", TestDate),
            Activity.Create(reportId, "Activity 2", TestDate.AddDays(1)),
            Activity.Create(2, "Activity 3", TestDate.AddDays(2))
        };

        var mockDbSet = allActivities.BuildMockDbSet();
        _mockActivityRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<Activity>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockDbSet.Object)!;
            }));

        // Act
        var result = await _service.GetActivitiesByReportIdAsync(reportId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(a => a.ReportId == reportId);
    }

    [Fact]
    public async Task UpdateActivityAsync_WithValidData_UpdatesActivity()
    {
        // Arrange
        var activity = Activity.Create(1, "Original Name", TestDate, "Original Description");
        var newStartDate = TestDate.AddDays(5);
        _mockActivityRepository.Setup(r => r.GetByIdAsync(activity.Id))
            .ReturnsAsync(activity);
        _mockActivityRepository.Setup(r => r.UpdateAsync(It.IsAny<Activity>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.UpdateActivityAsync(activity.Id, "Updated Name", newStartDate, "Updated Description");

        // Assert
        activity.Name.Should().Be("Updated Name");
        activity.Description.Should().Be("Updated Description");
        activity.StartDate.Should().Be(newStartDate);
    }

    [Fact]
    public async Task UpdateActivityAsync_NonExistingActivity_ThrowsException()
    {
        // Arrange
        _mockActivityRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Activity?)null);

        // Act & Assert
        var act = async () => await _service.UpdateActivityAsync(999, "Name", TestDate, "Description");
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task DeleteActivityAsync_ExistingActivity_DeletesActivity()
    {
        // Arrange
        var activity = Activity.Create(1, "Test Activity", TestDate);
        _mockActivityRepository.Setup(r => r.GetWithTransactionsAsync(activity.Id))
            .ReturnsAsync(activity);
        _mockActivityRepository.Setup(r => r.DeleteAsync(It.IsAny<Activity>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteActivityAsync(activity.Id);

        // Assert
        _mockActivityRepository.Verify(r => r.DeleteAsync(activity), Times.Once);
    }

    [Fact]
    public async Task DeleteActivityAsync_NonExistingActivity_ThrowsException()
    {
        // Arrange
        _mockActivityRepository.Setup(r => r.GetWithTransactionsAsync(999))
            .ReturnsAsync((Activity?)null);

        // Act & Assert
        var act = async () => await _service.DeleteActivityAsync(999);
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }
}
