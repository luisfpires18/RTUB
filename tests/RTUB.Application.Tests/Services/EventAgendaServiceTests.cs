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
/// The React Events area (React track 011): which events are upcoming or past and in what order,
/// what a visitor may never see, and the rules of a member's own answer. Each test gets its own
/// database. Enrollment writes go to a fake that stores the row, so no push or email is ever sent.
/// </summary>
public class EventAgendaServiceTests
{
    private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());

    private readonly DatabaseFixture _db = new();
    private readonly Mock<IEnrollmentService> _enrollments = new();
    private readonly Mock<IAuditLogService> _audit = new();
    private readonly EventAgendaService _service;
    private readonly string _memberId;
    private readonly ClaimsPrincipal _member;
    private readonly DateTime _today = DateTime.Today;

    public EventAgendaServiceTests()
    {
        _service = new EventAgendaService(new Contexts(_db), _enrollments.Object, _audit.Object, NullLogger<EventAgendaService>.Instance);
        _memberId = AddUser("Tuno");
        _member = SignedIn(_memberId);
        FakeEnrollmentWrites();
    }

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
    public async Task AMember_SeesTheDescription_TheirOwnAnswer_AndTheCounts()
    {
        var id = AddEvent("Serenata", _today.AddDays(5), description: "Levar capa");
        Enroll(id, _memberId, willAttend: true);
        Enroll(id, AddUser("Outro"), willAttend: true);
        Enroll(id, AddUser("Terceiro"), willAttend: false);
        AddRepertoire(id, _today.AddDays(5), "Balada", 1);

        var summary = (await _service.GetAgendaAsync(_member)).Upcoming.Single();
        var detail = await _service.GetEventAsync(id, _member);

        summary.Member.Should().BeEquivalentTo(new EventMemberSummaryDto("Levar capa", "going", 2, 1, 0));
        detail!.Member!.NotGoingCount.Should().Be(1);
    }

    [Fact]
    public async Task Trophies_AreForEveryone_InNameOrder()
    {
        var id = AddEvent("FITAB", _today.AddDays(-30), type: EventType.Festival);
        AddTrophy(id, "Melhor Tuna");
        AddTrophy(id, "Melhor Estandarte");

        var past = (await _service.GetAgendaAsync(Visitor)).Past.Single();

        past.Trophies.Should().Equal("Melhor Estandarte", "Melhor Tuna");
    }

    [Fact]
    public async Task Videos_AreForEveryone_InTheirStoredOrder_AndUnsafeLinksAreDropped()
    {
        var id = AddEvent("FITAB", _today.AddDays(-30));
        AddVideo(id, "Segundo", 1, "https://pub-test.r2.dev/videos/b.mp4");
        AddVideo(id, "Primeiro", 0, "https://pub-test.r2.dev/videos/a.mp4");
        AddVideo(id, "Perigoso", 2, "javascript:alert(1)");

        var detail = await _service.GetEventAsync(id, Visitor);

        detail!.Videos.Select(v => v.Title).Should().Equal("Primeiro", "Segundo");
        detail.Event.VideoCount.Should().Be(3, "the count is what is stored; the player only gets safe links");
    }

    [Theory]
    [InlineData("https://pub-test.r2.dev/images/events/a.jpg", "https://pub-test.r2.dev/images/events/a.jpg")]
    [InlineData("/images/a.jpg", "/images/a.jpg")]
    [InlineData("http://insecure.example/a.jpg", null)]
    [InlineData("//evil.example/a.jpg", null)]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("", null)]
    public async Task ImageUrl_IsHttpsOrSameSiteOnly(string stored, string? expected)
    {
        AddEvent("Com imagem", _today.AddDays(1), imageUrl: stored);

        (await _service.GetAgendaAsync(Visitor)).Upcoming.Single().ImageUrl.Should().Be(expected);
    }

    [Fact]
    public async Task Repertoire_IsGroupedByDay_InRunningOrder_ForMembersOnly()
    {
        var start = _today.AddDays(10);
        var id = AddEvent("Dois dias", start, endDate: start.AddDays(1));
        AddRepertoire(id, start.AddDays(1), "Domingo B", 2);
        AddRepertoire(id, start, "Sábado B", 2);
        AddRepertoire(id, start, "Sábado A", 1);
        AddRepertoire(id, start.AddDays(1), "Domingo A", 1);

        var detail = await _service.GetEventAsync(id, _member);

        detail!.Member!.Repertoire.Select(d => (d.Date, string.Join(",", d.Songs))).Should().Equal(
            (start.ToString("yyyy-MM-dd"), "Sábado A,Sábado B"),
            (start.AddDays(1).ToString("yyyy-MM-dd"), "Domingo A,Domingo B"));
    }

    [Fact]
    public async Task UnknownEvent_IsNull()
    {
        (await _service.GetEventAsync(999_999, _member)).Should().BeNull();
    }

    [Fact]
    public async Task UpcomingPreview_IsTheFirstUpcomingEvents_WithVisitorFieldsOnly()
    {
        AddEvent("Passada", _today.AddDays(-1));
        AddEvent("Terceira", _today.AddDays(3));
        AddEvent("Primeira", _today.AddDays(1));
        AddEvent("Segunda", _today.AddDays(2), cancelReason: "chuva");
        AddEvent("Quarta", _today.AddDays(4));

        var preview = await _service.GetUpcomingPreviewAsync(3);

        preview.Select(e => e.Name).Should().Equal("Primeira", "Segunda", "Terceira");
        preview[1].Cancelled.Should().BeTrue();
    }

    [Fact]
    public void Contracts_CarryNoUserIdNoteOrInternalField()
    {
        // A new property on these records is a new field in the browser: add it here on purpose.
        Names<EventSummaryDto>().Should().BeEquivalentTo("Id", "Name", "Date", "Time", "EndDate", "Location", "Type", "Cancelled",
            "Past", "Season", "ImageUrl", "VideoCount", "Trophies", "Member");
        Names<EventMemberSummaryDto>().Should().BeEquivalentTo("Description", "MyStatus", "GoingCount", "RepertoireCount", "DiscussionCount");
        Names<EventMemberDetailDto>().Should().BeEquivalentTo("CancellationReason", "NotGoingCount", "Repertoire");
        Names<EventVideoDto>().Should().BeEquivalentTo("Id", "Title", "Url", "MimeType");
        Names<EventEnrollmentDto>().Should().BeEquivalentTo("Event", "State", "Status", "Instrument", "Notes", "IsLeitao",
            "Instruments", "DefaultInstrument", "CanRemove");
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
        var leitao = AddUser("Leitão", MemberCategory.Leitao);
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
    public async Task ACancelledEvent_TakesNoAnswer()
    {
        var id = AddEvent("Cancelada", _today.AddDays(5), cancelReason: "chuva");

        (await _service.GetEnrollmentAsync(id, _member)).Value!.State.Should().Be("cancelled");
        (await _service.SaveEnrollmentAsync(id, new EventEnrollmentInput(true, null, null), _member)).Status
            .Should().Be(EventResultStatus.Closed);
        _enrollments.VerifyNoOtherCalls();
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
    public async Task AMember_OnlyEverSeesTheirOwnAnswer()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));
        Enroll(id, AddUser("Outro"), willAttend: true, notes: "nota de outro");

        var enrollment = (await _service.GetEnrollmentAsync(id, _member)).Value!;

        enrollment.Status.Should().BeNull();
        enrollment.Notes.Should().BeNull();
    }

    // ---------- video plays ----------

    [Fact]
    public async Task AVideoPlay_IsAudited_AndAnUnknownVideoIsNot()
    {
        var id = AddEvent("FITAB", _today.AddDays(-30));
        var video = AddVideo(id, "Atuação", 0, "https://pub-test.r2.dev/videos/a.mp4");

        (await _service.RecordVideoPlayAsync(video, Visitor)).Should().BeTrue();
        (await _service.RecordVideoPlayAsync(999_999, Visitor)).Should().BeFalse();

        _audit.Verify(a => a.AddAsync(It.Is<AuditLog>(l =>
            l.EntityType == "EventVideo" && l.EntityId == video && l.Action == "Played" && l.UserId == null
            && l.EntityDisplayName == "Atuação (FITAB)")), Times.Once);
    }

    // ---------- helpers ----------

    private static IEnumerable<string> Names<T>() => typeof(T).GetProperties().Select(p => p.Name);

    private static ClaimsPrincipal SignedIn(string userId, params string[] roles) =>
        new(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, "u" + userId[..6]) }
                .Concat(roles.Select(r => new Claim(ClaimTypes.Role, r))),
            "test"));

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

    private string AddUser(string nickname, params MemberCategory[] categories)
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
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    private int AddEvent(string name, DateTime date, DateTime? endDate = null, EventType type = EventType.Atuacao,
        string description = "", string? cancelReason = null, string? imageUrl = null)
    {
        using var db = _db.CreateContext();
        var e = Event.Create(name, date, "Bragança", type, description);
        e.EndDate = endDate;
        e.ImageUrl = imageUrl;
        if (cancelReason != null)
        {
            e.Cancel(cancelReason);
        }

        db.Events.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private int Enroll(int eventId, string userId, bool willAttend, string? notes = null, InstrumentType? instrument = null)
    {
        using var db = _db.CreateContext();
        var e = Enrollment.Create(userId, eventId);
        (e.WillAttend, e.Notes, e.Instrument) = (willAttend, notes, instrument);
        db.Enrollments.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private void AddInstrument(string userId, InstrumentType type, bool primary = false)
    {
        using var db = _db.CreateContext();
        db.MemberInstruments.Add(MemberInstrument.Create(userId, type, primary));
        db.SaveChanges();
    }

    private void AddTrophy(int eventId, string name)
    {
        using var db = _db.CreateContext();
        db.Trophies.Add(Trophy.Create(name, eventId));
        db.SaveChanges();
    }

    private int AddVideo(int eventId, string title, int order, string url)
    {
        using var db = _db.CreateContext();
        var video = EventVideo.CreateVideo(eventId, url, "video/mp4", 10, _memberId, title, order);
        db.EventVideos.Add(video);
        db.SaveChanges();
        return video.Id;
    }

    private void AddRepertoire(int eventId, DateTime day, string title, int order)
    {
        using var db = _db.CreateContext();
        var album = Album.Create("Álbum " + title, 2000);
        db.Albums.Add(album);
        db.SaveChanges();
        var song = Song.Create(title, album.Id);
        db.Songs.Add(song);
        db.SaveChanges();
        db.EventRepertoires.Add(EventRepertoire.Create(eventId, song.Id, order, day));
        db.SaveChanges();
    }

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
