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
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// The image, cancel / reactivate and notice actions behind the React agenda (React track 012A).
/// <see cref="IEventService"/> is a fake that writes to the test database; email and push are
/// recording fakes, so nothing is ever uploaded or sent.
/// </summary>
public class EventAdminServiceTests
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
    private readonly EventAdminService _service;
    private readonly DateTime _future = DateTime.Today.AddDays(30);

    public EventAdminServiceTests()
    {
        _service = new EventAdminService(new Contexts(_db), _events.Object, _email.Object, _push.Object, _pushFactory.Object,
            _audit.Object, NullLogger<EventAdminService>.Instance);
        FakeEventWrites();
        _pushFactory.Setup(f => f.CreateEventCustomNotification(It.IsAny<Event>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((Event e, string body, string url) => new SendPushNotificationDto { Title = e.Name, Body = body, Url = url + "/events" });
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

        (await _service.SetImageAsync(id, Upload(Webp), user)).Status.Should().Be(expected);
        (await _service.RemoveImageAsync(id, user)).Status.Should().Be(expected);
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

    // ---------- cancel / reactivate ----------

    [Fact]
    public async Task Cancel_NeedsAReason_OfAtMost1000Characters()
    {
        var id = AddEvent("Serenata", _future);

        (await _service.CancelAsync(id, new EventCancelInput("  ", false), Admin, "x")).Errors!.Keys.Should().Contain("reason");
        (await _service.CancelAsync(id, new EventCancelInput(new string('r', 1001), false), Admin, "x")).Errors!.Keys.Should().Contain("reason");
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_WithoutEmail_CancelsAndSendsNothing()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("a@rtub.test");

        var result = await _service.CancelAsync(id, new EventCancelInput("  Chuva  ", false), Admin, "https://rtub.test");

        result.Status.Should().Be(EventResultStatus.Ok);
        result.Value!.Sent.Should().Be(0);
        _events.Verify(s => s.CancelEventAsync(id, "Chuva", default), Times.Once);
        _email.VerifyNoOtherCalls();
        (await EventAsync(id)).IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_WithEmail_WritesToSubscribedConfirmedAddressesOnly()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("yes@rtub.test");
        AddUser("unsubscribed@rtub.test", subscribed: false);
        AddUser("unconfirmed@rtub.test", confirmed: false);

        var result = await _service.CancelAsync(id, new EventCancelInput("Chuva", true), Admin, "https://rtub.test");

        result.Value!.Sent.Should().Be(1);
        _email.Verify(m => m.SendEventCancellationNotificationAsync(id, "Serenata", It.IsAny<DateTime>(), "Bragança", "Chuva",
            "https://rtub.test/events", It.Is<List<string>>(l => l.SequenceEqual(new[] { "yes@rtub.test" })),
            It.IsAny<Dictionary<string, (string, string)>>(), null, null), Times.Once);
    }

    [Fact]
    public async Task Cancel_StaysCancelled_WhenTheEmailFails()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("yes@rtub.test");
        _email.Setup(m => m.SendEventCancellationNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, (string, string)>>(),
                It.IsAny<DateTime?>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var result = await _service.CancelAsync(id, new EventCancelInput("Chuva", true), Admin, "x");

        result.Status.Should().Be(EventResultStatus.Ok);
        result.Value!.Warning.Should().Contain("foi cancelada").And.Contain("não foram enviados");
        (await EventAsync(id)).IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task Cancel_IsClosed_ForAPastOrAlreadyCancelledEvent()
    {
        var past = AddEvent("Passada", DateTime.Today.AddDays(-3));
        var cancelled = AddEvent("Cancelada", _future, cancelled: true);

        (await _service.CancelAsync(past, new EventCancelInput("x", false), Admin, "x")).Status.Should().Be(EventResultStatus.Closed);
        (await _service.CancelAsync(cancelled, new EventCancelInput("x", false), Admin, "x")).Status.Should().Be(EventResultStatus.Closed);
        _events.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Cancel_IsOpen_OnTheLastDayOfAMultiDayEvent()
    {
        var id = AddEvent("Festival", DateTime.Today.AddDays(-2), endDate: DateTime.Today);

        (await _service.CancelAsync(id, new EventCancelInput("x", false), Admin, "x")).Status.Should().Be(EventResultStatus.Ok);
    }

    [Fact]
    public async Task Reactivate_OnlyACancelledUpcomingEvent()
    {
        var open = AddEvent("Aberta", _future);
        var pastCancelled = AddEvent("Passada", DateTime.Today.AddDays(-3), cancelled: true);
        var cancelled = AddEvent("Cancelada", _future, cancelled: true);

        (await _service.ReactivateAsync(open, Admin)).Status.Should().Be(EventResultStatus.Closed);
        (await _service.ReactivateAsync(pastCancelled, Admin)).Status.Should().Be(EventResultStatus.Closed);
        (await _service.ReactivateAsync(cancelled, Admin)).Status.Should().Be(EventResultStatus.Ok);

        _events.Verify(s => s.UncancelEventAsync(cancelled, default), Times.Once);
        _events.Verify(s => s.UncancelEventAsync(It.Is<int>(i => i != cancelled), default), Times.Never);
        _email.VerifyNoOtherCalls();
        _push.VerifyNoOtherCalls();
    }

    // ---------- notices ----------

    [Fact]
    public async Task Audience_IsCountsOnly()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("a@rtub.test", push: true);
        AddUser("b@rtub.test", subscribed: false, push: true, category: MemberCategory.Leitao);
        AddUser("c@rtub.test", confirmed: false, category: MemberCategory.Caloiro);

        var audience = (await _service.GetNoticeAudienceAsync(id, Admin)).Value!;

        audience.Should().Be(new EventNoticeAudienceDto(EmailSubscribed: 1, EmailTotal: 2, PushSubscribed: 2, PushTotal: 3,
            PushLeitoesCaloirosSubscribed: 1, PushLeitoesCaloirosTotal: 2));
    }

    [Theory]
    [InlineData("new")]
    [InlineData("reminder")]
    public async Task EmailNotice_UsesTheOldTemplates_ForSubscribedConfirmedAddresses(string kind)
    {
        var id = AddEvent("Serenata", _future);
        AddUser("yes@rtub.test");
        AddUser("no@rtub.test", subscribed: false);

        var result = await _service.SendNoticeAsync(id, Email(kind), Admin, "https://rtub.test");

        result.Value!.Sent.Should().Be(1);
        if (kind == "new")
        {
            _email.Verify(m => m.SendEventNotificationAsync(id, "Serenata", It.IsAny<DateTime>(), "Bragança", "https://rtub.test/events",
                It.Is<List<string>>(l => l.SequenceEqual(new[] { "yes@rtub.test" })), It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<string>(), null, null), Times.Once);
        }
        else
        {
            _email.Verify(m => m.SendEventReminderNotificationAsync(id, "Serenata", It.IsAny<DateTime>(), "Bragança", "https://rtub.test/events",
                It.Is<List<string>>(l => l.SequenceEqual(new[] { "yes@rtub.test" })), It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<string>(), null, null), Times.Once);
        }

        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EmailNotice_ShowsTheEmailServicesRefusal_ForExampleItsRateLimit()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("yes@rtub.test");
        _email.Setup(m => m.SendEventNotificationAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<Dictionary<string, (string, string)>>(), It.IsAny<string>(),
                It.IsAny<DateTime?>(), It.IsAny<IProgress<EmailSendProgress>?>()))
            .ReturnsAsync((false, 0, "Email já enviado recentemente para este evento."));

        var result = await _service.SendNoticeAsync(id, Email("new"), Admin, "x");

        result.Status.Should().Be(EventResultStatus.Invalid);
        result.Errors!["notice"].Single().Should().Be("Email já enviado recentemente para este evento.");
    }

    [Fact]
    public async Task Notices_AreClosed_ForCancelledOrPastEvents()
    {
        var past = AddEvent("Passada", DateTime.Today.AddDays(-3));
        var cancelled = AddEvent("Cancelada", _future, cancelled: true);
        AddUser("yes@rtub.test", push: true);

        (await _service.SendNoticeAsync(past, Email("new"), Admin, "x")).Status.Should().Be(EventResultStatus.Closed);
        (await _service.SendNoticeAsync(cancelled, Push("Olá"), Admin, "x")).Status.Should().Be(EventResultStatus.Closed);
        _email.VerifyNoOtherCalls();
        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Notices_RefuseAnUnknownChannelOrKind_AndAnEmptyAudience()
    {
        var id = AddEvent("Serenata", _future);

        (await _service.SendNoticeAsync(id, new EventNoticeInput("sms", null, "x", false), Admin, "x")).Errors!.Keys.Should().Contain("channel");
        (await _service.SendNoticeAsync(id, Email("weekly"), Admin, "x")).Errors!.Keys.Should().Contain("kind");
        (await _service.SendNoticeAsync(id, Email("new"), Admin, "x")).Errors!.Keys.Should().Contain("notice", "nobody is subscribed");
        _email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PushNotice_NeedsAMessage_OfAtMost500Characters()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("a@rtub.test", push: true);

        (await _service.SendNoticeAsync(id, Push(" "), Admin, "x")).Errors!.Keys.Should().Contain("message");
        (await _service.SendNoticeAsync(id, Push(new string('m', 501)), Admin, "x")).Errors!.Keys.Should().Contain("message");
        _push.Verify(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>()), Times.Never);
    }

    [Fact]
    public async Task PushNotice_GoesToSubscribedMembers_OptionallyLeitoesAndCaloirosOnly_AndIsAudited()
    {
        var id = AddEvent("Serenata", _future);
        var tuno = AddUser("t@rtub.test", push: true);
        var leitao = AddUser("l@rtub.test", push: true, category: MemberCategory.Leitao);
        AddUser("n@rtub.test", category: MemberCategory.Caloiro);

        (await _service.SendNoticeAsync(id, Push("Ensaio extra"), Admin, "https://rtub.test")).Value!.Sent.Should().Be(2);
        _push.Verify(p => p.SendToSelectedUsersAsync(It.Is<IEnumerable<string>>(ids => ids.OrderBy(x => x).SequenceEqual(new[] { tuno, leitao }.OrderBy(x => x))),
            It.Is<SendPushNotificationDto>(n => n.Title == "Serenata" && n.Body == "Ensaio extra")), Times.Once);

        (await _service.SendNoticeAsync(id, Push("Só os novos", young: true), Admin, "x")).Value!.Sent.Should().Be(1);
        _push.Verify(p => p.SendToSelectedUsersAsync(It.Is<IEnumerable<string>>(ids => ids.SequenceEqual(new[] { leitao })),
            It.IsAny<SendPushNotificationDto>()), Times.Once);

        _audit.Verify(a => a.AddAsync(It.Is<AuditLog>(l => l.Action == "PushNotificationSent" && l.EntityId == id && l.EntityType == "Event")), Times.Exactly(2));
        _email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PushNotice_ReportsPartialDelivery_AndAnAuditFailureDoesNotFailIt()
    {
        var id = AddEvent("Serenata", _future);
        AddUser("a@rtub.test", push: true);
        _push.Setup(p => p.SendToSelectedUsersAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<SendPushNotificationDto>())).ReturnsAsync((0, 1));
        _audit.Setup(a => a.AddAsync(It.IsAny<AuditLog>())).ThrowsAsync(new InvalidOperationException("db"));

        var result = await _service.SendNoticeAsync(id, Push("Olá"), Admin, "x");

        result.Status.Should().Be(EventResultStatus.Ok);
        result.Value!.Should().Be(new EventNoticeResultDto(0, 1, "Algumas notificações não foram entregues."));
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

    private async Task<Event> EventAsync(int id)
    {
        await using var db = _db.CreateContext();
        return await db.Events.AsNoTracking().SingleAsync(e => e.Id == id);
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
