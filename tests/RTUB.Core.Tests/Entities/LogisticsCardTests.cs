using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

public partial class LogisticsCardTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidData_ReturnsCard()
    {
        // Arrange
        var title = "Setup Stage";
        var listId = 1;
        var position = 0;

        // Act
        var card = LogisticsCard.Create(title, listId, position);

        // Assert
        card.Should().NotBeNull();
        card.Title.Should().Be(title);
        card.ListId.Should().Be(listId);
        card.Position.Should().Be(position);
        card.Description.Should().Be(string.Empty);
        card.Status.Should().Be(CardStatus.Todo);
    }

    [Fact]
    public void Create_WithDescription_ReturnsCard()
    {
        // Arrange
        var title = "Setup Stage";
        var description = "Detailed description";

        // Act
        var card = LogisticsCard.Create(title, 1, 0, description);

        // Assert
        card.Title.Should().Be(title);
        card.Description.Should().Be(description);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyTitle_ThrowsArgumentException(string? title)
    {
        // Act
        var act = () => LogisticsCard.Create(title!, 1, 0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*título*cartão*");
    }

    #endregion

    #region UpdateContent Tests

    [Fact]
    public void UpdateContent_WithValidData_UpdatesProperties()
    {
        // Arrange
        var card = LogisticsCard.Create("Old Title", 1, 0);
        var newTitle = "New Title";
        var newDescription = "New Description";

        // Act
        card.UpdateContent(newTitle, newDescription);

        // Assert
        card.Title.Should().Be(newTitle);
        card.Description.Should().Be(newDescription);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void UpdateContent_WithEmptyTitle_ThrowsArgumentException(string? title)
    {
        // Arrange
        var card = LogisticsCard.Create("Valid Title", 1, 0);

        // Act
        var act = () => card.UpdateContent(title!, "Description");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*título*cartão*");
    }

    #endregion

    #region MoveToList Tests

    [Fact]
    public void MoveToList_WithNewListIdAndPosition_UpdatesListIdAndPosition()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);

        // Act
        card.MoveToList(2, 5);

        // Assert
        card.ListId.Should().Be(2);
        card.Position.Should().Be(5);
    }

    #endregion

    #region UpdatePosition Tests

    [Fact]
    public void UpdatePosition_WithNewPosition_UpdatesPosition()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);

        // Act
        card.UpdatePosition(10);

        // Assert
        card.Position.Should().Be(10);
    }

    #endregion

    #region AssociateWithEvent Tests

    [Fact]
    public void AssociateWithEvent_WithValidEventId_SetsEventId()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);

        // Act
        card.AssociateWithEvent(42);

        // Assert
        card.EventId.Should().Be(42);
    }

    [Fact]
    public void AssociateWithEvent_WithNull_RemovesAssociation()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);
        card.AssociateWithEvent(42);

        // Act
        card.AssociateWithEvent(null);

        // Assert
        card.EventId.Should().BeNull();
    }

    #endregion

    #region AssignToUser Tests

    [Fact]
    public void AssignToUser_WithValidUserId_SetsAssignedToUserId()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);

        // Act
        card.AssignToUser("user-123");

        // Assert
        card.AssignedToUserId.Should().Be("user-123");
    }

    [Fact]
    public void AssignToUser_WithNull_RemovesAssignment()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);
        card.AssignToUser("user-123");

        // Act
        card.AssignToUser(null);

        // Assert
        card.AssignedToUserId.Should().BeNull();
    }

    #endregion
}
