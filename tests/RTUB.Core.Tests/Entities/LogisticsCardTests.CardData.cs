// LogisticsCard status, labels, dates, checklist and attachments
using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Entities;

public partial class LogisticsCardTests
{
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
