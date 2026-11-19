using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public partial class MessageTests
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
}
