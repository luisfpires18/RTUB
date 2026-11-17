using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Tests.Services;

// EventAgendaServiceTests: Owner Enrollment And Timezone tests.
public partial class EventAgendaServiceTests
{
    [Fact]
    public async Task AnOwner_GetsTheManageLink_ARegularMemberDoesNot()
    {
        AddEvent("Serenata", _today.AddDays(5));

        (await _service.GetAgendaAsync(SignedIn(_memberId, "Owner"))).CanManage.Should().BeTrue();
        (await _service.GetAgendaAsync(_member)).CanManage.Should().BeFalse();
    }

    // ---------- enrollment ----------

    [Fact]
    public async Task Enrollment_NeedsASignedInMember()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));

        (await _service.GetEnrollmentAsync(id, Visitor)).Status.Should().Be(EventResultStatus.SignInRequired);
        (await _service.SaveEnrollmentAsync(id, new EventEnrollmentInput(true, null, null), Visitor)).Status
            .Should().Be(EventResultStatus.SignInRequired);
        (await _service.RemoveEnrollmentAsync(id, Visitor)).Status.Should().Be(EventResultStatus.SignInRequired);
        _enrollments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Enrollment_OfAnUnknownEvent_IsNotFound()
    {
        (await _service.GetEnrollmentAsync(999_999, _member)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.SaveEnrollmentAsync(999_999, new EventEnrollmentInput(true, null, null), _member)).Status
            .Should().Be(EventResultStatus.NotFound);
    }

    [Fact]
    public async Task AnOpenEvent_OffersTheMembersOwnInstruments_PrimaryFirst()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));
        AddInstrument(_memberId, InstrumentType.Bandolim);
        AddInstrument(_memberId, InstrumentType.Guitarra, primary: true);

        var enrollment = (await _service.GetEnrollmentAsync(id, _member)).Value!;

        enrollment.State.Should().Be("open");
        enrollment.Status.Should().BeNull();
        enrollment.IsLeitao.Should().BeFalse();
        enrollment.Instruments.Select(i => i.Value).Should().Equal("Guitarra", "Bandolim");
        enrollment.DefaultInstrument.Should().Be("Guitarra");
        enrollment.CanRemove.Should().BeFalse();
    }

    [Fact]
    public async Task GoingWithAnInstrument_CreatesTheEnrollment_WithTheOtherInstruments()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));
        AddInstrument(_memberId, InstrumentType.Guitarra, primary: true);
        AddInstrument(_memberId, InstrumentType.Bandolim);

        var result = await _service.SaveEnrollmentAsync(id, new EventEnrollmentInput(true, "Bandolim", "  chego tarde  "), _member);

        result.Status.Should().Be(EventResultStatus.Ok);
        result.Value!.Status.Should().Be("going");
        result.Value.Instrument.Should().Be("Bandolim");
        result.Value.Notes.Should().Be("chego tarde");
        _enrollments.Verify(s => s.CreateEnrollmentAsync(_memberId, id, InstrumentType.Bandolim, "chego tarde", true, "Guitarra", false, default), Times.Once);
    }

    [Fact]
    public async Task ChangingAnAnswer_UpdatesTheSameEnrollment_AndNotGoingDropsTheInstrument()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));
        AddInstrument(_memberId, InstrumentType.Guitarra, primary: true);
        var enrollment = Enroll(id, _memberId, willAttend: true, instrument: InstrumentType.Guitarra);

        var result = await _service.SaveEnrollmentAsync(id, new EventEnrollmentInput(false, "Guitarra", "doente"), _member);

        result.Value!.Status.Should().Be("notGoing");
        result.Value.Instrument.Should().BeNull();
        _enrollments.Verify(s => s.UpdateEnrollmentAsync(enrollment, false, null, "doente", null, default), Times.Once);
        _enrollments.Verify(s => s.CreateEnrollmentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<InstrumentType?>(),
            It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>(), default), Times.Never);
    }

    [Theory]
    [InlineData("Violino")]
    [InlineData("Banjo")]
    [InlineData("99")]
    public async Task AnInstrumentThePageDidNotOffer_IsRefused(string instrument)
    {
        var id = AddEvent("Serenata", _today.AddDays(5));
        AddInstrument(_memberId, InstrumentType.Guitarra, primary: true);

        var result = await _service.SaveEnrollmentAsync(id, new EventEnrollmentInput(true, instrument, null), _member);

        result.Status.Should().Be(EventResultStatus.Invalid);
        result.Errors!.Keys.Should().Contain("instrument");
        _enrollments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AMemberWithoutInstruments_GoesWithoutOne()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));

        var enrollment = (await _service.GetEnrollmentAsync(id, _member)).Value!;
        var refused = await _service.SaveEnrollmentAsync(id, new EventEnrollmentInput(true, "Guitarra", null), _member);
        var accepted = await _service.SaveEnrollmentAsync(id, new EventEnrollmentInput(true, null, null), _member);

        enrollment.Instruments.Should().BeEmpty();
        refused.Status.Should().Be(EventResultStatus.Invalid);
        accepted.Status.Should().Be(EventResultStatus.Ok);
    }

    [Fact]
    public async Task ALeitaoWithoutInstruments_MayPickAnyInstrument()
    {
        var leitao = AddUser("Leitão", categories: new[] { MemberCategory.Leitao });
        var id = AddEvent("Serenata", _today.AddDays(5));

        var enrollment = (await _service.GetEnrollmentAsync(id, SignedIn(leitao))).Value!;
        var result = await _service.SaveEnrollmentAsync(id, new EventEnrollmentInput(true, "Pandeireta", null), SignedIn(leitao));

        enrollment.IsLeitao.Should().BeTrue();
        enrollment.Instruments.Should().HaveCount(Enum.GetValues<InstrumentType>().Length);
        result.Value!.Instrument.Should().Be("Pandeireta");
    }

    [Fact]
    public async Task ANoteLongerThanTheLimit_IsRefused()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));

        var result = await _service.SaveEnrollmentAsync(id,
            new EventEnrollmentInput(true, null, new string('x', EventAgendaService.MaxNotesLength + 1)), _member);

        result.Status.Should().Be(EventResultStatus.Invalid);
        result.Errors!.Keys.Should().Contain("notes");
    }

    [Fact]
    public async Task APastEvent_TakesNoNewAnswer()
    {
        var id = AddEvent("Passada", _today.AddDays(-2));

        (await _service.GetEnrollmentAsync(id, _member)).Value!.State.Should().Be("past");
        (await _service.SaveEnrollmentAsync(id, new EventEnrollmentInput(true, null, null), _member)).Status
            .Should().Be(EventResultStatus.Closed);
        _enrollments.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task APastEvent_LetsSomeoneWhoWentWithdraw_AndNobodyElse()
    {
        var id = AddEvent("Passada", _today.AddDays(-2));
        var mine = Enroll(id, _memberId, willAttend: true);
        var other = AddUser("Faltou");
        Enroll(id, other, willAttend: false);

        (await _service.RemoveEnrollmentAsync(id, SignedIn(other))).Status.Should().Be(EventResultStatus.Closed);
        var result = await _service.RemoveEnrollmentAsync(id, _member);

        result.Status.Should().Be(EventResultStatus.Ok);
        result.Value!.Status.Should().BeNull();
        _enrollments.Verify(s => s.DeleteEnrollmentAsync(mine, default), Times.Once);
    }

    [Fact]
    public async Task AnOpenEvent_CannotBeWithdrawnFrom_OnlyAnsweredDifferently()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));
        Enroll(id, _memberId, willAttend: true);

        (await _service.RemoveEnrollmentAsync(id, _member)).Status.Should().Be(EventResultStatus.Closed);
    }

    [Fact]
    public void PortugalToday_IsThePortugueseCalendarDate_NotTheUtcOne()
    {
        PortugalTime.Today(new FixedClock(JustAfterMidnightInPortugal)).Should().Be(new DateTime(2026, 10, 14));
        // Winter (WET, UTC+0): the dates agree.
        PortugalTime.Today(new FixedClock(new DateTimeOffset(2026, 12, 31, 23, 30, 0, TimeSpan.Zero))).Should().Be(new DateTime(2026, 12, 31));
        PortugalTime.Today(new FixedClock(new DateTimeOffset(2027, 1, 1, 0, 30, 0, TimeSpan.Zero))).Should().Be(new DateTime(2027, 1, 1));
    }

    [Fact]
    public async Task JustAfterMidnightInPortugal_WhileUtcIsStillTheDayBefore_YesterdaysEventsArePast()
    {
        AddEvent("Ontem", new DateTime(2026, 10, 13, 21, 30, 0));
        AddEvent("Hoje", new DateTime(2026, 10, 14, 21, 30, 0));
        AddEvent("Termina hoje", new DateTime(2026, 10, 12), endDate: new DateTime(2026, 10, 14));
        AddEvent("Terminou ontem", new DateTime(2026, 10, 11), endDate: new DateTime(2026, 10, 13));

        var agenda = await Service(new FixedClock(JustAfterMidnightInPortugal)).GetAgendaAsync(Visitor);

        agenda.Upcoming.Select(e => e.Name).Should().Equal("Termina hoje", "Hoje");
        agenda.Upcoming.Should().OnlyContain(e => !e.Past);
        agenda.Past.Select(e => e.Name).Should().Equal("Ontem", "Terminou ontem");
        agenda.Past.Should().OnlyContain(e => e.Past);
    }

    [Fact]
    public async Task LateEveningInPortugal_TodaysEventStaysUpcomingForTheWholePortugueseDay()
    {
        AddEvent("Hoje", new DateTime(2026, 10, 14, 10, 0, 0));
        AddEvent("Termina hoje", new DateTime(2026, 10, 12), endDate: new DateTime(2026, 10, 14));

        // 23:59 on 14 Oct in Portugal = 22:59 UTC, well after the event's start time.
        var agenda = await Service(new FixedClock(new DateTimeOffset(2026, 10, 14, 22, 59, 0, TimeSpan.Zero))).GetAgendaAsync(Visitor);

        agenda.Upcoming.Select(e => e.Name).Should().Equal("Termina hoje", "Hoje");
        agenda.Past.Should().BeEmpty();
    }

    private void AddInstrument(string userId, InstrumentType type, bool primary = false)
    {
        using var db = _db.CreateContext();
        db.MemberInstruments.Add(MemberInstrument.Create(userId, type, primary));
        db.SaveChanges();
    }
}
