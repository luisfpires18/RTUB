using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Rehearsal entity
/// Tests domain logic and entity behavior
/// </summary>
public class RehearsalTests
{
    [Fact]
    public void Create_WithValidData_CreatesRehearsal()
    {
        // Arrange
        var date = DateTime.Now.AddDays(7);
        var location = "Centro Académico";
        var theme = "Fado practice";

        // Act
        var result = Rehearsal.Create(date, location, theme);

        // Assert
        result.Should().NotBeNull();
        result.Date.Should().Be(date.Date);
        result.Location.Should().Be(location);
        result.Theme.Should().Be(theme);
        result.IsCanceled.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyLocation_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.Now.AddDays(7);
        var location = "";

        // Act & Assert
        var act = () => Rehearsal.Create(date, location);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Location*");
    }

    [Fact]
    public void Create_WithNullLocation_ThrowsArgumentException()
    {
        // Arrange
        var date = DateTime.Now.AddDays(7);
        string? location = null;

        // Act & Assert
        var act = () => Rehearsal.Create(date, location!);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Location*");
    }

    [Fact]
    public void Create_NormalizesDateToDateOnly()
    {
        // Arrange
        var dateTime = new DateTime(2025, 10, 31, 14, 30, 45);
        var location = "Test Location";

        // Act
        var result = Rehearsal.Create(dateTime, location);

        // Assert
        result.Date.Should().Be(dateTime.Date);
        result.Date.TimeOfDay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Create_WithoutTheme_SetsThemeToNull()
    {
        // Arrange
        var date = DateTime.Now.AddDays(7);
        var location = "Test Location";

        // Act
        var result = Rehearsal.Create(date, location);

        // Assert
        result.Theme.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesRehearsal()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Original Location");
        var newLocation = "New Location";
        var newTheme = "New Theme";
        var newNotes = "New Notes";

        // Act
        rehearsal.UpdateDetails(newLocation, newTheme, newNotes);

        // Assert
        rehearsal.Location.Should().Be(newLocation);
        rehearsal.Theme.Should().Be(newTheme);
        rehearsal.Notes.Should().Be(newNotes);
    }

    [Fact]
    public void UpdateDetails_WithEmptyLocation_ThrowsArgumentException()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Original Location");

        // Act & Assert
        var act = () => rehearsal.UpdateDetails("", "Theme", "Notes");
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Location*");
    }

    [Fact]
    public void Cancel_SetsCanceledToTrue()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");

        // Act
        rehearsal.Cancel();

        // Assert
        rehearsal.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public void Reactivate_SetsCanceledToFalse()
    {
        // Arrange
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");
        rehearsal.Cancel();

        // Act
        rehearsal.Reactivate();

        // Assert
        rehearsal.IsCanceled.Should().BeFalse();
    }

    [Fact]
    public void DefaultStartTime_Is21Hours()
    {
        // Arrange & Act
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");

        // Assert
        rehearsal.StartTime.Should().Be(new TimeSpan(21, 0, 0));
    }

    [Fact]
    public void DefaultEndTime_IsMidnight()
    {
        // Arrange & Act
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");

        // Assert
        rehearsal.EndTime.Should().Be(new TimeSpan(0, 0, 0));
    }

    [Fact]
    public void Attendances_InitializedAsEmptyCollection()
    {
        // Arrange & Act
        var rehearsal = Rehearsal.Create(DateTime.Now.AddDays(7), "Test Location");

        // Assert
        rehearsal.Attendances.Should().NotBeNull();
        rehearsal.Attendances.Should().BeEmpty();
    }
}
