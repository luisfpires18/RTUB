using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Comment entity
/// Tests domain logic and entity behavior
/// </summary>
public class CommentTests
{
    [Fact]
    public void Create_WithValidData_CreatesComment()
    {
        // Arrange
        var postId = 1;
        var userId = "user123";
        var content = "This is a test comment";

        // Act
        var result = Comment.Create(postId, userId, content);

        // Assert
        result.Should().NotBeNull();
        result.PostId.Should().Be(postId);
        result.UserId.Should().Be(userId);
        result.Content.Should().Be(content);
    }

    [Fact]
    public void Create_WithInvalidPostId_ThrowsArgumentException()
    {
        // Arrange
        var postId = 0;
        var userId = "user123";
        var content = "Test content";

        // Act & Assert
        var act = () => Comment.Create(postId, userId, content);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ID do post*");
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var postId = 1;
        var userId = "";
        var content = "Test content";

        // Act & Assert
        var act = () => Comment.Create(postId, userId, content);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ID do utilizador*");
    }

    [Fact]
    public void Create_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var postId = 1;
        var userId = "user123";
        var content = "";

        // Act & Assert
        var act = () => Comment.Create(postId, userId, content);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*conteúdo*");
    }

    [Fact]
    public void Create_WithTooLongContent_ThrowsArgumentException()
    {
        // Arrange
        var postId = 1;
        var userId = "user123";
        var content = new string('a', 2001); // 2001 characters

        // Act & Assert
        var act = () => Comment.Create(postId, userId, content);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*não pode exceder 2000 caracteres*");
    }

    [Fact]
    public void UpdateContent_WithValidContent_UpdatesContent()
    {
        // Arrange
        var comment = Comment.Create(1, "user123", "Original content");
        var newContent = "Updated content";

        // Act
        comment.UpdateContent(newContent);

        // Assert
        comment.Content.Should().Be(newContent);
    }

    [Fact]
    public void UpdateContent_WithEmptyContent_ThrowsArgumentException()
    {
        // Arrange
        var comment = Comment.Create(1, "user123", "Original content");
        var newContent = "";

        // Act & Assert
        var act = () => comment.UpdateContent(newContent);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*conteúdo*");
    }

    [Fact]
    public void UpdateContent_WithTooLongContent_ThrowsArgumentException()
    {
        // Arrange
        var comment = Comment.Create(1, "user123", "Original content");
        var newContent = new string('a', 2001);

        // Act & Assert
        var act = () => comment.UpdateContent(newContent);
        act.Should().Throw<ArgumentException>()
            .WithMessage("*não pode exceder 2000 caracteres*");
    }
}
