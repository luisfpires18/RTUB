using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Discussion entity
/// Tests domain logic and entity behavior
/// </summary>
public class DiscussionTests
{
    [Fact]
    public void Create_WithValidData_CreatesDiscussion()
    {
        // Arrange
        var eventId = 1;
        var title = "Event Discussion";

        // Act
        var result = Discussion.Create(eventId, title);

        // Assert
        result.Should().NotBeNull();
        result.EventId.Should().Be(eventId);
        result.Title.Should().Be(title);
    }

    [Fact]
    public void Create_WithoutTitle_CreatesDiscussion()
    {
        // Arrange
        var eventId = 1;

        // Act
        var result = Discussion.Create(eventId);

        // Assert
        result.Should().NotBeNull();
        result.EventId.Should().Be(eventId);
        result.Title.Should().BeNull();
    }

    [Fact]
    public void Create_WithInvalidEventId_ThrowsArgumentException()
    {
        // Arrange
        var eventId = 0;

        // Act & Assert
        var act = () => Discussion.Create(eventId);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ID do evento*");
    }

    [Fact]
    public void UpdateTitle_WithValidTitle_UpdatesTitle()
    {
        // Arrange
        var discussion = Discussion.Create(1, "Original Title");
        var newTitle = "Updated Title";

        // Act
        discussion.UpdateTitle(newTitle);

        // Assert
        discussion.Title.Should().Be(newTitle);
    }

    [Fact]
    public void UpdateTitle_WithTooLongTitle_ThrowsArgumentException()
    {
        // Arrange
        var discussion = Discussion.Create(1);
        var longTitle = new string('a', 201); // 201 characters

        // Act & Assert
        var act = () => discussion.UpdateTitle(longTitle);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*não pode exceder 200 caracteres*");
    }
}
