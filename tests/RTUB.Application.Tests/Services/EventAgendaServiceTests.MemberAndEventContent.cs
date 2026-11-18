using FluentAssertions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

// EventAgendaServiceTests: Member And Event Content tests.
public partial class EventAgendaServiceTests
{
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

    // ---------- members-only events (#227) ----------

    [Fact]
    public async Task UpcomingPreview_LeavesOutMembersOnlyEvents()
    {
        AddEvent("Surpresa", _today.AddDays(1), isPublic: false);
        AddEvent("Pública", _today.AddDays(2));

        var preview = await _service.GetUpcomingPreviewAsync(3);

        preview.Select(e => e.Name).Should().Equal("Pública");
    }

    [Fact]
    public async Task AVideoPlay_OnAMembersOnlyEvent_IsUnknownToAVisitor()
    {
        var video = AddVideo(AddEvent("Surpresa", _today.AddDays(-3), isPublic: false), "Vídeo", 1, "https://pub-test.r2.dev/v.mp4");

        (await _service.RecordVideoPlayAsync(video, Visitor)).Should().BeFalse();
        (await _service.RecordVideoPlayAsync(video, _member)).Should().BeTrue();
    }

    [Fact]
    public void ANewEvent_IsPublic_UnlessMadePrivate()
    {
        Event.Create("Nova", _today, "Bragança", EventType.Atuacao).IsPublic.Should().BeTrue();
        new EventInput("Nova", null, null, null, "Bragança", "Atuacao", null).MembersOnly.Should().BeNull("absent: public on create, unchanged on update");
    }

    [Fact]
    public void Contracts_CarryNoUserIdNoteOrInternalField()
    {
        // A new property on these records is a new field in the browser: add it here on purpose.
        Names<EventSummaryDto>().Should().BeEquivalentTo("Id", "Name", "Date", "Time", "EndDate", "Location", "Type", "Cancelled",
            "Past", "Season", "ImageUrl", "VideoCount", "Trophies", "Member", "MembersOnly");
        Names<EventMemberSummaryDto>().Should().BeEquivalentTo("Description", "MyStatus", "GoingCount", "RepertoireCount", "DiscussionCount");
        Names<EventMemberDetailDto>().Should().BeEquivalentTo("CancellationReason", "NotGoingCount", "Repertoire", "Participants");
        Names<EventParticipantsDto>().Should().BeEquivalentTo("Going", "Leitoes", "NotGoing");
        Names<EventParticipantDto>().Should().BeEquivalentTo("Name", "FullName", "AvatarUrl", "Badge", "Instrument", "Notes");
        Names<EventVideoDto>().Should().BeEquivalentTo("Id", "Title", "Url", "MimeType");
        Names<EventEnrollmentDto>().Should().BeEquivalentTo("Event", "State", "Status", "Instrument", "Notes", "IsLeitao",
            "Instruments", "DefaultInstrument", "CanRemove");
    }

    // ---------- who is going ----------

    [Fact]
    public async Task WhoIsGoing_IsForMembersOnly_GroupedLikeTheMembersList()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));
        Enroll(id, AddUser("Bandolim", categories: new[] { MemberCategory.Tuno }), willAttend: true, instrument: InstrumentType.Bandolim, notes: "levo capa");
        Enroll(id, AddUser("Porco", categories: new[] { MemberCategory.Leitao }), willAttend: true);
        Enroll(id, AddUser("Ausente"), willAttend: false, instrument: InstrumentType.Guitarra, notes: "exame");

        var forVisitor = await _service.GetEventAsync(id, Visitor);
        var who = (await _service.GetEventAsync(id, _member))!.Member!.Participants;

        forVisitor!.Member.Should().BeNull("visitors never learn who answered");
        who.Going.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Name = "Bandolim", FullName = "First Last", Badge = "TUNO", Instrument = "Bandolim", Notes = "levo capa",
        });
        who.Leitoes.Single().Name.Should().Be("Porco");
        who.NotGoing.Single().Should().Match<EventParticipantDto>(p => p.Name == "Ausente" && p.Instrument == null && p.Notes == "exame",
            "someone not going plays nothing");
    }

    [Fact]
    public async Task WhoIsGoing_UsesTheCategoryAtTheEvent_AndShowsTheMagister()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));
        var promoted = AddUser("Promovido");
        var magister = AddUser("Chefe", positions: Position.Magister);
        Enroll(id, promoted, willAttend: true, category: MemberCategory.Leitao);
        Enroll(id, magister, willAttend: true);

        var who = (await _service.GetEventAsync(id, _member))!.Member!.Participants;

        who.Leitoes.Select(p => p.Name).Should().Equal(new[] { "Promovido" }, "a promotion does not rewrite who was a Leitão then");
        who.Going.Single().Badge.Should().Be("MAGISTER");
    }

    [Fact]
    public async Task WhoIsGoing_IsNewestAnswerFirst_WithSafeAvatarsOnly()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));
        Enroll(id, AddUser("Primeiro", imageUrl: "javascript:alert(1)"), willAttend: true, enrolledAt: DateTime.UtcNow.AddHours(-2));
        Enroll(id, AddUser("Segundo", imageUrl: "https://pub-test.r2.dev/images/a.webp"), willAttend: true, enrolledAt: DateTime.UtcNow);

        var going = (await _service.GetEventAsync(id, _member))!.Member!.Participants.Going;

        going.Select(p => p.Name).Should().Equal("Segundo", "Primeiro");
        going.Select(p => p.AvatarUrl).Should().Equal("https://pub-test.r2.dev/images/a.webp", "/images/default-avatar.webp");
    }

    // ---------- permissions ----------

    [Theory]
    [InlineData(new string[0], false, false, false)]
    [InlineData(new[] { "Member" }, false, false, false)]
    [InlineData(new[] { "Mod" }, false, true, true)]
    [InlineData(new[] { "Admin" }, true, true, true)]
    [InlineData(new[] { "Owner" }, true, true, true)]
    public void Roles_Inherit_OwnerAboveAdminAboveMod(string[] roles, bool canManage, bool canTrackContacts, bool canEdit)
    {
        var user = SignedIn(Guid.NewGuid().ToString(), roles);

        RTUB.Application.Helpers.EventsAuthorization.CanManage(user).Should().Be(canManage);
        RTUB.Application.Helpers.EventsAuthorization.CanEdit(user).Should().Be(canEdit);
        RTUB.Application.Helpers.EventsAuthorization.CanTrackContacts(user).Should().Be(canTrackContacts);
        RTUB.Application.Helpers.EventsAuthorization.CanManage(Visitor).Should().BeFalse();
    }

    [Fact]
    public async Task ALeitao_GetsTheContactsLink_ATunoDoesNot()
    {
        var id = AddEvent("Serenata", _today.AddDays(5));
        var leitao = AddUser("Leitão", categories: MemberCategory.Leitao);
        var tuno = AddUser("Tuno", categories: MemberCategory.Tuno);

        (await _service.GetEventAsync(id, SignedIn(leitao)))!.CanTrackContacts.Should().BeTrue();
        (await _service.GetEventAsync(id, SignedIn(tuno)))!.CanTrackContacts.Should().BeFalse();
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
}
