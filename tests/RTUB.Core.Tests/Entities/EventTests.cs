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
    
    [Fact]
    public void Cancel_WithValidReason_CancelsEvent()
    {
        // Arrange
        var event1 = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        var reason = "Mau tempo previsto";

        // Act
        event1.Cancel(reason);

        // Assert
        event1.IsCancelled.Should().BeTrue();
        event1.CancellationReason.Should().Be(reason);
    }
    
    [Fact]
    public void Cancel_WithEmptyReason_ThrowsArgumentException()
    {
        // Arrange
        var event1 = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        var reason = "";

        // Act & Assert
        var act = () => event1.Cancel(reason);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*motivo de cancelamento*");
    }
    
    [Fact]
    public void Uncancel_WithCancelledEvent_UncancelsEvent()
    {
        // Arrange
        var event1 = Event.Create("Test Event", DateTime.Now.AddDays(7), "Test Location", EventType.Atuacao);
        event1.Cancel("Mau tempo previsto");

        // Act
        event1.Uncancel();

        // Assert
        event1.IsCancelled.Should().BeFalse();
        event1.CancellationReason.Should().BeNull();
    }
}
