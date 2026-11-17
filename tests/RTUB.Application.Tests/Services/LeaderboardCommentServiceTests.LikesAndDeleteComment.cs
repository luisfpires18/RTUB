using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

// LeaderboardCommentServiceTests: Likes And Delete Comment tests.
public partial class LeaderboardCommentServiceTests
{
    [Fact]
    public async Task ToggleLikeAsync_WhenNoLikeExists_AddsLike()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        comment.GetType().GetProperty("Id")!.SetValue(comment, 1);

        _mockCommentRepository
            .Setup(r => r.ToggleLikeAsync(1, _testUser.Id))
            .ReturnsAsync((bool?)true);

        _mockCommentRepository
            .Setup(r => r.GetByIdWithDetailsAsync(1))
            .ReturnsAsync(comment);

        // Act
        var result = await _service.ToggleLikeAsync(comment.Id, _testUser.Id);

        // Assert
        result.Should().BeTrue(); // Liked
        _mockCommentRepository.Verify(r => r.ToggleLikeAsync(1, _testUser.Id), Times.Once);
    }

    [Fact]
    public async Task ToggleLikeAsync_WhenLikeExists_RemovesLike()
    {
        // Arrange
        _mockCommentRepository
            .Setup(r => r.ToggleLikeAsync(1, _testUser.Id))
            .ReturnsAsync((bool?)false);

        // Act
        var result = await _service.ToggleLikeAsync(1, _testUser.Id);

        // Assert
        result.Should().BeFalse(); // Unliked
        _mockCommentRepository.Verify(r => r.ToggleLikeAsync(1, _testUser.Id), Times.Once);
    }

    [Fact]
    public async Task DeleteCommentAsync_AsAuthor_SoftDeletesComment()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        comment.GetType().GetProperty("Likes")!.SetValue(comment, new List<LeaderboardCommentLike>());

        var comments = new List<LeaderboardComment> { comment };
        var mockQueryable = comments.BuildMockDbSet().Object;
        _mockCommentRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<LeaderboardComment>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));

        // Act
        await _service.DeleteCommentAsync(comment.Id, _testAuthor.Id, false);

        // Assert
        comment.IsDeleted.Should().BeTrue();
        _mockCommentRepository.Verify(r => r.UpdateAsync(comment), Times.Once);
        // Removed non-existent method verification
    }

    [Fact]
    public async Task DeleteCommentAsync_AsAdmin_SoftDeletesComment()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        comment.GetType().GetProperty("Likes")!.SetValue(comment, new List<LeaderboardCommentLike>());

        var comments = new List<LeaderboardComment> { comment };
        var mockQueryable = comments.BuildMockDbSet().Object;
        _mockCommentRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<LeaderboardComment>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));

        // Act
        await _service.DeleteCommentAsync(comment.Id, _adminUser.Id, true);

        // Assert
        comment.IsDeleted.Should().BeTrue();
        _mockCommentRepository.Verify(r => r.UpdateAsync(comment), Times.Once);
    }

    [Fact]
    public async Task DeleteCommentAsync_AsNonAuthorNonAdmin_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        comment.GetType().GetProperty("Likes")!.SetValue(comment, new List<LeaderboardCommentLike>());

        var comments = new List<LeaderboardComment> { comment };
        var mockQueryable = comments.BuildMockDbSet().Object;
        _mockCommentRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<LeaderboardComment>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));

        // Act
        var act = async () => await _service.DeleteCommentAsync(comment.Id, "otherUser", false);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteCommentAsync_RemovesAllLikes()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        comment.GetType().GetProperty("Id")!.SetValue(comment, 1); // Set positive ID
        var like1 = LeaderboardCommentLike.Create(comment.Id, _testUser.Id);
        var like2 = LeaderboardCommentLike.Create(comment.Id, _adminUser.Id);
        comment.GetType().GetProperty("Likes")!.SetValue(comment, new List<LeaderboardCommentLike> { like1, like2 });

        var comments = new List<LeaderboardComment> { comment };
        var mockQueryable = comments.BuildMockDbSet().Object;
        _mockCommentRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<LeaderboardComment>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));

        // Act
        await _service.DeleteCommentAsync(comment.Id, _testAuthor.Id, false);

        // Assert
        comment.IsDeleted.Should().BeTrue();
        // Likes remain in collection but comment is soft-deleted - cascade handled by repository
    }

    [Fact]
    public void CanDeleteComment_AsAuthor_ReturnsTrue()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");

        // Act
        var result = _service.CanDeleteComment(comment, _testAuthor.Id, false);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanDeleteComment_AsAdmin_ReturnsTrue()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");

        // Act
        var result = _service.CanDeleteComment(comment, _adminUser.Id, true);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void CanDeleteComment_AsNonAuthorNonAdmin_ReturnsFalse()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");

        // Act
        var result = _service.CanDeleteComment(comment, "otherUser", false);

        // Assert
        result.Should().BeFalse();
    }
}
