using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Pins the clean-up done by <see cref="UserProfileService.DeleteMemberWithRelatedDataAsync"/>:
/// which rows are removed or nulled, the order of the repository calls, and the three ways it
/// returns false. The user itself is deleted through a mocked <see cref="UserManager{TUser}"/>.
/// </summary>
public sealed class UserProfileServiceDeleteTests : IDisposable
{
    private const string Doomed = "doomed-user";
    private const string Other = "other-user";

    // ExecuteDelete/ExecuteUpdate need a relational provider, so this uses SQLite, not InMemory.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<ILeaderboardCommentRepository> _leaderboardComments = new();
    private readonly Mock<ICommentRepository> _comments = new();
    private readonly Mock<IPostRepository> _posts = new();
    private readonly Mock<IMeetingRepository> _meetings = new();
    private readonly Mock<IMeetingRequestRepository> _meetingRequests = new();
    private readonly Mock<ILogger<UserProfileService>> _logger = new();
    private readonly List<string> _calls = new();
    private readonly UserProfileService _service;

    public UserProfileServiceDeleteTests()
    {
        _connection.Open();
        _context = CreateContext();
        _context.Database.EnsureCreated();
        var factory = new Mock<IDbContextFactory<ApplicationDbContext>>();
        factory.Setup(f => f.CreateDbContext()).Returns(CreateContext);
        _userManager = new Mock<UserManager<ApplicationUser>>(
            new Mock<IUserStore<ApplicationUser>>().Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _service = new UserProfileService(
            _userManager.Object,
            factory.Object,
            Mock.Of<IImageStorageService>(),
            _leaderboardComments.Object,
            _comments.Object,
            _posts.Object,
            _meetings.Object,
            _meetingRequests.Object,
            _logger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options,
        Mock.Of<IHttpContextAccessor>(), new AuditContext(), new AuditLogAppender());

    // Entities with private constructors that the test only needs as placeholders.
    private static T New<T>() => (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    private static ApplicationUser NewUser(string id) => new()
    {
        Id = id, UserName = id, Email = id + "@example.com",
        FirstName = "F", LastName = "L", Nickname = id,
    };

    private void SeedRelatedData()
    {
        _context.Users.AddRange(NewUser(Doomed), NewUser(Other));
        _context.SaveChanges();

        var media = New<GalleryMedia>();
        media.UploaderId = Doomed;
        media.Title = "t";
        media.MediaType = MediaType.Image;
        media.MediaUrl = "u";
        media.Year = 2024;
        var naipe = New<NaipeContent>();
        naipe.InstrumentType = InstrumentType.Guitarra;
        naipe.Title = "n";
        naipe.Url = "u";
        naipe.MimeType = "audio/mp3";
        naipe.CreatedByUserId = Doomed;
        var board = LogisticsBoard.Create("b");
        _context.AddRange(media, naipe, board);
        _context.SaveChanges();

        _context.Add(GalleryMediaPersonTag.Create(media.Id, Other));
        _context.Add(NaipeComment.Create(naipe.Id, Other, "hi"));
        var list = LogisticsList.Create("l", board.Id, 0);
        _context.Add(list);
        _context.SaveChanges();

        var card = LogisticsCard.Create("c", list.Id, 0);
        card.AssignToUser(Doomed);
        _context.Add(card);
        _context.SaveChanges();
    }

    private void SetupHappyPathMocks(Meeting? meeting = null)
    {
        _userManager.Setup(x => x.FindByIdAsync(Doomed)).ReturnsAsync(NewUser(Doomed));
        _userManager.Setup(x => x.DeleteAsync(It.IsAny<ApplicationUser>()))
            .Callback(() => _calls.Add("user.Delete"))
            .ReturnsAsync(IdentityResult.Success);

        _leaderboardComments.Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<LeaderboardComment>, Task<List<LeaderboardComment>>>>()))
            .ReturnsAsync(new List<LeaderboardComment> { New<LeaderboardComment>() });
        _leaderboardComments.Setup(r => r.DeleteAsync(It.IsAny<LeaderboardComment>()))
            .Callback(() => _calls.Add("leaderboard.Delete")).Returns(Task.CompletedTask);

        _comments.Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<Comment>, Task<List<Comment>>>>()))
            .ReturnsAsync(new List<Comment> { New<Comment>() });
        _comments.Setup(r => r.DeleteAsync(It.IsAny<Comment>()))
            .Callback(() => _calls.Add("comment.Delete")).Returns(Task.CompletedTask);

        _posts.Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<Post>, Task<List<Post>>>>()))
            .ReturnsAsync(new List<Post> { New<Post>() });
        _posts.Setup(r => r.DeleteAsync(It.IsAny<Post>()))
            .Callback(() => _calls.Add("post.Delete")).Returns(Task.CompletedTask);

        _meetings.Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<Meeting>, Task<List<Meeting>>>>()))
            .ReturnsAsync(new List<Meeting> { meeting ?? New<Meeting>() });
        _meetings.Setup(r => r.UpdateAsync(It.IsAny<Meeting>()))
            .Callback(() => _calls.Add("meeting.Update")).Returns(Task.CompletedTask);

        _meetingRequests.Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<MeetingRequest>, Task<List<MeetingRequest>>>>()))
            .ReturnsAsync(new List<MeetingRequest> { New<MeetingRequest>() });
        _meetingRequests.Setup(r => r.DeleteAsync(It.IsAny<MeetingRequest>()))
            .Callback(() => _calls.Add("meetingRequest.Delete")).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Delete_RemovesRelatedRowsNullsReferencesAndDeletesUser()
    {
        SeedRelatedData();
        var meeting = New<Meeting>();
        meeting.OrganizerUserId = Doomed;
        meeting.TunoRepresentativeUserId = Other;
        SetupHappyPathMocks(meeting);

        var result = await _service.DeleteMemberWithRelatedDataAsync(Doomed);

        result.Should().BeTrue();
        await using var ctx = CreateContext();
        (await ctx.GalleryMedia.CountAsync()).Should().Be(0);
        (await ctx.GalleryMediaPersonTags.CountAsync()).Should().Be(0);
        (await ctx.NaipeContents.CountAsync()).Should().Be(0);
        (await ctx.NaipeComments.CountAsync()).Should().Be(0);
        (await ctx.LogisticsCards.SingleAsync()).AssignedToUserId.Should().BeNull();
        meeting.OrganizerUserId.Should().BeNull();
        meeting.TunoRepresentativeUserId.Should().Be(Other);
        _calls.Should().Equal(
            "leaderboard.Delete", "comment.Delete", "post.Delete",
            "meeting.Update", "meetingRequest.Delete", "user.Delete");
    }

    [Fact]
    public async Task Delete_UnknownUser_ReturnsFalseAndTouchesNothing()
    {
        _userManager.Setup(x => x.FindByIdAsync("missing")).ReturnsAsync((ApplicationUser?)null);

        var result = await _service.DeleteMemberWithRelatedDataAsync("missing");

        result.Should().BeFalse();
        _userManager.Verify(x => x.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
        _logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("missing")),
            null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task Delete_WhenIdentityRefusesDelete_ReturnsFalse()
    {
        SetupHappyPathMocks();
        _userManager.Setup(x => x.DeleteAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "nope" }));

        var result = await _service.DeleteMemberWithRelatedDataAsync(Doomed);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_WhenCleanupThrows_ReturnsFalseWithoutDeletingUser()
    {
        SetupHappyPathMocks();
        _posts.Setup(r => r.QueryAsync(It.IsAny<Func<IQueryable<Post>, Task<List<Post>>>>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _service.DeleteMemberWithRelatedDataAsync(Doomed);

        result.Should().BeFalse();
        _userManager.Verify(x => x.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }
}
