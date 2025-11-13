using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Post entity
/// Tests domain logic and entity behavior
/// </summary>
public class PostTests
{
    [Fact]
    public void Create_WithValidData_CreatesPost()
    {
        // Arrange
        var discussionId = 1;
        var userId = "user123";
        var content = "This is a test post";

        // Act
        var result = Post.Create(discussionId, userId, content);

        // Assert
        result.Should().NotBeNull();
        result.DiscussionId.Should().Be(discussionId);
        result.UserId.Should().Be(userId);
        result.Content.Should().Be(content);
    }

    [Fact]
    public void Create_WithInvalidDiscussionId_ThrowsArgumentException()
    {
        // Arrange
        var discussionId = 0;
        var userId = "user123";
        var content = "Test content";

        // Act & Assert
        var act = () => Post.Create(discussionId, userId, content);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ID da discussão*");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var discussionId = 1;
        var userId = "";
        var content = "Test content";

        // Act & Assert
        var act = () => Post.Create(discussionId, userId, content);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ID do utilizador*");
    }

    [Fact]
    public void Create_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var discussionId = 1;
        var userId = "user123";
        var content = "";

        // Act & Assert
        var act = () => Post.Create(discussionId, userId, content);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*conteúdo*");
    }

    [Fact]
    public void Create_WithTooLongContent_ThrowsArgumentException()
    {
        // Arrange
        var discussionId = 1;
        var userId = "user123";
        var content = new string('a', 5001); // 5001 characters

        // Act & Assert
        var act = () => Post.Create(discussionId, userId, content);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*não pode exceder 5000 caracteres*");
    }

    [Fact]
    public void UpdateContent_WithValidContent_UpdatesContent()
    {
        // Arrange
        var post = Post.Create(1, "user123", "Original content");
        var newContent = "Updated content";

        // Act
        post.UpdateContent(newContent);

        // Assert
        post.Content.Should().Be(newContent);
    }

    [Fact]
    public void UpdateContent_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var post = Post.Create(1, "user123", "Original content");
        var newContent = "";

        // Act & Assert
        var act = () => post.UpdateContent(newContent);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*conteúdo*");
    }

    [Fact]
    public void UpdateContent_WithTooLongContent_ThrowsArgumentException()
    {
        // Arrange
        var post = Post.Create(1, "user123", "Original content");
        var newContent = new string('a', 5001);

        // Act & Assert
        var act = () => post.UpdateContent(newContent);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*não pode exceder 5000 caracteres*");
    }
}
