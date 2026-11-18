using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public partial class ActivityTests
{
    private static readonly DateTime TestDate = new DateTime(2024, 6, 15);

    [Fact]
    public void Create_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var reportId = 1;
        var name = "Concerto de Natal";
        var startDate = TestDate;
        var description = "Evento anual de Natal";

        // Act
        var activity = Activity.Create(reportId, name, startDate, description);

        // Assert
        activity.Should().NotBeNull();
        activity.ReportId.Should().Be(reportId);
        activity.Name.Should().Be(name);
        activity.Description.Should().Be(description);
        activity.StartDate.Should().Be(startDate);
        activity.EndDate.Should().BeNull();
        activity.TotalIncome.Should().Be(0);
        activity.TotalExpenses.Should().Be(0);
        activity.Balance.Should().Be(0);
    }

    [Fact]
    public void Create_WithDateRange_ShouldCreateInstance()
    {
        // Arrange
        var reportId = 1;
        var name = "Festival de Verão";
        var startDate = TestDate;
        var endDate = TestDate.AddDays(3);
        var description = "Festival de 4 dias";

        // Act
        var activity = Activity.Create(reportId, name, startDate, description, endDate);

        // Assert
        activity.Should().NotBeNull();
        activity.StartDate.Should().Be(startDate);
        activity.EndDate.Should().Be(endDate);
    }

    [Fact]
    public void Create_WithoutDescription_ShouldCreateInstance()
    {
        // Arrange
        var reportId = 1;
        var name = "Concerto de Páscoa";
        var startDate = TestDate;

        // Act
        var activity = Activity.Create(reportId, name, startDate);

        // Assert
        activity.Should().NotBeNull();
        activity.ReportId.Should().Be(reportId);
        activity.Name.Should().Be(name);
        activity.Description.Should().BeNull();
        activity.StartDate.Should().Be(startDate);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyName_ShouldThrowException(string? emptyName)
    {
        // Arrange
        var reportId = 1;
        var startDate = TestDate;

        // Act
        var act = () => Activity.Create(reportId, emptyName!, startDate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Activity name cannot be empty*");
    }

    [Fact]
    public void Create_WithEndDateBeforeStartDate_ShouldThrowException()
    {
        // Arrange
        var reportId = 1;
        var name = "Festival";
        var startDate = TestDate;
        var endDate = TestDate.AddDays(-1); // End before start

        // Act
        var act = () => Activity.Create(reportId, name, startDate, "desc", endDate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*data de fim*");
    }

    [Fact]
    public void UpdateDetails_WithValidData_ShouldUpdateProperties()
    {
        // Arrange
        var activity = Activity.Create(1, "Old Name", TestDate, "Old Description");
        var newName = "New Name";
        var newStartDate = TestDate.AddDays(10);
        var newDescription = "New Description";

        // Act
        activity.UpdateDetails(newName, newStartDate, newDescription);

        // Assert
        activity.Name.Should().Be(newName);
        activity.Description.Should().Be(newDescription);
        activity.StartDate.Should().Be(newStartDate);
    }

    [Fact]
    public void UpdateDetails_WithDateRange_ShouldUpdateDates()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity", TestDate);
        var newStartDate = TestDate.AddDays(5);
        var newEndDate = TestDate.AddDays(8);

        // Act
        activity.UpdateDetails("Activity", newStartDate, null, newEndDate);

        // Assert
        activity.StartDate.Should().Be(newStartDate);
        activity.EndDate.Should().Be(newEndDate);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void UpdateDetails_WithEmptyName_ShouldThrowException(string? emptyName)
    {
        // Arrange
        var activity = Activity.Create(1, "Valid Name", TestDate);

        // Act
        var act = () => activity.UpdateDetails(emptyName!, TestDate, "desc");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Activity name cannot be empty*");
    }

    [Fact]
    public void UpdateDetails_WithEndDateBeforeStartDate_ShouldThrowException()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity", TestDate);
        var newStartDate = TestDate.AddDays(5);
        var newEndDate = TestDate.AddDays(3); // End before new start

        // Act
        var act = () => activity.UpdateDetails("Activity", newStartDate, null, newEndDate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*data de fim*");
    }
}
