using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class LogisticsListTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var name = "To Do";
        var boardId = 1;
        var position = 0;

        // Act
        var list = LogisticsList.Create(name, boardId, position);

        // Assert
        list.Should().NotBeNull();
        list.Name.Should().Be(name);
        list.BoardId.Should().Be(boardId);
        list.Position.Should().Be(position);
    }

    [Theory]
    [InlineData("To Do", 1, 0)]
    [InlineData("In Progress", 2, 1)]
    [InlineData("Done", 3, 2)]
    public void Create_WithDifferentValues_ShouldCreateInstance(string name, int boardId, int position)
    {
        // Act
        var list = LogisticsList.Create(name, boardId, position);

        // Assert
        list.Should().NotBeNull();
        list.Name.Should().Be(name);
        list.BoardId.Should().Be(boardId);
        list.Position.Should().Be(position);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyName_ShouldThrowException(string? name)
    {
        // Act
        var act = () => LogisticsList.Create(name!, 1, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome*lista*");
    }

    [Fact]
    public void Create_ShouldInitializeEmptyCardsCollection()
    {
        // Act
        var list = LogisticsList.Create("List", 1, 0);

        // Assert
        list.Cards.Should().NotBeNull();
        list.Cards.Should().BeEmpty();
    }

    #endregion

    #region UpdateName Tests

    [Fact]
    public void UpdateName_WithValidName_ShouldUpdateName()
    {
        // Arrange
        var list = LogisticsList.Create("Old Name", 1, 0);
        var newName = "New Name";

        // Act
        list.UpdateName(newName);

        // Assert
        list.Name.Should().Be(newName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void UpdateName_WithEmptyName_ShouldThrowException(string? name)
    {
        // Arrange
        var list = LogisticsList.Create("Valid Name", 1, 0);

        // Act
        var act = () => list.UpdateName(name!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*nome*lista*");
    }

    #endregion

    #region UpdatePosition Tests

    [Fact]
    public void UpdatePosition_WithValidPosition_ShouldUpdatePosition()
    {
        // Arrange
        var list = LogisticsList.Create("List", 1, 0);

        // Act
        list.UpdatePosition(5);

        // Assert
        list.Position.Should().Be(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void UpdatePosition_WithDifferentPositions_ShouldUpdate(int newPosition)
    {
        // Arrange
        var list = LogisticsList.Create("List", 1, 0);

        // Act
        list.UpdatePosition(newPosition);

        // Assert
        list.Position.Should().Be(newPosition);
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void BaseEntityProperties_ShouldBeAccessible()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var list = LogisticsList.Create("List", 1, 0);

        // Act
        list.CreatedAt = now;
        list.CreatedBy = "creator";
        list.UpdatedAt = now.AddHours(1);
        list.UpdatedBy = "updater";

        // Assert
        list.CreatedAt.Should().Be(now);
        list.CreatedBy.Should().Be("creator");
        list.UpdatedAt.Should().Be(now.AddHours(1));
        list.UpdatedBy.Should().Be("updater");
    }

    #endregion
}
