// PendingActionsService: dismiss and answer.
using System.Security.Claims;
using FluentAssertions;
using Moq;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class PendingActionsServiceTests
{
    // ---------- dismissal ----------

    [Fact]
    public async Task Dismissing_AnAnswerOrARequiredAction_IsRefused_AndUnknownOnesAre404()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var e = AddEvent("Próxima", Today.AddDays(1));
        var service = Service(new FakeSource("required", required: true));

        (await service.DismissAsync(SignedIn(me), new(EventResponsePendingActionSource.KindCode, Key(e)))).Status.Should().Be(EventResultStatus.Forbidden);
        (await service.DismissAsync(SignedIn(me), new("fake.required", "1"))).Status.Should().Be(EventResultStatus.Forbidden);
        (await service.DismissAsync(SignedIn(me), new(EventResponsePendingActionSource.KindCode, "999"))).Status.Should().Be(EventResultStatus.NotFound);
        (await service.DismissAsync(SignedIn(me), new(null, null))).Status.Should().Be(EventResultStatus.NotFound);
        (await service.DismissAsync(SignedIn(me), new(new string('x', 65), "1"))).Status.Should().Be(EventResultStatus.NotFound);
        Dismissals().Should().BeEmpty();
    }

    [Fact]
    public async Task DismissingAnOptionalAction_HidesItForThatMemberAndPeriodOnly_AndIsIdempotent()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var other = AddUser([MemberCategory.Tuno], retired: false);
        var optional = new FakeSource("optional", required: false);
        var service = Service(optional);

        (await service.DismissAsync(SignedIn(me), new("fake.optional", "2"))).Status.Should().Be(EventResultStatus.Ok);
        (await service.DismissAsync(SignedIn(me), new("fake.optional", "2"))).Status.Should().Be(EventResultStatus.NotFound,
            "a retry finds nothing pending (the page treats it as done) and stores nothing");

        Dismissals().Should().ContainSingle(d => d.UserId == me && d.SourceKey == "2" && d.Period == "2026-W45");
        var mine = (await service.GetPanelAsync(SignedIn(me), "optional", null, 1)).Value!;
        mine.Rows.Select(r => r.SourceKey).Should().Equal("1", "3");
        (await service.GetPanelAsync(SignedIn(other), "optional", null, 1)).Value!.Total.Should().Be(3, "a dismissal is personal");

        // A new period is a new key: the prompt is back by itself.
        optional.Period = "2026-W46";
        (await service.GetPanelAsync(SignedIn(me), "optional", null, 1)).Value!.Total.Should().Be(3);
    }

    // ---------- answers (072) ----------

    [Fact]
    public async Task Answers_GoToTheDomainServices_CreateOnly_AndResolveTheRow()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        AddInstrument(me, InstrumentType.Bandolim, primary: true);
        AddInstrument(me, InstrumentType.Guitarra, primary: false);
        var upcoming = AddEvent("Serenata", Today.AddDays(2));
        var past = AddEvent("Gala", Today.AddDays(-10));
        var rehearsal = AddRehearsal(Today.AddDays(1));
        var lastWeek = AddRehearsal(Today.AddDays(-7));
        var service = Service();

        (await service.RespondAsync(SignedIn(me), new("event.response", Key(upcoming), "yes", "Guitarra", " até já "))).Status.Should().Be(EventResultStatus.Ok);
        (await service.RespondAsync(SignedIn(me), new("event.response", Key(past), "yes", null, null))).Status.Should().Be(EventResultStatus.Ok);
        (await service.RespondAsync(SignedIn(me), new("rehearsal.response", Key(rehearsal), "no", "Guitarra", "nota"))).Status.Should().Be(EventResultStatus.Ok);
        (await service.RespondAsync(SignedIn(me), new("rehearsal.response", Key(lastWeek), "yes", null, null))).Status.Should().Be(EventResultStatus.Ok);

        _enrollments.Verify(s => s.CreateEnrollmentAsync(me, upcoming, InstrumentType.Guitarra, "até já", true, "Bandolim", false, It.IsAny<CancellationToken>()));
        _enrollments.Verify(s => s.CreateEnrollmentAsync(me, past, null, null, true, null, true, It.IsAny<CancellationToken>()),
            "no instrument named = not playing, as on the event page; no push for a past event");
        _attendance.Verify(s => s.AddFirstAttendanceAsync(rehearsal, me, false, null, "nota", null, false), "a no keeps its reason, never an instrument");
        _attendance.Verify(s => s.AddFirstAttendanceAsync(lastWeek, me, true, null, null, null, true));
        _status.Verify(s => s.ReconcileMemberStatusAsync(me), Times.Once(), "only the past performance counts as activity now");
        (await service.CountPendingAsync(me)).Should().Be(0);

        // A retry, a double click or an answer given meanwhile on the module page: nothing is pending, nothing is written.
        (await service.RespondAsync(SignedIn(me), new("event.response", Key(upcoming), "no", null, null))).Status.Should().Be(EventResultStatus.NotFound);
        _enrollments.Invocations.Count.Should().Be(2);
    }

    [Fact]
    public async Task Answers_AreRefused_ForWhatIsNotPendingForTheCaller()
    {
        var me = AddUser([MemberCategory.Leitao], retired: false);
        var retired = AddUser([MemberCategory.Tuno], retired: true);
        var cancelled = AddEvent("Cancelada", Today.AddDays(3), cancelled: true);
        var lastYear = AddEvent("Ano passado", new DateTime(2026, 8, 30));
        var open = AddEvent("Aberta", Today.AddDays(3));
        var service = Service();

        (await service.RespondAsync(SignedIn(me), new("event.response", Key(cancelled), "yes", null, null))).Status.Should().Be(EventResultStatus.NotFound);
        (await service.RespondAsync(SignedIn(me), new("event.response", Key(lastYear), "yes", null, null))).Status.Should().Be(EventResultStatus.NotFound);
        (await service.RespondAsync(SignedIn(me), new("event.response", "999", "yes", null, null))).Status.Should().Be(EventResultStatus.NotFound);
        (await service.RespondAsync(SignedIn(me), new("rehearsal.response", Key(open), "yes", null, null))).Status.Should().Be(EventResultStatus.NotFound,
            "an event id is not a rehearsal");
        (await service.RespondAsync(SignedIn(retired), new("event.response", Key(open), "yes", null, null))).Status.Should().Be(EventResultStatus.NotFound,
            "a Reformado has no participation panel");
        (await service.RespondAsync(SignedIn(me), new("event.response", Key(open), "talvez", null, null))).Status.Should().Be(EventResultStatus.Invalid);
        (await service.RespondAsync(SignedIn(me), new("event.response", Key(open), "yes", "Trompete", null))).Status.Should().Be(EventResultStatus.Invalid,
            "not an instrument");
        (await service.RespondAsync(SignedIn(me), new("event.response", Key(open), "yes", null, new string('n', 1001)))).Status.Should().Be(EventResultStatus.Invalid);
        (await service.RespondAsync(new ClaimsPrincipal(new ClaimsIdentity()), new("event.response", Key(open), "yes", null, null))).Status
            .Should().Be(EventResultStatus.SignInRequired);
        _enrollments.Invocations.Should().BeEmpty();

        // A Leitão with no instrument may name any, as on the event page.
        (await service.RespondAsync(SignedIn(me), new("event.response", Key(open), "yes", "Pandeireta", null))).Status.Should().Be(EventResultStatus.Ok);
    }

    [Fact]
    public async Task AnInstrumentTheMemberDoesNotPlay_IsNotOffered()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        AddInstrument(me, InstrumentType.Bandolim, primary: true);
        var e = AddEvent("Serenata", Today.AddDays(2));

        var page = (await Service().GetPageAsync(SignedIn(me))).Value!;

        page.Instruments.Select(i => i.Value).Should().Equal("Bandolim");
        page.DefaultInstrument.Should().Be("Bandolim");
        (await Service().RespondAsync(SignedIn(me), new("event.response", Key(e), "yes", "Guitarra", null))).Status.Should().Be(EventResultStatus.Invalid);
    }

    [Fact]
    public async Task PastPrompts_StartAtTheAdmissionMonth_WhenItIsInThisFiscalYear()
    {
        var newcomer = AddUser([MemberCategory.Leitao], retired: false, leitao: (2026, 10));
        var veteran = AddUser([MemberCategory.Tuno], retired: false, leitao: (2019, 10));
        var september = AddEvent("Setembro", new DateTime(2026, 9, 20));
        var october = AddEvent("Outubro", new DateTime(2026, 10, 2));

        (await Service().GetPanelAsync(SignedIn(newcomer), "events", "past", 1)).Value!.Rows.Select(r => r.SourceKey).Should().Equal(Key(october));
        (await Service().GetPanelAsync(SignedIn(veteran), "events", "past", 1)).Value!.Rows.Select(r => r.SourceKey).Should().Equal(Key(october), Key(september));
        (await Service().RespondAsync(SignedIn(newcomer), new("event.response", Key(september), "yes", null, null))).Status.Should().Be(EventResultStatus.NotFound);
    }
}
