using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class DiscussionTests
{
    [Fact]
    public void Create_WithValidEventId_ShouldCreateInstance()
    {
        // Arrange
        var eventId = 1;

        // Act
        var discussion = Discussion.Create(eventId);

        // Assert
        discussion.Should().NotBeNull();
        discussion.EventId.Should().Be(eventId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Create_WithPositiveEventId_ShouldCreateInstance(int eventId)
    {
        // Act
        var discussion = Discussion.Create(eventId);

        // Assert
        discussion.Should().NotBeNull();
        discussion.EventId.Should().Be(eventId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithNonPositiveEventId_ShouldThrowException(int invalidEventId)
    {
        // Act
        var act = () => Discussion.Create(invalidEventId);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Event ID must be positive*");
    }

    [Fact]
    public void Create_ShouldInitializeEmptyPostsCollection()
    {
        // Arrange
        var eventId = 1;

        // Act
        var discussion = Discussion.Create(eventId);

        // Assert
        discussion.Posts.Should().NotBeNull();
        discussion.Posts.Should().BeEmpty();
    }

    [Fact]
    public void BaseEntityProperties_ShouldBeAccessible()
    {
        // Arrange
        var discussion = Discussion.Create(1);
        var now = DateTime.UtcNow;

        // Act
        discussion.CreatedAt = now;
        discussion.CreatedBy = "creator";
        discussion.UpdatedAt = now.AddHours(1);
        discussion.UpdatedBy = "updater";

        // Assert
        discussion.CreatedAt.Should().Be(now);
        discussion.CreatedBy.Should().Be("creator");
        discussion.UpdatedAt.Should().Be(now.AddHours(1));
        discussion.UpdatedBy.Should().Be("updater");
    }
}
