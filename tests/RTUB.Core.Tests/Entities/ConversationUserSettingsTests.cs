using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class ConversationUserSettingsTests
{
    [Fact]
    public void Constructor_WithDefaults_ShouldCreateInstance()
    {
        // Act
        var settings = new ConversationUserSettings();

        // Assert
        settings.Should().NotBeNull();
        settings.UserId.Should().Be(string.Empty);
        settings.IsMuted.Should().BeFalse();
        settings.IsPinned.Should().BeFalse();
    }

    [Fact]
    public void Properties_WhenSet_ShouldReturnCorrectValues()
    {
        // Arrange
        var settings = new ConversationUserSettings
        {
            Id = 1,
            ConversationId = 42,
            UserId = "user-id-123",
            IsMuted = true,
            IsPinned = true
        };

        // Assert
        settings.Id.Should().Be(1);
        settings.ConversationId.Should().Be(42);
        settings.UserId.Should().Be("user-id-123");
        settings.IsMuted.Should().BeTrue();
        settings.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void IsMuted_WhenSetToTrue_ShouldBeTrue()
    {
        // Arrange
        var settings = new ConversationUserSettings();

        // Act
        settings.IsMuted = true;

        // Assert
        settings.IsMuted.Should().BeTrue();
    }

    [Fact]
    public void IsPinned_WhenSetToTrue_ShouldBeTrue()
    {
        // Arrange
        var settings = new ConversationUserSettings();

        // Act
        settings.IsPinned = true;

        // Assert
        settings.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void NavigationProperties_WhenNotSet_ShouldBeNull()
    {
        // Arrange
        var settings = new ConversationUserSettings();

        // Assert
        settings.Conversation.Should().BeNull();
        settings.User.Should().BeNull();
    }

    [Fact]
    public void BaseEntityProperties_ShouldBeAccessible()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var settings = new ConversationUserSettings
        {
            CreatedAt = now,
            CreatedBy = "creator",
            UpdatedAt = now.AddHours(1),
            UpdatedBy = "updater"
        };

        // Assert
        settings.CreatedAt.Should().Be(now);
        settings.CreatedBy.Should().Be("creator");
        settings.UpdatedAt.Should().Be(now.AddHours(1));
        settings.UpdatedBy.Should().Be("updater");
    }
}
