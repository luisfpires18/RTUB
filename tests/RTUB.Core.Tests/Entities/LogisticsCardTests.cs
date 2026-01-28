using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

public class LogisticsCardTests
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

    #region SetStatus Tests

    [Theory]
    [InlineData(CardStatus.Todo)]
    [InlineData(CardStatus.InProgress)]
    [InlineData(CardStatus.Done)]
    public void SetStatus_WithValidStatus_UpdatesStatus(CardStatus status)
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);

        // Act
        card.SetStatus(status);

        // Assert
        card.Status.Should().Be(status);
    }

    #endregion

    #region SetLabels Tests

    [Fact]
    public void SetLabels_WithValidLabels_SetsLabels()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);

        // Act
        card.SetLabels("urgent,important");

        // Assert
        card.Labels.Should().Be("urgent,important");
    }

    [Fact]
    public void SetLabels_WithNull_ClearsLabels()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);
        card.SetLabels("urgent");

        // Act
        card.SetLabels(null);

        // Assert
        card.Labels.Should().BeNull();
    }

    #endregion

    #region SetDates Tests

    [Fact]
    public void SetDates_WithValidDates_SetsDates()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);
        var startDate = DateTime.UtcNow;
        var dueDate = DateTime.UtcNow.AddDays(7);
        var reminderDate = DateTime.UtcNow.AddDays(5);

        // Act
        card.SetDates(startDate, dueDate, reminderDate);

        // Assert
        card.StartDate.Should().Be(startDate);
        card.DueDate.Should().Be(dueDate);
        card.ReminderDate.Should().Be(reminderDate);
    }

    [Fact]
    public void SetDates_WithNullValues_ClearsDates()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);
        card.SetDates(DateTime.UtcNow, DateTime.UtcNow.AddDays(7), DateTime.UtcNow.AddDays(5));

        // Act
        card.SetDates(null, null, null);

        // Assert
        card.StartDate.Should().BeNull();
        card.DueDate.Should().BeNull();
        card.ReminderDate.Should().BeNull();
    }

    #endregion

    #region SetChecklist Tests

    [Fact]
    public void SetChecklist_WithValidJson_SetsChecklist()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);
        var checklistJson = "[{\"item\":\"Task 1\",\"done\":false}]";

        // Act
        card.SetChecklist(checklistJson);

        // Assert
        card.ChecklistJson.Should().Be(checklistJson);
    }

    [Fact]
    public void SetChecklist_WithNull_ClearsChecklist()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);
        card.SetChecklist("[{\"item\":\"Task 1\"}]");

        // Act
        card.SetChecklist(null);

        // Assert
        card.ChecklistJson.Should().BeNull();
    }

    #endregion

    #region SetAttachments Tests

    [Fact]
    public void SetAttachments_WithValidJson_ShouldSetAttachments()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);
        var attachmentsJson = "[{\"url\":\"https://example.com\"}]";

        // Act
        card.SetAttachments(attachmentsJson);

        // Assert
        card.AttachmentsJson.Should().Be(attachmentsJson);
    }

    [Fact]
    public void SetAttachments_WithNull_ShouldClearAttachments()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);
        card.SetAttachments("[{\"url\":\"https://example.com\"}]");

        // Act
        card.SetAttachments(null);

        // Assert
        card.AttachmentsJson.Should().BeNull();
    }

    #endregion

    #region Navigation Properties Tests

    [Fact]
    public void NavigationProperties_WhenNotSet_AreNull()
    {
        // Arrange
        var card = LogisticsCard.Create("Card", 1, 0);

        // Assert
        card.Event.Should().BeNull();
        card.AssignedToUser.Should().BeNull();
    }

    #endregion
}
