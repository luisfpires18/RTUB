using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for EventRepertoire entity
/// Tests domain logic and entity behavior
/// </summary>
public class EventRepertoireTests
{
    private readonly DateTime _testDate = new DateTime(2025, 12, 31);

    [Fact]
    public void Create_WithValidData_CreatesEventRepertoire()
    {
        // Arrange
        var eventId = 1;
        var songId = 2;
        var displayOrder = 1;

        // Act
        var result = EventRepertoire.Create(eventId, songId, displayOrder, _testDate);

        // Assert
        result.Should().NotBeNull();
        result.EventId.Should().Be(eventId);
        result.SongId.Should().Be(songId);
        result.DisplayOrder.Should().Be(displayOrder);
        result.RepertoireDate.Should().Be(_testDate.Date);
    }

    [Fact]
    public void Create_WithZeroDisplayOrder_ThrowsArgumentException()
    {
        // Arrange
        var eventId = 1;
        var songId = 2;
        var displayOrder = 0;

        // Act & Assert
        var act = () => EventRepertoire.Create(eventId, songId, displayOrder, _testDate);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Display order must be greater than 0*");
    }

    [Fact]
    public void Create_WithNegativeDisplayOrder_ThrowsArgumentException()
    {
        // Arrange
        var eventId = 1;
        var songId = 2;
        var displayOrder = -1;

        // Act & Assert
        var act = () => EventRepertoire.Create(eventId, songId, displayOrder, _testDate);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Display order must be greater than 0*");
    }

    [Fact]
    public void UpdateOrder_WithValidOrder_UpdatesDisplayOrder()
    {
        // Arrange
        var repertoire = EventRepertoire.Create(1, 2, 1, _testDate);
        var newOrder = 3;

        // Act
        repertoire.UpdateOrder(newOrder);

        // Assert
        repertoire.DisplayOrder.Should().Be(newOrder);
    }

    [Fact]
    public void UpdateOrder_WithZero_ThrowsArgumentException()
    {
        // Arrange
        var repertoire = EventRepertoire.Create(1, 2, 1, _testDate);

        // Act & Assert
        var act = () => repertoire.UpdateOrder(0);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Display order must be greater than 0*");
    }

    [Fact]
    public void UpdateOrder_WithNegativeValue_ThrowsArgumentException()
    {
        // Arrange
        var repertoire = EventRepertoire.Create(1, 2, 1, _testDate);

        // Act & Assert
        var act = () => repertoire.UpdateOrder(-1);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Display order must be greater than 0*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    [InlineData(999)]
    public void Create_WithVariousValidOrders_CreatesSuccessfully(int order)
    {
        // Act
        var result = EventRepertoire.Create(1, 2, order, _testDate);

        // Assert
        result.Should().NotBeNull();
        result.DisplayOrder.Should().Be(order);
    }

    [Fact]
    public void Create_StoresOnlyDatePartOfRepertoireDate()
    {
        // Arrange
        var dateWithTime = new DateTime(2025, 12, 31, 15, 30, 45);

        // Act
        var result = EventRepertoire.Create(1, 2, 1, dateWithTime);

        // Assert
        result.RepertoireDate.Should().Be(new DateTime(2025, 12, 31));
        result.RepertoireDate.TimeOfDay.Should().Be(TimeSpan.Zero);
    }
}
