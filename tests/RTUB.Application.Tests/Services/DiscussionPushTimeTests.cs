using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Task 134: a new post or comment is pushed only while the event is still to come. An event's date is Portugal's
/// clock, so "now" must be Portugal's clock too. The event here starts at 21:00 on 14 July 2026 (summer: UTC+1).
/// </summary>
public class DiscussionPushTimeTests : IDisposable
{
    private const string Author = "author";
    private const string Reader = "reader";
    private static readonly TestClock HalfAnHourBefore = TestClock.AtUtc(2026, 7, 14, 19, 30); // 20:30 in Portugal
    private static readonly TestClock HalfAnHourAfter = TestClock.AtUtc(2026, 7, 14, 20, 30); // 21:30 in Portugal

    private readonly DatabaseFixture _db = new();
    private readonly Mock<IPushNotificationService> _push = new();
    private readonly Mock<IDiscussionRepository> _discussions = new();
    private readonly Mock<IUserProfileService> _users = new();
    private readonly int _discussionId;
    private readonly int _postId;

    public DiscussionPushTimeTests()
    {
        using var db = _db.CreateContext();
        db.Users.AddRange(User(Author), User(Reader));
        var concert = Event.Create("Serenata", new DateTime(2026, 7, 14, 21, 0, 0), "Bragança", EventType.Atuacao);
        db.Events.Add(concert);
        db.SaveChanges();
        var discussion = Discussion.Create(concert.Id);
        db.Discussions.Add(discussion);
        db.Enrollments.Add(Enrollment.Create(Reader, concert.Id));
        db.SaveChanges();
        var post = Post.Create(discussion.Id, Reader, "Boleias", "Quem leva carro?");
        db.Posts.Add(post);
        db.SaveChanges();
        (_discussionId, _postId) = (discussion.Id, post.Id);

        discussion.Event = concert;
        _discussions.Setup(d => d.GetByIdWithEventAsync(discussion.Id)).ReturnsAsync(discussion);
        _users.Setup(u => u.GetUserByIdAsync(Author)).ReturnsAsync(User(Author));
    }

    [Fact]
    public async Task APost_BeforeTheEventStarts_IsPushedToTheEnrolledMembers()
    {
        await Posts(HalfAnHourBefore).CreateAsync(_discussionId, Author, "Horas", "A que horas?");

        _push.Verify(p => p.SendToSelectedUsersAsync(It.Is<IEnumerable<string>>(ids => ids.Single() == Reader),
            It.IsAny<SendPushNotificationDto>()), Times.Once);
    }

    [Fact]
    public async Task APost_AfterTheEventStarted_IsNotPushed()
    {
        await Posts(HalfAnHourAfter).CreateAsync(_discussionId, Author, "Horas", "A que horas?");

        _push.Verify(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()), Times.Never);
    }

    [Fact]
    public async Task AComment_BeforeTheEventStarts_IsPushedToThePostAuthor()
    {
        await Comments(HalfAnHourBefore).CreateAsync(_postId, Author, "Eu levo.");

        _push.Verify(p => p.SendToSelectedUsersAsync(It.Is<IEnumerable<string>>(ids => ids.Single() == Reader),
            It.IsAny<SendPushNotificationDto>()), Times.Once);
    }

    [Fact]
    public async Task AComment_AfterTheEventStarted_IsNotPushed()
    {
        await Comments(HalfAnHourAfter).CreateAsync(_postId, Author, "Eu levo.");

        _push.Verify(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()), Times.Never);
    }

    private PostService Posts(TimeProvider clock) => new(
        new PostRepository(_db.CreateContextFactory()), _discussions.Object, new EnrollmentRepository(_db.CreateContextFactory()),
        Mock.Of<IPushNotificationFactory>(),
        _push.Object, _users.Object, clock);

    private CommentService Comments(TimeProvider clock) => new(
        new CommentRepository(_db.CreateContextFactory()),
        new PostRepository(_db.CreateContextFactory()), _discussions.Object, Mock.Of<IPostService>(),
        Mock.Of<IPushNotificationFactory>(), _push.Object, _users.Object, clock);

    private static ApplicationUser User(string id) => new()
    {
        Id = id, UserName = id, Email = $"{id}@example.test", FirstName = "Nome", LastName = "Apelido", Nickname = id,
    };

    public void Dispose() => _db.Dispose();
}
