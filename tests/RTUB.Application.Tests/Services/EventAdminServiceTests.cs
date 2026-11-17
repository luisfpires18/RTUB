using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// The image, cancel / reactivate and notice actions behind the React agenda (React track 012A).
/// <see cref="IEventService"/> is a fake that writes to the test database; email and push are
/// recording fakes, so nothing is ever uploaded or sent.
/// </summary>
public partial class EventAdminServiceTests
{
    private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());
    private static readonly ClaimsPrincipal Member = SignedIn("Member");
    private static readonly ClaimsPrincipal Mod = SignedIn("Mod");
    private static readonly ClaimsPrincipal Admin = SignedIn("Admin");
    private static readonly ClaimsPrincipal Owner = SignedIn("Owner");

    private static readonly byte[] Webp = "RIFF\0\0\0\0WEBPVP8 "u8.ToArray();
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1 };

    private readonly DatabaseFixture _db = new();
    private readonly Mock<IEventService> _events = new();
    private readonly Mock<IEmailNotificationService> _email = new();
    private readonly Mock<IPushNotificationService> _push = new();
    private readonly Mock<IPushNotificationFactory> _pushFactory = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly Mock<ITrophyService> _trophies = new();
    private readonly EventAdminService _service;
    private readonly DateTime _future = DateTime.Today.AddDays(30);

    public EventAdminServiceTests()
    {
        _service = new EventAdminService(new Contexts(_db), _events.Object, _email.Object, _push.Object, _pushFactory.Object,
            _audit.Object, _trophies.Object, NullLogger<EventAdminService>.Instance);
        FakeEventWrites();
        _pushFactory.Setup(f => f.CreateEventCustomNotification(It.IsAny<Event>(), It.IsAny<string>()))
            .Returns((Event e, string body) => new SendPushNotificationDto { Title = e.Name, Body = body, Url = $"/events/{e.Id}" });
        _push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()))
            .ReturnsAsync((IEnumerable<string> ids, SendPushNotificationDto _) => (ids.Count(), 0));
        EmailSucceeds();
    }

    // ---------- who may ----------

    public static TheoryData<string, EventResultStatus> Refused => new()
    {
        { "visitor", EventResultStatus.SignInRequired },
        { "member", EventResultStatus.Forbidden },
        { "mod", EventResultStatus.Forbidden },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task OnlyAdminOrOwner_MayChangeTheImage_CancelReactivateOrNotify(string who, EventResultStatus expected)
    {
        var user = who switch { "visitor" => Visitor, "member" => Member, _ => Mod };
        var id = AddEvent("Serenata", _future);
        var cancelled = AddEvent("Cancelada", _future, cancelled: true);
        AddUser("a@rtub.test", push: true);

        // The image belongs to editing the event: a Mod may change it, so only visitors and members are refused here.
        if (who != "mod")
        {
            (await _service.SetImageAsync(id, Upload(Webp), user)).Status.Should().Be(expected);
            (await _service.RemoveImageAsync(id, user)).Status.Should().Be(expected);
        }

        (await _service.CancelAsync(id, new EventCancelInput("Chuva", true), user, "https://rtub.test")).Status.Should().Be(expected);
        (await _service.ReactivateAsync(cancelled, user)).Status.Should().Be(expected);
        (await _service.GetNoticeAudienceAsync(id, user)).Status.Should().Be(expected);
        (await _service.SendNoticeAsync(id, Email("new"), user, "https://rtub.test")).Status.Should().Be(expected);
        (await _service.SendNoticeAsync(id, Push("Olá"), user, "https://rtub.test")).Status.Should().Be(expected);

        _events.VerifyNoOtherCalls();
        _email.VerifyNoOtherCalls();
        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnOwner_InheritsAdmin()
    {
        var id = AddEvent("Serenata", _future);

        (await _service.CancelAsync(id, new EventCancelInput("Chuva", false), Owner, "x")).Status.Should().Be(EventResultStatus.Ok);
        (await _service.ReactivateAsync(id, Owner)).Status.Should().Be(EventResultStatus.Ok);
        (await _service.SetImageAsync(id, Upload(Jpeg, "image/jpeg"), Owner)).Status.Should().Be(EventResultStatus.Ok);
    }

    [Fact]
    public async Task AMissingEvent_IsNotFound()
    {
        (await _service.SetImageAsync(999, Upload(Webp), Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.RemoveImageAsync(999, Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.CancelAsync(999, new EventCancelInput("x", false), Admin, "x")).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.ReactivateAsync(999, Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.SendNoticeAsync(999, Email("new"), Admin, "x")).Status.Should().Be(EventResultStatus.NotFound);
        _events.VerifyNoOtherCalls();
    }

    // ---------- image ----------

    [Fact]
    public async Task Image_GoesThroughTheExistingReplacePath()
    {
        var id = AddEvent("Serenata", _future);

        var result = await _service.SetImageAsync(id, Upload(Webp), Admin);

        result.Status.Should().Be(EventResultStatus.Ok);
        _events.Verify(s => s.SetEventImageAsync(id, It.IsAny<Stream>(), "event-image.webp", "image/webp", default), Times.Once,
            "SetEventImageAsync deletes the old image and stores the new one under the existing key convention");
    }

    [Theory]
    [InlineData("image/gif", false, 12)]
    [InlineData("image/webp", true, 0)]
    [InlineData("image/webp", false, 12)]
    [InlineData("text/html", true, 12)]
    public async Task Image_RefusesWhatIsNotAWebpJpegOrPng(string contentType, bool realBytes, int length)
    {
        var id = AddEvent("Serenata", _future);
        var bytes = realBytes ? Webp : "<html><body>x"u8.ToArray();

        var result = await _service.SetImageAsync(id, new EventImageUpload(new MemoryStream(bytes), "x", contentType, length), Admin);

        result.Status.Should().Be(EventResultStatus.Invalid);
        result.Errors!.Keys.Should().Contain("image");
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Image_RefusesMoreThan5MB()
    {
        var id = AddEvent("Serenata", _future);

        var result = await _service.SetImageAsync(id, new EventImageUpload(new MemoryStream(Webp), "x", "image/webp", EventAdminService.MaxImageBytes + 1), Admin);

        result.Errors!["image"].Single().Should().Contain("5 MB");
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RemoveImage_UsesTheEventService()
    {
        var id = AddEvent("Serenata", _future);

        (await _service.RemoveImageAsync(id, Admin)).Status.Should().Be(EventResultStatus.Ok);
        _events.Verify(s => s.RemoveEventImageAsync(id, default), Times.Once);
    }

    // ---------- helpers ----------

    private static ClaimsPrincipal SignedIn(string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "u-" + role),
            new Claim(ClaimTypes.Role, role),
        }, "test"));

    private static EventImageUpload Upload(byte[] bytes, string type = "image/webp") =>
        new(new MemoryStream(bytes), type == "image/jpeg" ? "event-image.jpg" : "event-image.webp", type, bytes.Length);

    private static EventNoticeInput Email(string kind) => new("email", kind, null, false);

    private static EventNoticeInput Push(string message, bool young = false) => new("push", null, message, young);

    private int AddEvent(string name, DateTime date, DateTime? endDate = null, bool cancelled = false)
    {
        using var db = _db.CreateContext();
        var e = Event.Create(name, date, "Bragança", EventType.Atuacao, "");
        e.EndDate = endDate;
        if (cancelled)
        {
            e.Cancel("Antes");
        }

        db.Events.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private readonly List<string> _pushIds = new();

    private string AddUser(string email, bool subscribed = true, bool confirmed = true, bool push = false,
        MemberCategory category = MemberCategory.Tuno)
    {
        using var db = _db.CreateContext();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = confirmed,
            Subscribed = subscribed,
            Nickname = email.Split('@')[0],
            FirstName = "Nome",
            LastName = "Apelido",
            Categories = new List<MemberCategory> { category },
        };
        db.Users.Add(user);
        db.SaveChanges();
        if (push)
        {
            _pushIds.Add(user.Id);
        }

        _push.Setup(p => p.GetSubscribedUserIdsAsync()).ReturnsAsync(() => _pushIds.ToList());
        return user.Id;
    }

    private void EmailSucceeds()
    {
        (bool, int, string?) Sent(List<string> to) => (true, to.Count, null);
        _email.Setup(m => m.SendEventNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((int _, string _, DateTime _, string _, string _, List<string> to, Dictionary<string, (string, string)>? _, string _,
                DateTime? _, IProgress<EmailSendProgress>? _) => Sent(to));
        _email.Setup(m => m.SendEventReminderNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((int _, string _, DateTime _, string _, string _, List<string> to, Dictionary<string, (string, string)>? _, string _,
                DateTime? _, IProgress<EmailSendProgress>? _) => Sent(to));
        _email.Setup(m => m.SendEventCancellationNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, (string, string)>>(),
                It.IsAny<DateTime?>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((int _, string _, DateTime _, string _, string _, string _, List<string> to, Dictionary<string, (string, string)>? _,
                DateTime? _, IProgress<EmailSendProgress>? _) => Sent(to));
    }

    /// <summary>The fake stores what the real EventService would for cancel and reactivate.</summary>
    private void FakeEventWrites()
    {
        _events.Setup(s => s.CancelEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((int id, string reason, CancellationToken _) =>
            {
                using var db = _db.CreateContext();
                db.Events.Single(x => x.Id == id).Cancel(reason);
                db.SaveChanges();
                return Task.CompletedTask;
            });
        _events.Setup(s => s.UncancelEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int id, CancellationToken _) =>
            {
                using var db = _db.CreateContext();
                db.Events.Single(x => x.Id == id).Uncancel();
                db.SaveChanges();
                return Task.CompletedTask;
            });
    }

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
