using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class LogisticsBoardTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidName_ShouldCreateInstance()
    {
        // Arrange
        var name = "Event Planning Board";

        // Act
        var board = LogisticsBoard.Create(name);

        // Assert
        board.Should().NotBeNull();
        board.Name.Should().Be(name);
        board.Description.Should().Be(string.Empty);
        board.EventId.Should().BeNull();
    }

    [Fact]
    public void Create_WithNameAndDescription_ShouldCreateInstance()
    {
        // Arrange
        var name = "Event Planning Board";
        var description = "Board for planning the annual concert";

        // Act
        var board = LogisticsBoard.Create(name, description);

        // Assert
        board.Should().NotBeNull();
        board.Name.Should().Be(name);
        board.Description.Should().Be(description);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyName_ShouldThrowException(string? name)
    {
        // Act
        var act = () => LogisticsBoard.Create(name!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome*quadro*");
    }

    [Fact]
    public void Create_ShouldInitializeEmptyListsCollection()
    {
        // Act
        var board = LogisticsBoard.Create("Test Board");

        // Assert
        board.Lists.Should().NotBeNull();
        board.Lists.Should().BeEmpty();
    }

    #endregion

    #region UpdateDetails Tests

    [Fact]
    public void UpdateDetails_WithValidData_ShouldUpdateProperties()
    {
        // Arrange
        var board = LogisticsBoard.Create("Old Name", "Old Description");
        var newName = "New Name";
        var newDescription = "New Description";

        // Act
        board.UpdateDetails(newName, newDescription);

        // Assert
        board.Name.Should().Be(newName);
        board.Description.Should().Be(newDescription);
    }

    [Fact]
    public void UpdateDetails_WithEmptyDescription_ShouldUpdateProperties()
    {
        // Arrange
        var board = LogisticsBoard.Create("Name", "Description");

        // Act
        board.UpdateDetails("Updated Name", "");

        // Assert
        board.Name.Should().Be("Updated Name");
        board.Description.Should().Be("");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void UpdateDetails_WithEmptyName_ShouldThrowException(string? name)
    {
        // Arrange
        var board = LogisticsBoard.Create("Valid Name");

        // Act
        var act = () => board.UpdateDetails(name!, "Description");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome*quadro*");
    }

    #endregion

    #region AssociateWithEvent Tests

    [Fact]
    public void AssociateWithEvent_WithValidEventId_ShouldSetEventId()
    {
        // Arrange
        var board = LogisticsBoard.Create("Board");
        var eventId = 42;

        // Act
        board.AssociateWithEvent(eventId);

        // Assert
        board.EventId.Should().Be(eventId);
    }

    [Fact]
    public void AssociateWithEvent_WithNull_ShouldRemoveAssociation()
    {
        // Arrange
        var board = LogisticsBoard.Create("Board");
        board.AssociateWithEvent(42);

        // Act
        board.AssociateWithEvent(null);

        // Assert
        board.EventId.Should().BeNull();
    }

    [Fact]
    public void AssociateWithEvent_CalledMultipleTimes_ShouldUpdateEventId()
    {
        // Arrange
        var board = LogisticsBoard.Create("Board");

        // Act
        board.AssociateWithEvent(1);
        board.AssociateWithEvent(2);
        board.AssociateWithEvent(3);

        // Assert
        board.EventId.Should().Be(3);
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void BaseEntityProperties_ShouldBeAccessible()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var board = LogisticsBoard.Create("Board");
        board.CreatedAt = now;
        board.CreatedBy = "creator";
        board.UpdatedAt = now.AddHours(1);
        board.UpdatedBy = "updater";

        // Assert
        board.CreatedAt.Should().Be(now);
        board.CreatedBy.Should().Be("creator");
        board.UpdatedAt.Should().Be(now.AddHours(1));
        board.UpdatedBy.Should().Be("updater");
    }

    [Fact]
    public void Event_NavigationProperty_WhenNotSet_ShouldBeNull()
    {
        // Arrange
        var board = LogisticsBoard.Create("Board");

        // Assert
        board.Event.Should().BeNull();
    }

    #endregion
}
