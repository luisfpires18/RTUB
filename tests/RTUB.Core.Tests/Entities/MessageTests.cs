using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class MessageTests
{
    [Fact]
    public void Constructor_WithDefaults_ShouldCreateInstance()
    {
        // Act
        var message = new Message();

        // Assert
        message.Should().NotBeNull();
        message.Body.Should().Be(string.Empty);
        message.ReadBy.Should().Be(string.Empty);
        message.IsSystem.Should().BeFalse();
    }

    [Fact]
    public void Properties_WhenSet_ShouldReturnCorrectValues()
    {
        // Arrange
        var message = new Message
        {
            Id = 1,
            ConversationId = 42,
            SenderId = "sender-123",
            Body = "Hello, World!",
            IsSystem = false,
            ReadBy = "user1;user2",
            Link = "https://example.com"
        };

        // Assert
        message.Id.Should().Be(1);
        message.ConversationId.Should().Be(42);
        message.SenderId.Should().Be("sender-123");
        message.Body.Should().Be("Hello, World!");
        message.IsSystem.Should().BeFalse();
        message.ReadBy.Should().Be("user1;user2");
        message.Link.Should().Be("https://example.com");
    }

    #region GetReadByIds Tests

    [Fact]
    public void GetReadByIds_WithEmptyReadBy_ShouldReturnEmptyList()
    {
        // Arrange
        var message = new Message { ReadBy = "" };

        // Act
        var result = message.GetReadByIds();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetReadByIds_WithSingleUser_ShouldReturnSingleItem()
    {
        // Arrange
        var message = new Message { ReadBy = "user1" };

        // Act
        var result = message.GetReadByIds();

        // Assert
        result.Should().ContainSingle().Which.Should().Be("user1");
    }

    [Fact]
    public void GetReadByIds_WithMultipleUsers_ShouldReturnAllUsers()
    {
        // Arrange
        var message = new Message { ReadBy = "user1;user2;user3" };

        // Act
        var result = message.GetReadByIds();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(new[] { "user1", "user2", "user3" });
    }

    [Fact]
    public void GetReadByIds_WithTrailingSemicolon_ShouldHandleCorrectly()
    {
        // Arrange
        var message = new Message { ReadBy = "user1;user2;" };

        // Act
        var result = message.GetReadByIds();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(new[] { "user1", "user2" });
    }

    #endregion

    #region IsReadBy Tests

    [Fact]
    public void IsReadBy_WhenUserHasRead_ShouldReturnTrue()
    {
        // Arrange
        var message = new Message { ReadBy = "user1;user2;user3" };

        // Act
        var result = message.IsReadBy("user2");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsReadBy_WhenUserHasNotRead_ShouldReturnFalse()
    {
        // Arrange
        var message = new Message { ReadBy = "user1;user2" };

        // Act
        var result = message.IsReadBy("user99");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsReadBy_WithEmptyReadBy_ShouldReturnFalse()
    {
        // Arrange
        var message = new Message { ReadBy = "" };

        // Act
        var result = message.IsReadBy("user1");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region MarkAsReadBy Tests

    [Fact]
    public void MarkAsReadBy_WhenUserHasNotRead_ShouldAddUser()
    {
        // Arrange
        var message = new Message { ReadBy = "user1" };

        // Act
        message.MarkAsReadBy("user2");

        // Assert
        message.ReadBy.Should().Be("user1;user2");
    }

    [Fact]
    public void MarkAsReadBy_WhenUserAlreadyRead_ShouldNotDuplicate()
    {
        // Arrange
        var message = new Message { ReadBy = "user1;user2" };

        // Act
        message.MarkAsReadBy("user1");

        // Assert
        message.ReadBy.Should().Be("user1;user2");
    }

    [Fact]
    public void MarkAsReadBy_WhenReadByIsEmpty_ShouldAddUser()
    {
        // Arrange
        var message = new Message { ReadBy = "" };

        // Act
        message.MarkAsReadBy("user1");

        // Assert
        message.ReadBy.Should().Be("user1");
    }

    [Fact]
    public void MarkAsReadBy_MultipleUsersSequentially_ShouldAddAll()
    {
        // Arrange
        var message = new Message { ReadBy = "" };

        // Act
        message.MarkAsReadBy("user1");
        message.MarkAsReadBy("user2");
        message.MarkAsReadBy("user3");

        // Assert
        message.ReadBy.Should().Be("user1;user2;user3");
    }

    #endregion

    #region MarkAsUnreadBy Tests

    [Fact]
    public void MarkAsUnreadBy_WhenUserHasRead_ShouldRemoveUser()
    {
        // Arrange
        var message = new Message { ReadBy = "user1;user2;user3" };

        // Act
        message.MarkAsUnreadBy("user2");

        // Assert
        message.ReadBy.Should().Be("user1;user3");
    }

    [Fact]
    public void MarkAsUnreadBy_WhenUserHasNotRead_ShouldNotChange()
    {
        // Arrange
        var message = new Message { ReadBy = "user1;user2" };

        // Act
        message.MarkAsUnreadBy("user99");

        // Assert
        message.ReadBy.Should().Be("user1;user2");
    }

    [Fact]
    public void MarkAsUnreadBy_WhenOnlyUser_ShouldResultInEmptyString()
    {
        // Arrange
        var message = new Message { ReadBy = "user1" };

        // Act
        message.MarkAsUnreadBy("user1");

        // Assert
        message.ReadBy.Should().Be("");
    }

    [Fact]
    public void MarkAsUnreadBy_WhenReadByIsEmpty_ShouldNotChange()
    {
        // Arrange
        var message = new Message { ReadBy = "" };

        // Act
        message.MarkAsUnreadBy("user1");

        // Assert
        message.ReadBy.Should().Be("");
    }

    #endregion

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
