using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for LeaderboardCommentService
/// Tests business logic and service layer operations for leaderboard comments and likes
/// </summary>
public class LeaderboardCommentServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly LeaderboardCommentService _service;
    private readonly ApplicationUser _testUser;
    private readonly ApplicationUser _testAuthor;
    private readonly ApplicationUser _adminUser;

    public LeaderboardCommentServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(
            options, 
            Mock.Of<Microsoft.AspNetCore.Http.IHttpContextAccessor>(), 
            new AuditContext());

        // Mock UserManager
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            null!, null!, null!, null!, null!, null!, null!, null!);

        _service = new LeaderboardCommentService(_context, _userManagerMock.Object);

        // Setup test users
        _testUser = new ApplicationUser
        {
            Id = "user1",
            UserName = "testuser",
            Email = "test@example.com",
            Nickname = "TestUser",
            FirstName = "Test",
            LastName = "User",
            PhoneContact = "123456789"
        };

        _testAuthor = new ApplicationUser
        {
            Id = "author1",
            UserName = "author",
            Email = "author@example.com",
            Nickname = "AuthorUser",
            FirstName = "Author",
            LastName = "User",
            PhoneContact = "987654321"
        };

        _adminUser = new ApplicationUser
        {
            Id = "admin1",
            UserName = "admin",
            Email = "admin@example.com",
            Nickname = "AdminUser",
            FirstName = "Admin",
            LastName = "User",
            PhoneContact = "555555555"
        };

        _context.Users.AddRange(_testUser, _testAuthor, _adminUser);
        _context.SaveChanges();
    }

    [Fact]
    public async Task AddCommentAsync_WithValidData_ReturnsCommentDto()
    {
        // Arrange
        var text = "Great job on the leaderboard!";
        _userManagerMock.Setup(um => um.FindByIdAsync(_testAuthor.Id))
            .ReturnsAsync(_testAuthor);
        _userManagerMock.Setup(um => um.GetRolesAsync(_testAuthor))
            .ReturnsAsync(new List<string>());

        // Act
        var result = await _service.AddCommentAsync(_testUser.Id, _testAuthor.Id, text);

        // Assert
        result.Should().NotBeNull();
        result.Text.Should().Be(text);
        result.AuthorId.Should().Be(_testAuthor.Id);
        result.TargetUserId.Should().Be(_testUser.Id);
        result.AuthorName.Should().Be("AuthorUser");
        result.LikesCount.Should().Be(0);
        result.IsLikedByCurrentUser.Should().BeFalse();
        result.CanDelete.Should().BeTrue();
    }

    [Fact]
    public async Task AddCommentAsync_WithEmptyText_ThrowsArgumentException()
    {
        // Act
        var act = async () => await _service.AddCommentAsync(_testUser.Id, _testAuthor.Id, "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetCommentsForUserAsync_WithNoComments_ReturnsEmptyList()
    {
        // Arrange
        _userManagerMock.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.GetCommentsForUserAsync(_testUser.Id, null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCommentsForUserAsync_WithComments_ReturnsOrderedByCreatedAtDesc()
    {
        // Arrange
        var comment1 = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "First comment");
        _context.LeaderboardComments.Add(comment1);
        await _context.SaveChangesAsync();
        
        await Task.Delay(100); // Ensure different timestamps
        
        var comment2 = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Second comment");
        _context.LeaderboardComments.Add(comment2);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.GetCommentsForUserAsync(_testUser.Id, null);

        // Assert
        result.Should().HaveCount(2);
        result[0].Text.Should().Be("Second comment");
        result[1].Text.Should().Be("First comment");
    }

    [Fact]
    public async Task GetCommentsForUserAsync_ExcludesDeletedComments()
    {
        // Arrange
        var comment1 = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Active comment");
        var comment2 = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Deleted comment");
        comment2.SoftDelete();
        
        _context.LeaderboardComments.AddRange(comment1, comment2);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.GetCommentsForUserAsync(_testUser.Id, null);

        // Assert
        result.Should().HaveCount(1);
        result[0].Text.Should().Be("Active comment");
    }

    [Fact]
    public async Task ToggleLikeAsync_WhenNoLikeExists_AddsLike()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        _context.LeaderboardComments.Add(comment);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ToggleLikeAsync(comment.Id, _testUser.Id);

        // Assert
        result.Should().BeTrue(); // Liked
        var likes = await _context.LeaderboardCommentLikes
            .Where(l => l.CommentId == comment.Id && l.UserId == _testUser.Id)
            .ToListAsync();
        likes.Should().HaveCount(1);
    }

    [Fact]
    public async Task ToggleLikeAsync_WhenLikeExists_RemovesLike()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        _context.LeaderboardComments.Add(comment);
        await _context.SaveChangesAsync();

        var like = LeaderboardCommentLike.Create(comment.Id, _testUser.Id);
        _context.LeaderboardCommentLikes.Add(like);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ToggleLikeAsync(comment.Id, _testUser.Id);

        // Assert
        result.Should().BeFalse(); // Unliked
        var likes = await _context.LeaderboardCommentLikes
            .Where(l => l.CommentId == comment.Id && l.UserId == _testUser.Id)
            .ToListAsync();
        likes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCommentsForUserAsync_IncludesLikeCount()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        _context.LeaderboardComments.Add(comment);
        await _context.SaveChangesAsync();

        var like1 = LeaderboardCommentLike.Create(comment.Id, "user2");
        var like2 = LeaderboardCommentLike.Create(comment.Id, "user3");
        _context.LeaderboardCommentLikes.AddRange(like1, like2);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.GetCommentsForUserAsync(_testUser.Id, null);

        // Assert
        result.Should().HaveCount(1);
        result[0].LikesCount.Should().Be(2);
    }

    [Fact]
    public async Task GetCommentsForUserAsync_IndicatesIfCurrentUserLiked()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        _context.LeaderboardComments.Add(comment);
        await _context.SaveChangesAsync();

        var like = LeaderboardCommentLike.Create(comment.Id, _testUser.Id);
        _context.LeaderboardCommentLikes.Add(like);
        await _context.SaveChangesAsync();

        _userManagerMock.Setup(um => um.FindByIdAsync(_testUser.Id))
            .ReturnsAsync(_testUser);
        _userManagerMock.Setup(um => um.GetRolesAsync(_testUser))
            .ReturnsAsync(new List<string>());

        // Act
        var result = await _service.GetCommentsForUserAsync(_testUser.Id, _testUser.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].IsLikedByCurrentUser.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCommentAsync_AsAuthor_SoftDeletesComment()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        _context.LeaderboardComments.Add(comment);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteCommentAsync(comment.Id, _testAuthor.Id, false);

        // Assert
        var deletedComment = await _context.LeaderboardComments.FindAsync(comment.Id);
        deletedComment.Should().NotBeNull();
        deletedComment!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCommentAsync_AsAdmin_SoftDeletesComment()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        _context.LeaderboardComments.Add(comment);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteCommentAsync(comment.Id, _adminUser.Id, true);

        // Assert
        var deletedComment = await _context.LeaderboardComments.FindAsync(comment.Id);
        deletedComment.Should().NotBeNull();
        deletedComment!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCommentAsync_AsNonAuthorNonAdmin_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        _context.LeaderboardComments.Add(comment);
        await _context.SaveChangesAsync();

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
        _context.LeaderboardComments.Add(comment);
        await _context.SaveChangesAsync();

        var like1 = LeaderboardCommentLike.Create(comment.Id, "user2");
        var like2 = LeaderboardCommentLike.Create(comment.Id, "user3");
        _context.LeaderboardCommentLikes.AddRange(like1, like2);
        await _context.SaveChangesAsync();

        // Act
        await _service.DeleteCommentAsync(comment.Id, _testAuthor.Id, false);

        // Assert
        var likes = await _context.LeaderboardCommentLikes
            .Where(l => l.CommentId == comment.Id)
            .ToListAsync();
        likes.Should().BeEmpty();
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

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
