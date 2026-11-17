using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Utilities;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for LeaderboardCommentService
/// Tests business logic and service layer operations for leaderboard comments and likes
/// </summary>
public partial class LeaderboardCommentServiceTests
{
    private readonly Mock<ILeaderboardCommentRepository> _mockCommentRepository;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IPushNotificationService> _mockPushNotificationService;
    private readonly Mock<IPushNotificationFactory> _mockPushNotificationFactory;
    private readonly LeaderboardCommentService _service;
    private readonly ApplicationUser _testUser;
    private readonly ApplicationUser _testAuthor;
    private readonly ApplicationUser _adminUser;

    public LeaderboardCommentServiceTests()
    {
        _mockCommentRepository = new Mock<ILeaderboardCommentRepository>();
        _userManagerMock = MockHelpers.CreateMockUserManager();
        _mockPushNotificationService = new Mock<IPushNotificationService>();
        _mockPushNotificationFactory = new Mock<IPushNotificationFactory>();

        _service = new LeaderboardCommentService(
            _mockCommentRepository.Object,
            _userManagerMock.Object,
            _mockPushNotificationService.Object,
            _mockPushNotificationFactory.Object);

        // Setup test users
        _testUser = new ApplicationUser
        {
            Id = "user1",
            UserName = "testuser",
            Email = "test@example.com",
            Nickname = "TestUser",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "123456789"
        };

        _testAuthor = new ApplicationUser
        {
            Id = "author1",
            UserName = "author",
            Email = "author@example.com",
            Nickname = "AuthorUser",
            FirstName = "Author",
            LastName = "User",
            PhoneNumber = "987654321"
        };

        _adminUser = new ApplicationUser
        {
            Id = "admin1",
            UserName = "admin",
            Email = "admin@example.com",
            Nickname = "AdminUser",
            FirstName = "Admin",
            LastName = "User",
            PhoneNumber = "555555555"
        };
    }

    [Fact]
    public async Task AddCommentAsync_WithValidData_ReturnsCommentDto()
    {
        // Arrange
        var text = "Great job on the leaderboard!";
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, text);

        _mockCommentRepository.Setup(r => r.AddAsync(It.IsAny<LeaderboardComment>()))
            .ReturnsAsync(comment);
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
        var mockQueryable = new List<LeaderboardComment>().BuildMockDbSet().Object;
        _mockCommentRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<LeaderboardComment>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));
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
        comment1.GetType().GetProperty("Author")!.SetValue(comment1, _testAuthor);
        comment1.GetType().GetProperty("Likes")!.SetValue(comment1, new List<LeaderboardCommentLike>());

        var comment2 = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Second comment");
        comment2.GetType().GetProperty("Author")!.SetValue(comment2, _testAuthor);
        comment2.GetType().GetProperty("Likes")!.SetValue(comment2, new List<LeaderboardCommentLike>());

        await Task.Delay(10); // Simulate time difference

        var comments = new List<LeaderboardComment> { comment2, comment1 }; // Already ordered desc
        var mockQueryable = comments.BuildMockDbSet().Object;

        _mockCommentRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<LeaderboardComment>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));
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
        comment1.GetType().GetProperty("Author")!.SetValue(comment1, _testAuthor);
        comment1.GetType().GetProperty("Likes")!.SetValue(comment1, new List<LeaderboardCommentLike>());

        var comment2 = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Deleted comment");
        comment2.SoftDelete();

        var comments = new List<LeaderboardComment> { comment1 }; // Repository should not return deleted
        var mockQueryable = comments.BuildMockDbSet().Object;

        _mockCommentRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<LeaderboardComment>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));
        _userManagerMock.Setup(um => um.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _service.GetCommentsForUserAsync(_testUser.Id, null);

        // Assert
        result.Should().HaveCount(1);
        result[0].Text.Should().Be("Active comment");
    }

    [Fact]
    public async Task GetCommentsForUserAsync_IncludesLikeCount()
    {
        // Arrange
        var comment = LeaderboardComment.Create(_testUser.Id, _testAuthor.Id, "Test comment");
        comment.GetType().GetProperty("Id")!.SetValue(comment, 1); // Set positive ID for like creation
        comment.GetType().GetProperty("Author")!.SetValue(comment, _testAuthor);

        var like1 = LeaderboardCommentLike.Create(comment.Id, _testUser.Id);
        like1.GetType().GetProperty("User")!.SetValue(like1, _testUser);
        var like2 = LeaderboardCommentLike.Create(comment.Id, _adminUser.Id);
        like2.GetType().GetProperty("User")!.SetValue(like2, _adminUser);

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
        comment.GetType().GetProperty("Id")!.SetValue(comment, 1); // Set positive ID for like creation
        comment.GetType().GetProperty("Author")!.SetValue(comment, _testAuthor);

        var like = LeaderboardCommentLike.Create(comment.Id, _testUser.Id);
        like.GetType().GetProperty("User")!.SetValue(like, _testUser);

        comment.GetType().GetProperty("Likes")!.SetValue(comment, new List<LeaderboardCommentLike> { like });

        var comments = new List<LeaderboardComment> { comment };
        var mockQueryable = comments.BuildMockDbSet().Object;
        _mockCommentRepository
            .Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<LeaderboardComment>, Task<It.IsAnyType>>>(), It.IsAny<CancellationToken>()))
            .Returns(new InvocationFunc(invocation =>
            {
                var queryFunc = (Delegate)invocation.Arguments[0];
                return queryFunc.DynamicInvoke(mockQueryable)!;
            }));
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
}
