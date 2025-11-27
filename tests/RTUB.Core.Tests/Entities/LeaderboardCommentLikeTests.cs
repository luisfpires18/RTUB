using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class LeaderboardCommentLikeTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var commentId = 1;
        var userId = "user-id-123";

        // Act
        var like = LeaderboardCommentLike.Create(commentId, userId);

        // Assert
        like.Should().NotBeNull();
        like.CommentId.Should().Be(commentId);
        like.UserId.Should().Be(userId);
        like.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public void Create_WithPositiveCommentId_ShouldCreateInstance(int commentId)
    {
        // Act
        var like = LeaderboardCommentLike.Create(commentId, "user-id");

        // Assert
        like.Should().NotBeNull();
        like.CommentId.Should().Be(commentId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithNonPositiveCommentId_ShouldThrowException(int invalidCommentId)
    {
        // Act
        var act = () => LeaderboardCommentLike.Create(invalidCommentId, "user-id");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Comment ID must be positive*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyUserId_ShouldThrowException(string? userId)
    {
        // Act
        var act = () => LeaderboardCommentLike.Create(1, userId!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*User ID*");
    }

    #endregion

    #region Properties Tests

    [Fact]
    public void BaseEntityProperties_ShouldBeAccessible()
    {
        // Arrange
        var like = LeaderboardCommentLike.Create(1, "user-id");
        var now = DateTime.UtcNow;

        // Act
        like.CreatedBy = "creator";
        like.UpdatedAt = now.AddHours(1);
        like.UpdatedBy = "updater";

        // Assert
        like.CreatedBy.Should().Be("creator");
        like.UpdatedAt.Should().Be(now.AddHours(1));
        like.UpdatedBy.Should().Be("updater");
    }

    #endregion
}
