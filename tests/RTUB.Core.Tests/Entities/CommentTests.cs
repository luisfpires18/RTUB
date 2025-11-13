using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

/// <summary>
/// Unit tests for Comment entity business logic
/// </summary>
public class CommentTests
{
    [Fact]
    public void Create_WithValidData_ReturnsComment()
    {
        // Arrange
        var postId = 1;
        var authorId = "user1";
        var body = "This is a test comment";

        // Act
        var comment = Comment.Create(postId, authorId, body);

        // Assert
        comment.Should().NotBeNull();
        comment.PostId.Should().Be(postId);
        comment.AuthorId.Should().Be(authorId);
        comment.Body.Should().Be(body);
        comment.IsEdited.Should().BeFalse();
        comment.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithInvalidBody_ThrowsException(string? invalidBody)
    {
        // Arrange
        var postId = 1;
        var authorId = "user1";

        // Act & Assert
        var act = () => Comment.Create(postId, authorId, invalidBody!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Edit_WithValidBody_UpdatesComment()
    {
        // Arrange
        var comment = Comment.Create(1, "user1", "Original comment");
        var newBody = "Updated comment";

        // Act
        comment.Edit(newBody);

        // Assert
        comment.Body.Should().Be(newBody);
        comment.IsEdited.Should().BeTrue();
        comment.UpdatedAt.Should().NotBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Edit_WithInvalidBody_ThrowsException(string? invalidBody)
    {
        // Arrange
        var comment = Comment.Create(1, "user1", "Original comment");

        // Act & Assert
        var act = () => comment.Edit(invalidBody!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedToTrue()
    {
        // Arrange
        var comment = Comment.Create(1, "user1", "Test comment");

        // Act
        comment.SoftDelete();

        // Assert
        comment.IsDeleted.Should().BeTrue();
        comment.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void SetMentions_SetsMentionsJson()
    {
        // Arrange
        var comment = Comment.Create(1, "user1", "Test comment");
        var mentionsJson = "{\"john\":\"user1\"}";

        // Act
        comment.SetMentions(mentionsJson);

        // Assert
        comment.MentionsJson.Should().Be(mentionsJson);
    }
}
