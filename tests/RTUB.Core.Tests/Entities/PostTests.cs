using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Post entity business logic
/// </summary>
public class PostTests
{
    [Fact]
    public void Create_WithValidData_ReturnsPost()
    {
        // Arrange
        var discussionId = 1;
        var authorId = "user1";
        var title = "Test Post";
        var body = "This is a test post body";

        // Act
        var post = Post.Create(discussionId, authorId, title, body);

        // Assert
        post.Should().NotBeNull();
        post.DiscussionId.Should().Be(discussionId);
        post.AuthorId.Should().Be(authorId);
        post.Title.Should().Be(title);
        post.Body.Should().Be(body);
        post.IsEdited.Should().BeFalse();
        post.IsPinned.Should().BeFalse();
        post.IsLocked.Should().BeFalse();
        post.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidTitle_ThrowsException(string? invalidTitle)
    {
        // Arrange
        var discussionId = 1;
        var authorId = "user1";
        var body = "This is a test post body";

        // Act & Assert
        var act = () => Post.Create(discussionId, authorId, invalidTitle!, body);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithTitleTooShort_ThrowsException()
    {
        // Arrange
        var discussionId = 1;
        var authorId = "user1";
        var title = "AB"; // Only 2 characters
        var body = "This is a test post body";

        // Act & Assert
        var act = () => Post.Create(discussionId, authorId, title, body);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithTitleTooLong_ThrowsException()
    {
        // Arrange
        var discussionId = 1;
        var authorId = "user1";
        var title = new string('A', 121); // 121 characters
        var body = "This is a test post body";

        // Act & Assert
        var act = () => Post.Create(discussionId, authorId, title, body);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Edit_WithValidData_UpdatesPost()
    {
        // Arrange
        var post = Post.Create(1, "user1", "Original Title", "Original Body");
        var newTitle = "Updated Title";
        var newBody = "Updated Body";

        // Act
        post.Edit(newTitle, newBody);

        // Assert
        post.Title.Should().Be(newTitle);
        post.Body.Should().Be(newBody);
        post.IsEdited.Should().BeTrue();
        post.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Pin_SetsIsPinnedToTrue()
    {
        // Arrange
        var post = Post.Create(1, "user1", "Test Post", "Test Body");

        // Act
        post.Pin();

        // Assert
        post.IsPinned.Should().BeTrue();
        post.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Unpin_SetsIsPinnedToFalse()
    {
        // Arrange
        var post = Post.Create(1, "user1", "Test Post", "Test Body");
        post.Pin();

        // Act
        post.Unpin();

        // Assert
        post.IsPinned.Should().BeFalse();
        post.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Lock_SetsIsLockedToTrue()
    {
        // Arrange
        var post = Post.Create(1, "user1", "Test Post", "Test Body");

        // Act
        post.Lock();

        // Assert
        post.IsLocked.Should().BeTrue();
        post.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Unlock_SetsIsLockedToFalse()
    {
        // Arrange
        var post = Post.Create(1, "user1", "Test Post", "Test Body");
        post.Lock();

        // Act
        post.Unlock();

        // Assert
        post.IsLocked.Should().BeFalse();
        post.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedToTrue()
    {
        // Arrange
        var post = Post.Create(1, "user1", "Test Post", "Test Body");

        // Act
        post.SoftDelete();

        // Assert
        post.IsDeleted.Should().BeTrue();
        post.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateLastActivity_UpdatesLastActivityAt()
    {
        // Arrange
        var post = Post.Create(1, "user1", "Test Post", "Test Body");
        var originalLastActivity = post.LastActivityAt;
        Thread.Sleep(10); // Small delay to ensure time difference

        // Act
        post.UpdateLastActivity();

        // Assert
        post.LastActivityAt.Should().BeAfter(originalLastActivity);
    }

    [Fact]
    public void SetMentions_SetsMentionsJson()
    {
        // Arrange
        var post = Post.Create(1, "user1", "Test Post", "Test Body");
        var mentionsJson = "{\"john\":\"user1\"}";

        // Act
        post.SetMentions(mentionsJson);

        // Assert
        post.MentionsJson.Should().Be(mentionsJson);
    }
}
