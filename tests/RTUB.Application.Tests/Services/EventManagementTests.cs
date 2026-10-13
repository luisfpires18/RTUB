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
/// Admin/Owner event management on the React agenda (React track 011.5): who may create, edit and
/// delete, the old form's rules, and the old delete semantics. <see cref="IEventService"/> is a fake
/// that writes to the test database; push is a recording fake, so nothing is ever sent.
/// </summary>
public class EventManagementTests
{
    private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());
    private static readonly ClaimsPrincipal Member = SignedIn("Member");
    private static readonly ClaimsPrincipal Mod = SignedIn("Mod");
    private static readonly ClaimsPrincipal Admin = SignedIn("Admin");
    private static readonly ClaimsPrincipal Owner = SignedIn("Owner");

    private readonly DatabaseFixture _db = new();
    private readonly Mock<IEventService> _events = new();
    private readonly Mock<IPushNotificationService> _push = new();
    private readonly Mock<IPushNotificationFactory> _pushFactory = new();
    private readonly EventAgendaService _service;
    private readonly DateTime _future = DateTime.Today.AddDays(30);

    public EventManagementTests()
    {
        _service = new EventAgendaService(new Contexts(_db), Mock.Of<IEnrollmentService>(), _events.Object, _pushFactory.Object,
            _push.Object, Mock.Of<IAuditLogService>(), NullLogger<EventAgendaService>.Instance);
        FakeEventWrites();
        _pushFactory.Setup(f => f.CreateEventNotification(It.IsAny<Event>(), false, It.IsAny<string>()))
            .Returns((Event e, bool _, string url) => new SendPushNotificationDto { Title = e.Name, Url = url });
    }

    // ---------- who may manage ----------

    public static TheoryData<string, EventResultStatus> Refused => new()
    {
        { "visitor", EventResultStatus.SignInRequired },
        { "member", EventResultStatus.Forbidden },
        { "mod", EventResultStatus.Forbidden },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public async Task OnlyAdminOrOwner_MayCreateEditOrDelete(string who, EventResultStatus expected)
    {
        var user = who switch { "visitor" => Visitor, "member" => Member, _ => Mod };
        var id = AddEvent("Serenata", _future);

        (await _service.CreateEventAsync(Input(), user, "https://rtub.test")).Status.Should().Be(expected);
        (await _service.UpdateEventAsync(id, Input(name: "Outro nome"), user)).Status.Should().Be(expected);
        (await _service.DeleteEventAsync(id, user)).Status.Should().Be(expected);
        (await _service.GetEventForEditAsync(id, user)).Status.Should().Be(expected);

        _events.Invocations.Where(i => i.Method.Name != nameof(IEventService.GetEventByIdAsync)).Should().BeEmpty("nothing is written for them");
        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnOwner_InheritsAdmin_AndBothGetTheTypesForTheForm()
    {
        (await _service.CreateEventAsync(Input(name: "Do Owner"), Owner, "https://rtub.test")).Status.Should().Be(EventResultStatus.Ok);
        (await _service.CreateEventAsync(Input(name: "Do Admin"), Admin, "https://rtub.test")).Status.Should().Be(EventResultStatus.Ok);

        (await _service.GetAgendaAsync(Owner)).Types.Should().HaveCount(Enum.GetValues<EventType>().Length);
        (await _service.GetAgendaAsync(Admin)).Types.Should().NotBeNull();
        (await _service.GetAgendaAsync(Member)).Types.Should().BeNull("only the people who can use the form get its choices");
        (await _service.GetAgendaAsync(Visitor)).Types.Should().BeNull();
    }

    // ---------- create ----------

    [Fact]
    public async Task Create_StoresTheEvent_AnnouncesItByPush_AndAnswersTheAgendaCard()
    {
        var result = await _service.CreateEventAsync(
            Input(name: "  Serenata nova  ", date: "2031-05-03", time: "21:30", location: "Sé", type: "Serenata", description: "Levar capa"),
            Admin, "https://rtub.test");

        result.Status.Should().Be(EventResultStatus.Ok);
        result.Value!.Should().Match<EventSummaryDto>(e =>
            e.Name == "Serenata nova" && e.Date == "2031-05-03" && e.Time == "21:30" && e.EndDate == null
            && e.Location == "Sé" && e.Type == "Serenata" && !e.Past);
        _events.Verify(s => s.CreateEventAsync("Serenata nova", new DateTime(2031, 5, 3, 21, 30, 0), "Sé", EventType.Serenata,
            "Levar capa", null, null, default), Times.Once);
        _push.Verify(p => p.BroadcastAsync(It.Is<SendPushNotificationDto>(n => n.Title == "Serenata nova" && n.Url == "https://rtub.test")), Times.Once);
    }

    [Fact]
    public async Task Create_ForSeveralDays_StartsAtMidnight_WithoutATime()
    {
        var result = await _service.CreateEventAsync(Input(date: "2031-05-03", time: "21:30", endDate: "2031-05-05"), Admin, "https://rtub.test");

        result.Value!.Time.Should().BeNull("a multi-day event has no time, as on the old form");
        result.Value.EndDate.Should().Be("2031-05-05");
        _events.Verify(s => s.CreateEventAsync(It.IsAny<string>(), new DateTime(2031, 5, 3), It.IsAny<string>(), It.IsAny<EventType>(),
            It.IsAny<string>(), new DateTime(2031, 5, 5), null, default), Times.Once);
    }

    [Fact]
    public async Task Create_StillSucceeds_WhenThePushFails()
    {
        _push.Setup(p => p.BroadcastAsync(It.IsAny<SendPushNotificationDto>())).ThrowsAsync(new InvalidOperationException("push down"));

        (await _service.CreateEventAsync(Input(), Admin, "https://rtub.test")).Status.Should().Be(EventResultStatus.Ok);
    }

    [Theory]
    [InlineData("name", "", "2031-05-03", null, "Sé", "Atuacao")]
    [InlineData("location", "Serenata", "2031-05-03", null, " ", "Atuacao")]
    [InlineData("date", "Serenata", "03/05/2031", null, "Sé", "Atuacao")]
    [InlineData("endDate", "Serenata", "2031-05-03", "2031-05-01", "Sé", "Atuacao")]
    [InlineData("type", "Serenata", "2031-05-03", null, "Sé", "Banda")]
    [InlineData("type", "Serenata", "2031-05-03", null, "Sé", "99")]
    public async Task Create_RefusesWhatTheOldFormRefused(string field, string name, string date, string? endDate, string location, string type)
    {
        var result = await _service.CreateEventAsync(Input(name: name, date: date, endDate: endDate, location: location, type: type), Admin, "x");

        result.Status.Should().Be(EventResultStatus.Invalid);
        result.Errors!.Keys.Should().Contain(field);
        _events.VerifyNoOtherCalls();
        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_RefusesTooLongTextAndABadTime()
    {
        var result = await _service.CreateEventAsync(
            Input(name: new string('n', 201), time: "25:00", description: new string('d', 2001)), Admin, "x");

        result.Errors!.Keys.Should().Contain(new[] { "name", "time", "description" });
    }

    // ---------- edit ----------

    [Fact]
    public async Task EditForm_LoadsTheStoredValues()
    {
        var single = AddEvent("Serenata", new DateTime(2031, 5, 3, 21, 30, 0), description: "nota", imageUrl: "https://pub-test.r2.dev/a.webp");
        var multi = AddEvent("Festival", new DateTime(2031, 6, 1), endDate: new DateTime(2031, 6, 3), type: EventType.Festival);

        var one = (await _service.GetEventForEditAsync(single, Owner)).Value!;
        var many = (await _service.GetEventForEditAsync(multi, Admin)).Value!;

        one.Should().BeEquivalentTo(new EventEditDto(single, "Serenata", "2031-05-03", "21:30", null, "Bragança", "Atuacao", "nota", true,
            "https://pub-test.r2.dev/a.webp"));
        many.Time.Should().BeNull();
        many.EndDate.Should().Be("2031-06-03");
        many.Type.Should().Be("Festival");
        (await _service.GetEventForEditAsync(999_999, Admin)).Status.Should().Be(EventResultStatus.NotFound);
    }

    [Fact]
    public async Task Update_ChangesTheDetails_ThroughTheExistingService()
    {
        var id = AddEvent("Serenata", _future);

        var result = await _service.UpdateEventAsync(id, Input(name: "Serenata ao luar", date: "2031-07-01", time: "22:00", location: "Castelo"), Admin);

        result.Value!.Name.Should().Be("Serenata ao luar");
        result.Value.Location.Should().Be("Castelo");
        _events.Verify(s => s.UpdateEventAsync(id, "Serenata ao luar", new DateTime(2031, 7, 1, 22, 0, 0), "Castelo", string.Empty,
            EventType.Atuacao, null, default), Times.Once);
        _push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_OfAMissingEvent_IsNotFound()
    {
        (await _service.UpdateEventAsync(999_999, Input(), Admin)).Status.Should().Be(EventResultStatus.NotFound);
    }

    // ---------- delete ----------

    [Fact]
    public async Task Delete_GoesThroughTheExistingService()
    {
        var id = AddEvent("Serenata", _future);

        (await _service.DeleteEventAsync(id, Owner)).Status.Should().Be(EventResultStatus.Ok);

        _events.Verify(s => s.DeleteEventAsync(id, default), Times.Once);
        (await _service.GetEventAsync(id, Owner)).Should().BeNull();
    }

    [Fact]
    public async Task Delete_IsRefused_WhenNerbaOrdersPointAtTheEvent()
    {
        var id = AddEvent("NERBA", _future, type: EventType.Nerba);
        using (var db = _db.CreateContext())
        {
            db.NerbaOrders.Add(new NerbaOrder { Item = "Bifanas", Stock = 1, PricePerUnit = 2, EventId = id, ReportId = 1 });
            db.SaveChanges();
        }

        (await _service.DeleteEventAsync(id, Admin)).Status.Should().Be(EventResultStatus.InUse);
        _events.Verify(s => s.DeleteEventAsync(It.IsAny<int>(), default), Times.Never);
    }

    [Fact]
    public async Task Delete_OfAMissingEvent_IsNotFound()
    {
        (await _service.DeleteEventAsync(999_999, Admin)).Status.Should().Be(EventResultStatus.NotFound);
    }

    [Fact]
    public void ManagementContracts_CarryNoInternalField()
    {
        typeof(EventEditDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Id", "Name", "Date", "Time", "EndDate", "Location", "Type", "Description", "HasImage", "ImageUrl");
        typeof(EventInput).GetProperties().Select(p => p.Name).Should().BeEquivalentTo(
            "Name", "Date", "Time", "EndDate", "Location", "Type", "Description");
    }

    // ---------- helpers ----------

    private static ClaimsPrincipal SignedIn(string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "u-" + role),
            new Claim(ClaimTypes.Role, role),
        }, "test"));

    private static EventInput Input(string name = "Serenata", string date = "2031-05-03", string? time = "19:00", string? endDate = null,
        string location = "Bragança", string type = "Atuacao", string? description = null) =>
        new(name, date, time, endDate, location, type, description);

    private int AddEvent(string name, DateTime date, DateTime? endDate = null, EventType type = EventType.Atuacao,
        string description = "", string? imageUrl = null)
    {
        using var db = _db.CreateContext();
        var e = Event.Create(name, date, "Bragança", type, description);
        e.EndDate = endDate;
        e.ImageUrl = imageUrl;
        db.Events.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    /// <summary>The fake stores what the real EventService would, so the answers read back real rows.</summary>
    private void FakeEventWrites()
    {
        _events.Setup(s => s.CreateEventAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<EventType>(),
                It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, DateTime date, string location, EventType type, string description, DateTime? end, string? _, CancellationToken _) =>
            {
                using var db = _db.CreateContext();
                var e = Event.Create(name, date, location, type, description);
                e.EndDate = end;
                db.Events.Add(e);
                db.SaveChanges();
                return e;
            });
        _events.Setup(s => s.UpdateEventAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<EventType>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .Returns((int id, string name, DateTime date, string location, string description, EventType type, DateTime? end, CancellationToken _) =>
            {
                using var db = _db.CreateContext();
                var e = db.Events.Single(x => x.Id == id);
                e.UpdateDetails(name, date, location, description, type);
                e.EndDate = end;
                db.SaveChanges();
                return Task.CompletedTask;
            });
        _events.Setup(s => s.DeleteEventAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int id, CancellationToken _) =>
            {
                using var db = _db.CreateContext();
                db.Events.Remove(db.Events.Single(x => x.Id == id));
                db.SaveChanges();
                return Task.CompletedTask;
            });
    }

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
