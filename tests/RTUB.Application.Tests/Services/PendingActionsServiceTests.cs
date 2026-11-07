using System.Security.Claims;
using FluentAssertions;
using Moq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
/// "As minhas ações pendentes" (tasks 071 / 072): who gets which panel, what counts as pending in Atuações / Ensaios
/// (unanswered, not cancelled; every upcoming one; past ones of the current fiscal year from the admission month),
/// independent paging with the last page clamped, the dismissal rules (optional only, server-decided, per member and
/// period, idempotent) and the in-page answers (routed to the domain services, create-only, validated). The domain
/// services are fakes that write the row they would write; their own rules and the real SQLite unique indexes are
/// covered by RehearsalAttendanceAdminWorkflowTests and PendingActionsApiTests.
/// Each test gets its own database and a frozen clock: Friday 6 Nov 2026, fiscal year 2026-2027.
/// </summary>
public class PendingActionsServiceTests
{
    private static readonly DateTime Today = new(2026, 11, 6);
    private readonly TimeProvider _clock = new FixedClock(new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero));
    private readonly DatabaseFixture _db = new();

    private readonly Mock<IEnrollmentService> _enrollments = new();
    private readonly Mock<IRehearsalAttendanceService> _attendance = new();
    private readonly Mock<IMemberStatusService> _status = new();

    public PendingActionsServiceTests()
    {
        _enrollments.Setup(s => s.CreateEnrollmentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<InstrumentType?>(), It.IsAny<string?>(),
                It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns((string userId, int eventId, InstrumentType? instrument, string? notes, bool willAttend, string? others, bool _, CancellationToken _) =>
            {
                using var db = _db.CreateContext();
                var x = Enrollment.Create(userId, eventId);
                (x.Instrument, x.Notes, x.WillAttend, x.OtherInstruments) = (instrument, notes, willAttend, others);
                db.Enrollments.Add(x);
                db.SaveChanges();
                return Task.FromResult(x);
            });
        _attendance.Setup(s => s.AddFirstAttendanceAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<InstrumentType?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>()))
            .Returns((int rehearsalId, string userId, bool willAttend, InstrumentType? instrument, string? notes, string? others, bool _) =>
            {
                using var db = _db.CreateContext();
                var a = RehearsalAttendance.Create(rehearsalId, userId, instrument);
                (a.WillAttend, a.Notes, a.OtherInstruments) = (willAttend, notes, others);
                db.RehearsalAttendances.Add(a);
                db.SaveChanges();
                return Task.FromResult(a);
            });
    }

    private PendingActionsService Service(params IPendingActionSource[] extra) =>
        new(new Contexts(_db), new IPendingActionSource[]
        {
            new EventResponsePendingActionSource(_enrollments.Object, _status.Object),
            new RehearsalResponsePendingActionSource(_attendance.Object),
        }.Concat(extra), _clock);

    // ---------- access ----------

    [Fact]
    public async Task ActiveLeitao_GetsBothParticipationPanels_AndTheHeader()
    {
        var me = AddUser([MemberCategory.Leitao], retired: false);

        var page = (await Service().GetPageAsync(SignedIn(me))).Value!;

        page.Status.Should().Be("active");
        page.Today.Should().Be("2026-11-06");
        page.FiscalYear.Should().Be("2026-2027");
        page.Panels.Select(p => p.Key).Should().Equal("events", "rehearsals");
        page.Panels.Should().OnlyContain(p => p.Category == "participation" && p.PageSize == PendingActionsService.PageSize);
    }

    [Fact]
    public async Task Reformado_AndAccountsWithoutCategory_GetThePage_WithoutParticipationPanels()
    {
        AddEvent("Serenata", Today.AddDays(3));
        var retired = AddUser([MemberCategory.Tuno], retired: true);
        var none = AddUser([], retired: false);

        var r = (await Service().GetPageAsync(SignedIn(retired))).Value!;
        var n = (await Service().GetPageAsync(SignedIn(none))).Value!;

        r.Status.Should().Be("retired");
        r.Panels.Should().BeEmpty("status alone grants nothing, and Reformados answer in the agenda");
        n.Status.Should().Be("none");
        n.Panels.Should().BeEmpty();
        (await Service().GetPanelAsync(SignedIn(retired), "events", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
    }

    [Fact]
    public async Task Visitors_ExpelledAndUnknownAccounts_AreRefused()
    {
        var expelled = AddUser([MemberCategory.Tuno], retired: false, expelled: true);

        (await Service().GetPageAsync(new ClaimsPrincipal(new ClaimsIdentity()))).Status.Should().Be(EventResultStatus.SignInRequired);
        (await Service().GetPageAsync(SignedIn(expelled))).Status.Should().Be(EventResultStatus.SignInRequired);
        (await Service().GetPageAsync(SignedIn("ghost"))).Status.Should().Be(EventResultStatus.SignInRequired);
        (await Service().CountPendingAsync("ghost")).Should().Be(0);
    }

    // ---------- what is pending ----------

    [Fact]
    public async Task Events_UpcomingIsEveryUnansweredOne_PastIsThisFiscalYearOnly()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var soon = AddEvent("Amanhã", Today.AddDays(1));
        var far = AddEvent("Daqui a meio ano", Today.AddDays(200));
        var going = AddEvent("Já disse que vou", Today.AddDays(2));
        var notGoing = AddEvent("Já disse que não", Today.AddDays(4));
        AddEvent("Cancelada", Today.AddDays(5), cancelled: true);
        var today = AddEvent("Hoje", Today.AddHours(21));
        var september = AddEvent("Setembro", new DateTime(2026, 9, 1, 21, 0, 0));
        AddEvent("Ano passado", new DateTime(2026, 8, 31, 21, 0, 0));
        AddEnrollment(going, me, true);
        AddEnrollment(notGoing, me, false);

        var upcoming = (await Service().GetPanelAsync(SignedIn(me), "events", "upcoming", 1)).Value!;
        var past = (await Service().GetPanelAsync(SignedIn(me), "events", "past", 1)).Value!;

        upcoming.Total.Should().Be(3);
        upcoming.Rows.Select(r => r.SourceKey).Should().Equal(Key(today), Key(soon), Key(far));
        upcoming.Rows[0].Href.Should().Be($"/events/{today}");
        upcoming.Rows[0].Actions.Select(a => (a.Label, a.Answer)).Should().Equal(("Vou", "yes"), ("Não vou", "no"));
        past.Rows.Single().Actions.Select(a => (a.Label, a.Answer)).Should().Equal(("Fui", "yes"), ("Não fui", "no"));
        upcoming.Rows.Should().OnlyContain(r => !r.Required && !r.Dismissible);
        past.Total.Should().Be(1, "the fiscal year starts on 1 September");
        past.Rows.Single().SourceKey.Should().Be(Key(september));
        past.Filters.Select(f => (f.Key, f.Count)).Should().Equal(("upcoming", 3), ("past", 1));
    }

    [Fact]
    public async Task Rehearsals_SameRules_AndAnotherMembersAnswerDoesNotResolveMine()
    {
        var me = AddUser([MemberCategory.Caloiro], retired: false);
        var other = AddUser([MemberCategory.Caloiro], retired: false);
        var next = AddRehearsal(Today.AddDays(3));
        var answered = AddRehearsal(Today.AddDays(5));
        AddRehearsal(Today.AddDays(6), cancelled: true);
        var lastWeek = AddRehearsal(Today.AddDays(-7));
        AddRehearsal(new DateTime(2026, 6, 1));
        AddAttendance(answered, me);
        AddAttendance(next, other);

        var upcoming = (await Service().GetPanelAsync(SignedIn(me), "rehearsals", "upcoming", 1)).Value!;
        var past = (await Service().GetPanelAsync(SignedIn(me), "rehearsals", "past", 1)).Value!;

        upcoming.Rows.Select(r => r.SourceKey).Should().Equal(Key(next));
        upcoming.Rows.Single().Href.Should().Be($"/rehearsals/{next}");
        past.Rows.Select(r => r.SourceKey).Should().Equal(Key(lastWeek));
        (await Service().CountPendingAsync(me)).Should().Be(2);
        (await Service().GetPanelAsync(SignedIn(other), "rehearsals", "upcoming", 1)).Value!.Rows.Select(r => r.SourceKey)
            .Should().Equal([Key(answered)], "each member's own answers only");
    }

    // ---------- paging ----------

    [Fact]
    public async Task Panels_PageThreeRowsAtATime_IndependentlyAndClamped()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var ids = Enumerable.Range(1, 7).Select(i => AddEvent($"E{i}", Today.AddDays(i))).ToList();
        AddRehearsal(Today.AddDays(1));
        var service = Service();

        var page1 = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 1)).Value!;
        var page3 = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 3)).Value!;
        var beyond = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 99)).Value!;
        var rehearsals = (await service.GetPanelAsync(SignedIn(me), "rehearsals", "upcoming", 1)).Value!;

        page1.Rows.Select(r => r.SourceKey).Should().Equal(ids.Take(3).Select(Key));
        page1.Total.Should().Be(7);
        page3.Rows.Select(r => r.SourceKey).Should().Equal(Key(ids[6]));
        beyond.Page.Should().Be(3, "a page past the end answers the last one");
        rehearsals.Total.Should().Be(1, "the other panel pages on its own");

        // The only row of the last page is answered elsewhere: asking for page 3 again answers page 2, with the new count.
        AddEnrollment(ids[6], me, true);
        var after = (await service.GetPanelAsync(SignedIn(me), "events", "upcoming", 3)).Value!;
        after.Page.Should().Be(2);
        after.Total.Should().Be(6);
        after.Rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task UnknownFilter_IsTheDefault_AndUnknownPanel_Is404()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        AddEvent("Próxima", Today.AddDays(1));

        var panel = (await Service().GetPanelAsync(SignedIn(me), "events", "everything", 0)).Value!;

        panel.Filter.Should().Be("upcoming");
        panel.Page.Should().Be(1);
        (await Service().GetPanelAsync(SignedIn(me), "inbox", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
    }

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

    // ---------- Secretaria (074) ----------

    private static DocumentationDto Year(string fiscalYear, params string[] folders) => new(
        [], fiscalYear, folders.Select(f => new DocumentFolderDto(f, f, [])).ToList(), [], 0, true);

    private Mock<IDocumentationService> Documentation(string[] previous, string[] current)
    {
        var docs = new Mock<IDocumentationService>();
        docs.Setup(d => d.GetAsync("2025-2026", It.IsAny<ClaimsPrincipal>())).ReturnsAsync(EventResult<DocumentationDto>.Ok(Year("2025-2026", previous)));
        docs.Setup(d => d.GetAsync(null, It.IsAny<ClaimsPrincipal>())).ReturnsAsync(() => EventResult<DocumentationDto>.Ok(Year("2026-2027", current)));
        docs.Setup(d => d.CreateFolderAsync(It.IsAny<DocumentFolderInput>(), It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((DocumentFolderInput i, ClaimsPrincipal _) => EventResult<DocumentFolderDto>.Ok(new DocumentFolderDto(i.Name!, i.Name!, [])));
        return docs;
    }

    [Fact]
    public async Task Secretary_SeesLastYearsMissingTopLevelFolders_AndCreatesOnlyThose()
    {
        var secretary = AddUser([MemberCategory.Tuno], retired: false, positions: [Position.Secretario]);
        var admin = AddUser([MemberCategory.Tuno], retired: false);
        AddRole(admin, "Admin");
        var docs = Documentation(["Atas", "Contas", "Logistics", "Logistics/FITAB", "Regulamentos"], ["contas"]);
        var service = Service(new SecretaryPendingActionSource(docs.Object));

        var panel = (await service.GetPanelAsync(SignedIn(secretary), "secretary", null, 1)).Value!;
        var row = panel.Rows.Single();
        row.Context.Should().Be("Em falta, como em 2025-2026: Atas, Regulamentos", "the board files folder and an existing name (any case) are left out");
        row.Dismissible.Should().BeTrue();
        row.Actions[0].Confirm.Should().Contain("Criar 2 pastas em 2026-2027: Atas, Regulamentos");
        (await service.GetPageAsync(SignedIn(admin))).Value!.Panels.Select(p => p.Key).Should().NotContain("secretary", "a role without the position");
        (await service.RespondAsync(SignedIn(admin), new("secretary.folders", "", "create", null, null))).Status.Should().Be(EventResultStatus.NotFound);

        (await service.RespondAsync(SignedIn(secretary), new("secretary.folders", "", "create", null, null))).Status.Should().Be(EventResultStatus.Ok);

        docs.Verify(d => d.CreateFolderAsync(It.Is<DocumentFolderInput>(i => i.Name == "Atas"), It.Is<ClaimsPrincipal>(p => p.FindFirst(ClaimTypes.NameIdentifier)!.Value == secretary)));
        docs.Verify(d => d.CreateFolderAsync(It.Is<DocumentFolderInput>(i => i.Name == "Regulamentos"), It.IsAny<ClaimsPrincipal>()));
        docs.Verify(d => d.CreateFolderAsync(It.IsAny<DocumentFolderInput>(), It.IsAny<ClaimsPrincipal>()), Times.Exactly(2), "only the missing ones");
    }

    [Fact]
    public async Task Secretary_AgoraNao_IsTheirOwnForThisFiscalYear_AndNothingMissingMeansNoRow()
    {
        var secretary = AddUser([MemberCategory.Tuno], retired: false, positions: [Position.Secretario]);
        var complete = Service(SecretarySource(Documentation(["Atas"], ["Atas", "Extra"]).Object));
        (await complete.GetPanelAsync(SignedIn(secretary), "secretary", null, 1)).Value!.Total.Should().Be(0);

        var service = Service(SecretarySource(Documentation(["Atas"], []).Object));
        (await service.DismissAsync(SignedIn(secretary), new("secretary.folders", ""))).Status.Should().Be(EventResultStatus.Ok);

        (await service.GetPanelAsync(SignedIn(secretary), "secretary", null, 1)).Value!.Total.Should().Be(0);
        Dismissals().Should().ContainSingle(d => d.UserId == secretary && d.ActionKind == "secretary.folders" && d.Period == "2026-2027");
        (await service.RespondAsync(SignedIn(secretary), new("secretary.folders", "", "create", null, null))).Status.Should().Be(EventResultStatus.NotFound,
            "dismissed: nothing pending");
    }

    private static SecretaryPendingActionSource SecretarySource(IDocumentationService docs) => new(docs);

    // ---------- live permissions ----------

    [Fact]
    public async Task APanelGatedByRole_FollowsTheDatabase_NotTheCookie()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var gated = new FakeSource("mods", required: true) { Role = "Mod" };
        var cookie = SignedIn(me); // no role claims at all

        (await Service(gated).GetPageAsync(cookie)).Value!.Panels.Select(p => p.Key).Should().NotContain("mods");
        AddRole(me, "Mod");
        (await Service(gated).GetPageAsync(cookie)).Value!.Panels.Select(p => p.Key).Should().Contain("mods");
        SetRetired(me, true);
        (await Service(gated).GetPageAsync(cookie)).Value!.Panels.Should().BeEmpty("087: a Reformado has no panel, whatever the role");
        SetRetired(me, false);
        (await Service(gated).GetPageAsync(cookie)).Value!.Panels.Select(p => p.Key).Should().Contain("mods");
    }

    // ---------- Galeria and Novidades (077) ----------

    [Fact]
    public async Task Gallery_ListsOverEventsOfTheYearWithoutAPhoto_AVideoDoesNotCount_AnyonesPhotoResolvesForAll()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var other = AddUser([MemberCategory.Tuno], retired: false);
        var withVideo = AddEvent("Com vídeo", Today.AddDays(-3));
        var withPhoto = AddEvent("Com foto", Today.AddDays(-4));
        var bare = AddEvent("Sem nada", Today.AddDays(-5));
        AddEvent("Cancelada", Today.AddDays(-6), cancelled: true);
        AddEvent("Ainda não foi", Today.AddDays(2));
        AddEvent("Ano passado", new DateTime(2026, 8, 30));
        AddMedia(other, withPhoto, MediaType.Image);
        AddMedia(other, withVideo, MediaType.Video);
        AddMedia(other, null, MediaType.Image); // a legacy item is never matched to an event

        var service = Service(new GalleryPendingActionSource());
        var panel = (await service.GetPanelAsync(SignedIn(me), "gallery", null, 1)).Value!;

        panel.Rows.Select(r => r.SourceKey).Should().Equal(Key(withVideo), Key(bare));
        panel.Rows[0].Actions.Single().Href.Should().Be($"/gallery?upload=1&event={withVideo}&date=2026-11-03&title=Com%20v%C3%ADdeo");
        panel.Rows[0].DismissLabel.Should().Be("Não tenho");

        AddMedia(other, bare, MediaType.Image);
        (await service.GetPanelAsync(SignedIn(me), "gallery", null, 1)).Value!.Total.Should().Be(1);
    }

    [Fact]
    public async Task Gallery_NaoTenho_IsPersonal_AndOnlyForActiveMembers()
    {
        var me = AddUser([MemberCategory.Tuno], retired: false);
        var other = AddUser([MemberCategory.Tuno], retired: false);
        var retired = AddUser([MemberCategory.Tuno], retired: true);
        var ev = AddEvent("Serenata", Today.AddDays(-2));
        var service = Service(new GalleryPendingActionSource());

        (await service.DismissAsync(SignedIn(me), new PendingActionDismissInput(GalleryPendingActionSource.KindCode, Key(ev)))).Status
            .Should().Be(EventResultStatus.Ok);

        (await service.GetPanelAsync(SignedIn(me), "gallery", null, 1)).Value!.Total.Should().Be(0);
        (await service.GetPanelAsync(SignedIn(other), "gallery", null, 1)).Value!.Total.Should().Be(1);
        (await service.GetPanelAsync(SignedIn(retired), "gallery", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
        Dismissals().Single().Period.Should().Be("2026-2027");
    }

    [Fact]
    public async Task News_OnlyForWriters_OnePromptPerWeek_ResolvedByOwnPostThisWeek_DismissedForTheWeek()
    {
        // 087: the Direção in office who may write news; a writer outside the Direção, or a Direção member who cannot write, has no panel.
        var mod = AddUser([MemberCategory.Tuno], retired: false, positions: [Position.ViceMagister]);
        AddRole(mod, "Mod");
        var admin = AddUser([MemberCategory.Tuno], retired: false, positions: [Position.Magister]);
        AddRole(admin, "Admin");
        var member = AddUser([MemberCategory.Tuno], retired: false);
        var writerOutside = AddUser([MemberCategory.Tuno], retired: false);
        AddRole(writerOutside, "Mod");
        var direcaoNotWriter = AddUser([MemberCategory.Tuno], retired: false, positions: [Position.Secretario]);
        var service = Service(new NewsPendingActionSource());

        (await service.GetPanelAsync(SignedIn(member), "news", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
        (await service.GetPanelAsync(SignedIn(writerOutside), "news", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
        (await service.GetPanelAsync(SignedIn(direcaoNotWriter), "news", null, 1)).Status.Should().Be(EventResultStatus.NotFound);
        var row = (await service.GetPanelAsync(SignedIn(mod), "news", null, 1)).Value!.Rows.Single();
        row.Actions.Single().Href.Should().Be("/news?new=1");
        row.Required.Should().BeFalse();

        AddNews(mod, new DateTime(2026, 11, 1, 22, 0, 0)); // last Sunday: another week
        (await service.GetPanelAsync(SignedIn(mod), "news", null, 1)).Value!.Total.Should().Be(1);
        AddNews(mod, new DateTime(2026, 11, 2, 9, 0, 0)); // a draft this Monday
        (await service.GetPanelAsync(SignedIn(mod), "news", null, 1)).Value!.Total.Should().Be(0, "a saved draft resolves the week");

        (await service.DismissAsync(SignedIn(admin), new PendingActionDismissInput(NewsPendingActionSource.KindCode, ""))).Status
            .Should().Be(EventResultStatus.Ok);
        (await service.GetPanelAsync(SignedIn(admin), "news", null, 1)).Value!.Total.Should().Be(0);
        Dismissals().Single().Period.Should().Be("2026-W45");
        NewsPendingActionSource.Week(new DateTime(2027, 1, 1)).Should().Be("2026-W53");
    }

    // ---------- helpers ----------

    private static string Key(int id) => id.ToString();

    private static ClaimsPrincipal SignedIn(string userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test"));

    private void AddInstrument(string userId, InstrumentType type, bool primary)
    {
        using var db = _db.CreateContext();
        db.MemberInstruments.Add(MemberInstrument.Create(userId, type, primary));
        db.SaveChanges();
    }

    private string AddUser(List<MemberCategory> categories, bool retired, bool expelled = false, (int Year, int Month)? leitao = null,
        List<Position>? positions = null)
    {
        using var db = _db.CreateContext();
        var id = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser
        {
            Id = id, UserName = id, Email = $"{id}@test.pt", FirstName = "Ana", LastName = "Silva", Nickname = "Ana",
            Categories = categories, IsRetired = retired, IsExpelled = expelled,
            YearLeitao = leitao?.Year, MonthLeitao = leitao?.Month, Positions = positions ?? [],
        });
        db.SaveChanges();
        return id;
    }

    private void AddRole(string userId, string role)
    {
        using var db = _db.CreateContext();
        var r = new IdentityRole(role) { Id = Guid.NewGuid().ToString() };
        db.Roles.Add(r);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = r.Id });
        db.SaveChanges();
    }

    private void SetRetired(string userId, bool retired)
    {
        using var db = _db.CreateContext();
        db.Users.Single(u => u.Id == userId).IsRetired = retired;
        db.SaveChanges();
    }

    private int AddEvent(string name, DateTime date, bool cancelled = false)
    {
        using var db = _db.CreateContext();
        var e = Event.Create(name, date, "Braga", Enum.GetValues<EventType>()[0]);
        e.IsCancelled = cancelled;
        db.Events.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private void AddEnrollment(int eventId, string userId, bool willAttend)
    {
        using var db = _db.CreateContext();
        var x = Enrollment.Create(userId, eventId);
        x.WillAttend = willAttend;
        db.Enrollments.Add(x);
        db.SaveChanges();
    }

    private int AddRehearsal(DateTime day, bool cancelled = false)
    {
        using var db = _db.CreateContext();
        var r = Rehearsal.Create(day, "Centro Académico");
        if (cancelled)
        {
            r.Cancel("Feriado");
        }

        db.Rehearsals.Add(r);
        db.SaveChanges();
        return r.Id;
    }

    private void AddAttendance(int rehearsalId, string userId)
    {
        using var db = _db.CreateContext();
        db.RehearsalAttendances.Add(RehearsalAttendance.Create(rehearsalId, userId));
        db.SaveChanges();
    }

    private void AddMedia(string uploader, int? eventId, MediaType type)
    {
        using var db = _db.CreateContext();
        db.GalleryMedia.Add(GalleryMedia.Create(uploader, "Foto", type, "https://example.test/x", 2026, eventId: eventId));
        db.SaveChanges();
    }

    private void AddNews(string author, DateTime createdUtc)
    {
        using var db = _db.CreateContext();
        db.NewsPosts.Add(new NewsPost { Body = "Texto", AuthorId = author, CreatedAt = createdUtc });
        db.SaveChanges();
    }

    private List<PendingActionDismissal> Dismissals()
    {
        using var db = _db.CreateContext();
        return db.PendingActionDismissals.AsNoTracking().ToList();
    }

    /// <summary>
    /// A stand-in for the optional and required panels of later phases (news prompt, treasurer report...): three actions
    /// "1".."3", the member's dismissals of the current <see cref="Period"/> filtered out in the query, like a real source.
    /// </summary>
    private sealed class FakeSource(string panel, bool required) : IPendingActionSource
    {
        public string Period { get; set; } = "2026-W45";
        public string? Role { get; init; }
        public string Panel => panel;
        public string Title => panel;
        public string Category => "responsibilities";
        public IReadOnlyList<(string Key, string Label)> Filters => [];
        private string Kind => $"fake.{panel}";

        public bool AppliesTo(PendingActionMember member) => Role is null || member.Roles.Contains(Role);

        public async Task<int> CountAsync(ApplicationDbContext db, PendingActionMember member, string? filter) => (await Open(db, member)).Count;

        public async Task<IReadOnlyList<PendingActionRowDto>> PageAsync(ApplicationDbContext db, PendingActionMember member, string? filter, int skip, int take) =>
            (await Open(db, member)).Skip(skip).Take(take).Select(Row).ToList();

        public async Task<(PendingActionRowDto Row, string Period)?> FindAsync(ApplicationDbContext db, PendingActionMember member, string kind, string sourceKey) =>
            kind == Kind && (await Open(db, member)).Contains(sourceKey) ? (Row(sourceKey), Period) : null;

        private async Task<List<string>> Open(ApplicationDbContext db, PendingActionMember member)
        {
            var dismissed = await db.PendingActionDismissals
                .Where(d => d.UserId == member.UserId && d.ActionKind == Kind && d.Period == Period)
                .Select(d => d.SourceKey).ToListAsync();
            return new[] { "1", "2", "3" }.Except(dismissed).ToList();
        }

        private PendingActionRowDto Row(string key) =>
            new(Kind, key, $"Ação {key}", null, null, null, required, !required, [new PendingActionButtonDto("Abrir", "/", true)]);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
