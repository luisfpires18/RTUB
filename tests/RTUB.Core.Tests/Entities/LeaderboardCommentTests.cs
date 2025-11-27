using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class LeaderboardCommentTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var targetUserId = "target-user-id";
        var authorId = "author-id";
        var text = "Great performance!";

        // Act
        var comment = LeaderboardComment.Create(targetUserId, authorId, text);

        // Assert
        comment.Should().NotBeNull();
        comment.TargetUserId.Should().Be(targetUserId);
        comment.AuthorId.Should().Be(authorId);
        comment.Text.Should().Be(text);
        comment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        comment.DeletedAt.Should().BeNull();
        comment.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyTargetUserId_ShouldThrowException(string? targetUserId)
    {
        // Act
        var act = () => LeaderboardComment.Create(targetUserId!, "author-id", "text");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Target User ID*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyAuthorId_ShouldThrowException(string? authorId)
    {
        // Act
        var act = () => LeaderboardComment.Create("target-id", authorId!, "text");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Author ID*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyText_ShouldThrowException(string? text)
    {
        // Act
        var act = () => LeaderboardComment.Create("target-id", "author-id", text!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Text*");
    }

    [Fact]
    public void Create_WithTextExceeding1000Characters_ShouldThrowException()
    {
        // Arrange
        var longText = new string('a', 1001);

        // Act
        var act = () => LeaderboardComment.Create("target-id", "author-id", longText);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*1000*");
    }

    [Fact]
    public void Create_WithTextAtMaxLength_ShouldCreateInstance()
    {
        // Arrange
        var maxLengthText = new string('a', 1000);

        // Act
        var comment = LeaderboardComment.Create("target-id", "author-id", maxLengthText);

        // Assert
        comment.Should().NotBeNull();
        comment.Text.Should().HaveLength(1000);
    }

    #endregion

    #region SoftDelete Tests

    [Fact]
    public void SoftDelete_WhenNotDeleted_ShouldSetDeletedAt()
    {
        // Arrange
        var comment = LeaderboardComment.Create("target-id", "author-id", "text");

        // Act
        comment.SoftDelete();

        // Assert
        comment.DeletedAt.Should().NotBeNull();
        comment.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        comment.IsDeleted.Should().BeTrue();
        comment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void SoftDelete_WhenCalled_ShouldSetDeletedAtAndUpdatedAt()
    {
        // Arrange
        var comment = LeaderboardComment.Create("target-id", "author-id", "text");

        // Act
        comment.SoftDelete();

        // Assert
        comment.DeletedAt.Should().NotBeNull();
        comment.UpdatedAt.Should().NotBeNull();
        comment.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        comment.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region IsDeleted Tests

    [Fact]
    public void IsDeleted_WhenDeletedAtIsNull_ShouldReturnFalse()
    {
        // Arrange
        var comment = LeaderboardComment.Create("target-id", "author-id", "text");

        // Assert
        comment.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void IsDeleted_WhenDeletedAtHasValue_ShouldReturnTrue()
    {
        // Arrange
        var comment = LeaderboardComment.Create("target-id", "author-id", "text");
        comment.SoftDelete();

        // Assert
        comment.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region Navigation Properties Tests

    [Fact]
    public void Likes_ShouldInitializeAsEmptyCollection()
    {
        // Arrange
        var comment = LeaderboardComment.Create("target-id", "author-id", "text");

        // Assert
        comment.Likes.Should().NotBeNull();
        comment.Likes.Should().BeEmpty();
    }

    #endregion
}
