// Message system messages and entity base properties
using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public partial class MessageTests
{
    #region System Message Tests

    [Fact]
    public void SystemMessage_ShouldAllowNullSenderId()
    {
        // Arrange
        var message = new Message
        {
            SenderId = null,
            IsSystem = true,
            Body = "System notification"
        };

        // Assert
        message.SenderId.Should().BeNull();
        message.IsSystem.Should().BeTrue();
    }

    [Fact]
    public void SystemMessage_ShouldAllowLink()
    {
        // Arrange
        var message = new Message
        {
            IsSystem = true,
            Body = "Check out this link",
            Link = "https://example.com/event/123"
        };

        // Assert
        message.Link.Should().Be("https://example.com/event/123");
    }

    #endregion

    #region Navigation Properties Tests

    [Fact]
    public void NavigationProperties_WhenNotSet_ShouldBeNull()
    {
        // Arrange
        var message = new Message();

        // Assert
        message.Conversation.Should().BeNull();
        message.Sender.Should().BeNull();
    }

    #endregion

    #region BaseEntity Properties Tests

    [Fact]
    public void BaseEntityProperties_ShouldBeAccessible()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var message = new Message
        {
            CreatedAt = now,
            CreatedBy = "creator",
            UpdatedAt = now.AddHours(1),
            UpdatedBy = "updater"
        };

        // Assert
        message.CreatedAt.Should().Be(now);
        message.CreatedBy.Should().Be("creator");
        message.UpdatedAt.Should().Be(now.AddHours(1));
        message.UpdatedBy.Should().Be("updater");
    }

    #endregion
}
