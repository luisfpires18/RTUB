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
using RTUB.Core.Helpers;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// The React Events area (React track 011): which events are upcoming or past and in what order,
/// what a visitor may never see, and the rules of a member's own answer. Each test gets its own
/// database. Enrollment writes go to a fake that stores the row, so no push or email is ever sent.
/// </summary>
public partial class EventAgendaServiceTests
{
    private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());

    private readonly DatabaseFixture _db = new();
    private readonly Mock<IEnrollmentService> _enrollments = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly EventAgendaService _service;
    private readonly string _memberId;
    private readonly ClaimsPrincipal _member;
    // One frozen instant per test, so "today" cannot move between arranging and asserting.
    private readonly TimeProvider _clock = new TestClock(DateTimeOffset.UtcNow);
    private readonly DateTime _today;

    public EventAgendaServiceTests()
    {
        _today = PortugalTime.Today(_clock);
        _service = Service(_clock);
        _memberId = AddUser("Tuno");
        _member = TestPrincipal.SignedIn(_memberId);
        FakeEnrollmentWrites();
    }

    private EventAgendaService Service(TimeProvider clock) =>
        new(new Contexts(_db), _enrollments.Object, Mock.Of<IEventService>(), Mock.Of<IPushNotificationFactory>(),
            Mock.Of<IPushNotificationService>(), _audit.Object, NullLogger<EventAgendaService>.Instance, clock);

    // ---------- agenda ----------

    [Fact]
    public async Task NoEvents_IsAnEmptyAgenda()
    {
        var agenda = await _service.GetAgendaAsync(Visitor);

        agenda.Upcoming.Should().BeEmpty();
        agenda.Past.Should().BeEmpty();
        agenda.IsMember.Should().BeFalse();
        agenda.CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task Upcoming_IsSoonestFirst_AndPast_IsNewestFirst()
    {
        AddEvent("Daqui a um mês", _today.AddDays(30));
        AddEvent("Hoje", _today.AddHours(21));
        AddEvent("Semana que vem", _today.AddDays(7));
        AddEvent("Ontem", _today.AddDays(-1));
        AddEvent("Ano passado", _today.AddYears(-1));
        AddEvent("Mês passado", _today.AddMonths(-1));

        var agenda = await _service.GetAgendaAsync(Visitor);

        agenda.Upcoming.Select(e => e.Name).Should().Equal("Hoje", "Semana que vem", "Daqui a um mês");
        agenda.Past.Select(e => e.Name).Should().Equal("Ontem", "Mês passado", "Ano passado");
        agenda.Upcoming.Should().OnlyContain(e => !e.Past);
        agenda.Past.Should().OnlyContain(e => e.Past);
    }

    [Fact]
    public async Task SameStart_IsOrderedById_SoTheOrderNeverFlips()
    {
        var first = AddEvent("A", _today.AddDays(3));
        var second = AddEvent("B", _today.AddDays(3));

        var agenda = await _service.GetAgendaAsync(Visitor);

        agenda.Upcoming.Select(e => e.Id).Should().Equal(first, second);
    }

    [Fact]
    public async Task AMultiDayEvent_StaysUpcomingUntilItsLastDay_AndCarriesItsEndDate()
    {
        AddEvent("Festival a decorrer", _today.AddDays(-2), endDate: _today);
        AddEvent("Festival acabado", _today.AddDays(-3), endDate: _today.AddDays(-1));

        var agenda = await _service.GetAgendaAsync(Visitor);

        var running = agenda.Upcoming.Should().ContainSingle().Subject;
        running.Name.Should().Be("Festival a decorrer");
        running.EndDate.Should().Be(_today.ToString("yyyy-MM-dd"));
        agenda.Past.Select(e => e.Name).Should().Equal("Festival acabado");
    }

    [Fact]
    public async Task DatesAreLocalText_TimeOnlyWhenSet_EndDateOnlyWhenLater()
    {
        AddEvent("Com hora", new DateTime(2030, 5, 3, 21, 30, 0));
        AddEvent("Dia inteiro", new DateTime(2030, 5, 4), endDate: new DateTime(2030, 5, 4, 23, 0, 0));

        var upcoming = (await _service.GetAgendaAsync(Visitor)).Upcoming;

        upcoming[0].Should().Match<EventSummaryDto>(e => e.Date == "2030-05-03" && e.Time == "21:30" && e.EndDate == null);
        upcoming[1].Should().Match<EventSummaryDto>(e => e.Time == null && e.EndDate == null,
            "an end date on the same day is not a multi-day event");
    }

    [Fact]
    public async Task Season_RunsFromSeptemberToAugust()
    {
        AddEvent("Setembro", new DateTime(2024, 9, 1));
        AddEvent("Agosto", new DateTime(2025, 8, 31));
        AddEvent("Outra época", new DateTime(2025, 9, 1));

        var past = (await _service.GetAgendaAsync(Visitor)).Past;

        past.Single(e => e.Name == "Setembro").Season.Should().Be("2024-2025");
        past.Single(e => e.Name == "Agosto").Season.Should().Be("2024-2025");
        past.Single(e => e.Name == "Outra época").Season.Should().Be("2025-2026");
    }

    [Fact]
    public async Task ACancelledEvent_IsListedAsCancelled_ButItsReasonIsForMembersOnly()
    {
        var id = AddEvent("Cancelada", _today.AddDays(5), cancelReason: "motivo interno");

        var forVisitor = await _service.GetEventAsync(id, Visitor);
        var forMember = await _service.GetEventAsync(id, _member);

        forVisitor!.Event.Cancelled.Should().BeTrue();
        forVisitor.Member.Should().BeNull();
        forMember!.Member!.CancellationReason.Should().Be("motivo interno");
    }

    [Fact]
    public async Task AVisitor_GetsNoMemberField_NorAnyCount()
    {
        var id = AddEvent("Serenata", _today.AddDays(5), description: "descrição interna");
        Enroll(id, _memberId, willAttend: true, notes: "nota privada");

        var agenda = await _service.GetAgendaAsync(Visitor);
        var detail = await _service.GetEventAsync(id, Visitor);

        agenda.Upcoming.Single().Member.Should().BeNull();
        detail!.Member.Should().BeNull();
        detail.Event.Member.Should().BeNull();
        detail.IsMember.Should().BeFalse();
    }

    [Fact]
    public async Task AVisitor_NeverGetsAMembersOnlyEvent_WhileAMemberDoes()
    {
        var surprise = AddEvent("Surpresa", _today.AddDays(1), isPublic: false);
        var pastSurprise = AddEvent("Surpresa antiga", _today.AddDays(-3), isPublic: false);
        var open = AddEvent("Pública", _today.AddDays(2));

        var visitor = await _service.GetAgendaAsync(Visitor);
        visitor.Upcoming.Select(e => e.Id).Should().Equal(open);
        visitor.Past.Should().BeEmpty();
        (await _service.GetEventAsync(surprise, Visitor)).Should().BeNull();
        (await _service.GetEventAsync(pastSurprise, Visitor)).Should().BeNull();
        (await _service.GetEventAsync(open, Visitor))!.Event.MembersOnly.Should().BeFalse();

        var member = await _service.GetAgendaAsync(_member);
        member.Upcoming.Select(e => (e.Id, e.MembersOnly)).Should().Equal((surprise, true), (open, false));
        member.Past.Select(e => e.Id).Should().Equal(pastSurprise);
        (await _service.GetEventAsync(surprise, _member))!.Event.MembersOnly.Should().BeTrue();
    }

    [Fact]
    public async Task ACancelledEvent_TakesNoAnswer()
    {
        var id = AddEvent("Cancelada", _today.AddDays(5), cancelReason: "chuva");

        (await _service.GetEnrollmentAsync(id, _member)).Value!.State.Should().Be("cancelled");
        (await _service.SaveEnrollmentAsync(id, new EventEnrollmentInput(true, null, null), _member)).Status
            .Should().Be(EventResultStatus.Closed);
        _enrollments.VerifyNoOtherCalls();
    }

    /// <summary>The fake stores exactly what the real service would, so the reread answer is real.</summary>
    private void FakeEnrollmentWrites()
    {
        _enrollments.Setup(s => s.CreateEnrollmentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<InstrumentType?>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string user, int ev, InstrumentType? instrument, string? notes, bool going, string? others, bool _, CancellationToken _) =>
            {
                using var db = _db.CreateContext();
                var e = Enrollment.Create(user, ev);
                (e.Instrument, e.Notes, e.WillAttend, e.OtherInstruments) = (instrument, notes, going, others);
                db.Enrollments.Add(e);
                db.SaveChanges();
                return e;
            });
        _enrollments.Setup(s => s.UpdateEnrollmentAsync(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<InstrumentType?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int id, bool going, InstrumentType? instrument, string? notes, string? others, CancellationToken _) =>
            {
                using var db = _db.CreateContext();
                var e = db.Enrollments.Single(x => x.Id == id);
                (e.Instrument, e.Notes, e.WillAttend, e.OtherInstruments) = (instrument, notes, going, others);
                db.SaveChanges();
                return e;
            });
        _enrollments.Setup(s => s.DeleteEnrollmentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((int id, CancellationToken _) =>
            {
                using var db = _db.CreateContext();
                db.Enrollments.Remove(db.Enrollments.Single(x => x.Id == id));
                db.SaveChanges();
                return Task.CompletedTask;
            });
    }

    private string AddUser(string nickname, string? imageUrl = null, Position? positions = null, params MemberCategory[] categories)
    {
        using var db = _db.CreateContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "u" + Guid.NewGuid().ToString("N")[..10],
            Email = "private@test.com",
            Nickname = nickname,
            FirstName = "First",
            LastName = "Last",
            Categories = categories.ToList(),
            Positions = positions is { } p ? new List<Position> { p } : new List<Position>(),
            ImageUrl = imageUrl,
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    // ---------- Portugal calendar date (hotfix) ----------

    /// <summary>00:51 on 14 Oct 2026 in Portugal (WEST, UTC+1) is still 23:51 on 13 Oct in UTC.</summary>
    private static readonly DateTimeOffset JustAfterMidnightInPortugal = new(2026, 10, 13, 23, 51, 0, TimeSpan.Zero);

    private int AddEvent(string name, DateTime date, DateTime? endDate = null, EventType type = EventType.Atuacao,
        string description = "", string? cancelReason = null, string? imageUrl = null, bool isPublic = true)
    {
        using var db = _db.CreateContext();
        var e = Event.Create(name, date, "Bragança", type, description);
        e.EndDate = endDate;
        e.ImageUrl = imageUrl;
        e.IsPublic = isPublic;
        if (cancelReason != null)
        {
            e.Cancel(cancelReason);
        }

        db.Events.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private int Enroll(int eventId, string userId, bool willAttend, string? notes = null, InstrumentType? instrument = null,
        MemberCategory? category = null, DateTime? enrolledAt = null)
    {
        using var db = _db.CreateContext();
        var e = Enrollment.Create(userId, eventId);
        (e.WillAttend, e.Notes, e.Instrument, e.CategoryAtEvent) = (willAttend, notes, instrument, category);
        e.EnrolledAt = enrolledAt ?? e.EnrolledAt;
        db.Enrollments.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
