using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Event entity
/// Tests domain logic and entity behavior
/// </summary>
public class EventTests
{
    [Fact]
    public void Create_WithValidData_CreatesEvent()
    {
        // Arrange
        var name = "Test Event";
        var date = DateTime.Now.AddDays(7);
        var location = "Test Location";
        var type = EventType.Festival;

        // Act
        var result = Event.Create(name, date, location, type);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(name);
        result.Date.Should().Be(date);
        result.Location.Should().Be(location);
        result.Type.Should().Be(type);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var name = "";
        var date = DateTime.Now.AddDays(7);
        var location = "Test Location";
        var type = EventType.Festival;

        // Act & Assert
        var act = () => Event.Create(name, date, location, type);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome do evento*");
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesEvent()
    {
        // Arrange
        var event1 = Event.Create("Original Name", DateTime.Now, "Original Location", EventType.Festival);
        var newName = "Updated Name";
        var newDate = DateTime.Now.AddDays(14);
        var newLocation = "Updated Location";
        var newDescription = "Updated Description";

        // Act
        event1.UpdateDetails(newName, newDate, newLocation, newDescription);

        // Assert
        event1.Name.Should().Be(newName);
        event1.Date.Should().Be(newDate);
        event1.Location.Should().Be(newLocation);
        event1.Description.Should().Be(newDescription);
    }
}
