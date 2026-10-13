using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Repertoire management behind the React event page (React track 012D), through the real
/// EventRepertoireService on the test database. No storage, email or push is involved.
/// </summary>
public class EventRepertoireAdminTests
{
    private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());
    private static readonly ClaimsPrincipal Member = SignedIn("Member", "member-1");
    private static readonly ClaimsPrincipal Mod = SignedIn("Mod", "mod-1");
    private static readonly ClaimsPrincipal Admin = SignedIn("Admin", "admin-1");
    private static readonly ClaimsPrincipal Owner = SignedIn("Owner", "owner-1");

    private readonly DatabaseFixture _db = new();
    private readonly EventRepertoireAdminService _service;
    private readonly EventAgendaService _agenda;
    private readonly int _album;

    public EventRepertoireAdminTests()
    {
        var contexts = new Contexts(_db);
        _service = new EventRepertoireAdminService(contexts, new EventRepertoireService(new EventRepertoireRepository(contexts)));
        _agenda = new EventAgendaService(contexts, Mock.Of<IEnrollmentService>(), Mock.Of<IEventService>(), Mock.Of<IPushNotificationFactory>(),
            Mock.Of<IPushNotificationService>(), Mock.Of<IAuditLogService>(), NullLogger<EventAgendaService>.Instance);
        _album = AddAlbum("Cancioneiro");
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
    public async Task OnlyAdminOrOwner_MayReadSearchAddRemoveReorderOrClear(string who, EventResultStatus expected)
    {
        var user = who switch { "visitor" => Visitor, "member" => Member, _ => Mod };
        var id = AddEvent(new DateTime(2030, 5, 3));
        var song = AddSong("Balada");
        var item = AddItem(id, song, new DateTime(2030, 5, 3), 1);

        (await _service.GetAsync(id, user)).Status.Should().Be(expected);
        (await _service.SearchSongsAsync(id, "", user)).Status.Should().Be(expected);
        (await _service.AddAsync(id, new EventRepertoireAddInput(AddSong("Outra"), "2030-05-03"), user)).Status.Should().Be(expected);
        (await _service.RemoveAsync(id, item, user)).Status.Should().Be(expected);
        (await _service.ReorderAsync(id, new EventRepertoireOrderInput("2030-05-03", new[] { item }), user)).Status.Should().Be(expected);
        (await _service.RemoveDayAsync(id, "2030-05-03", user)).Status.Should().Be(expected);

        (await TitlesAsync(id, "2030-05-03")).Should().Equal("Balada");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_AddAfterTheLast_RemoveAndReorder(string role)
    {
        var user = role == "Owner" ? Owner : Admin;
        var id = AddEvent(new DateTime(2030, 5, 3));
        var a = AddSong("Amores");
        var b = AddSong("Bragança");
        var c = AddSong("Capas");

        await _service.AddAsync(id, new EventRepertoireAddInput(a, "2030-05-03"), user);
        await _service.AddAsync(id, new EventRepertoireAddInput(b, "2030-05-03"), user);
        var three = (await _service.AddAsync(id, new EventRepertoireAddInput(c, "2030-05-03"), user)).Value!.Days.Single();
        three.Items.Select(i => i.Title).Should().Equal("Amores", "Bragança", "Capas");

        var ids = three.Items.Select(i => i.Id).ToList();
        var reordered = (await _service.ReorderAsync(id, new EventRepertoireOrderInput("2030-05-03", new[] { ids[2], ids[0], ids[1] }), user)).Value!;
        reordered.Days.Single().Items.Select(i => i.Title).Should().Equal("Capas", "Amores", "Bragança");

        var removed = (await _service.RemoveAsync(id, ids[0], user)).Value!;
        removed.Days.Single().Items.Select(i => i.Title).Should().Equal("Capas", "Bragança");

        (await _agenda.GetEventAsync(id, Member))!.Member!.Repertoire.Single().Songs.Should().Equal(new[] { "Capas", "Bragança" },
            "members read the same running order on the event page");
    }

    // ---------- rules ----------

    [Fact]
    public async Task ASong_GoesOncePerEvent_EvenOnAnotherDay_AsTheDatabaseRequires()
    {
        var id = AddEvent(new DateTime(2030, 5, 3), end: new DateTime(2030, 5, 4));
        var song = AddSong("Balada");

        (await _service.AddAsync(id, new EventRepertoireAddInput(song, "2030-05-03"), Admin)).Status.Should().Be(EventResultStatus.Ok);
        (await _service.AddAsync(id, new EventRepertoireAddInput(song, "2030-05-03"), Admin)).Errors!.Keys.Should().Contain("songId");
        (await _service.AddAsync(id, new EventRepertoireAddInput(song, "2030-05-04"), Admin)).Errors!.Keys.Should().Contain("songId");
        (await _service.SearchSongsAsync(id, "bal", Admin)).Value!.Should().BeEmpty("a song already in the event is not offered again");
    }

    [Fact]
    public async Task TwoSongsWithTheSameTitle_DoNotBothGoIn_AsTheOldPickerListedEachTitleOnce()
    {
        var id = AddEvent(new DateTime(2030, 5, 3));
        var first = AddSong("À Entrada do Café");
        var sameTitle = AddSong("à entrada do cafe", AddAlbum("Outro disco"));

        (await _service.SearchSongsAsync(id, "entrada", Admin)).Value!.Should().HaveCount(2, "both versions can be picked, each shown with its album");
        await _service.AddAsync(id, new EventRepertoireAddInput(first, "2030-05-03"), Admin);

        (await _service.SearchSongsAsync(id, "entrada", Admin)).Value!.Should().BeEmpty();
        (await _service.AddAsync(id, new EventRepertoireAddInput(sameTitle, "2030-05-03"), Admin)).Errors!.Keys.Should().Contain("songId");
    }

    [Fact]
    public async Task Days_AreTheEventsDays_PlusAnyDayThatAlreadyHasSongs()
    {
        var id = AddEvent(new DateTime(2030, 5, 3), end: new DateTime(2030, 5, 5));
        AddItem(id, AddSong("Fora de horas"), new DateTime(2030, 5, 7), 1);

        var days = (await _service.GetAsync(id, Admin)).Value!.Days.Select(d => d.Date);

        days.Should().Equal("2030-05-03", "2030-05-04", "2030-05-05", "2030-05-07");
        (await _service.AddAsync(id, new EventRepertoireAddInput(AddSong("Nova"), "2030-05-09"), Admin)).Errors!.Keys.Should().Contain("date");
        (await _service.AddAsync(id, new EventRepertoireAddInput(AddSong("Nova 2"), "09/05/2030"), Admin)).Errors!.Keys.Should().Contain("date");
    }

    [Fact]
    public async Task EachDay_KeepsItsOwnRunningOrder()
    {
        var id = AddEvent(new DateTime(2030, 5, 3), end: new DateTime(2030, 5, 4));
        var first = AddSong("Primeira");
        var second = AddSong("Segunda");

        await _service.AddAsync(id, new EventRepertoireAddInput(first, "2030-05-03"), Admin);
        var result = (await _service.AddAsync(id, new EventRepertoireAddInput(second, "2030-05-04"), Admin)).Value!;

        result.Days.Select(d => d.Items.Single().Title).Should().Equal("Primeira", "Segunda");
        await using var db = _db.CreateContext();
        (await db.EventRepertoires.Where(r => r.EventId == id).Select(r => r.DisplayOrder).ToListAsync()).Should().AllBeEquivalentTo(1);
    }

    [Fact]
    public async Task Reorder_RefusesAStaleOrPartialOrder()
    {
        var id = AddEvent(new DateTime(2030, 5, 3));
        var x = AddItem(id, AddSong("X"), new DateTime(2030, 5, 3), 1);
        var y = AddItem(id, AddSong("Y"), new DateTime(2030, 5, 3), 2);

        (await _service.ReorderAsync(id, new EventRepertoireOrderInput("2030-05-03", new[] { x }), Admin)).Errors!.Keys.Should().Contain("itemIds");
        (await _service.ReorderAsync(id, new EventRepertoireOrderInput("2030-05-03", new[] { x, x }), Admin)).Errors!.Keys.Should().Contain("itemIds");
        (await _service.ReorderAsync(id, new EventRepertoireOrderInput("2030-05-03", new[] { x, y, 999 }), Admin)).Errors!.Keys.Should().Contain("itemIds");
        (await TitlesAsync(id, "2030-05-03")).Should().Equal("X", "Y");
    }

    [Fact]
    public async Task ClearingTheLastDay_EmptiesTheRepertoireForMembers()
    {
        var id = AddEvent(new DateTime(2030, 5, 3));
        AddItem(id, AddSong("X"), new DateTime(2030, 5, 3), 1);
        AddItem(id, AddSong("Y"), new DateTime(2030, 5, 3), 2);

        var result = (await _service.RemoveDayAsync(id, "2030-05-03", Admin)).Value!;

        result.Days.Single().Items.Should().BeEmpty();
        (await _agenda.GetEventAsync(id, Member))!.Member!.Repertoire.Should().BeEmpty("the section has nothing left to show");
        (await _agenda.GetEventAsync(id, Member))!.Event.Member!.RepertoireCount.Should().Be(0);
    }

    [Fact]
    public async Task AnItem_IsOnlyReachedThroughItsOwnEvent()
    {
        var mine = AddEvent(new DateTime(2030, 5, 3));
        var other = AddEvent(new DateTime(2030, 6, 3));
        var theirs = AddItem(other, AddSong("Deles"), new DateTime(2030, 6, 3), 1);

        (await _service.RemoveAsync(mine, theirs, Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.GetAsync(999_999, Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.AddAsync(mine, new EventRepertoireAddInput(999_999, "2030-05-03"), Admin)).Errors!.Keys.Should().Contain("songId");
        (await TitlesAsync(other, "2030-06-03")).Should().Equal("Deles");
    }

    // ---------- the song picker ----------

    [Fact]
    public async Task Search_MatchesTitlesWithoutCaseOrAccents_PrefixesFirst_AndCarriesTheAlbum()
    {
        var id = AddEvent(new DateTime(2030, 5, 3));
        AddSong("Canção do Mar");
        AddSong("Outra canção");
        AddSong("Balada");

        var found = (await _service.SearchSongsAsync(id, "CANCAO", Admin)).Value!;

        found.Select(s => s.Title).Should().Equal("Canção do Mar", "Outra canção");
        found.Should().OnlyContain(s => s.Album == "Cancioneiro");
        (await _service.SearchSongsAsync(id, "", Admin)).Value!.Should().HaveCount(3);
    }

    [Fact]
    public async Task Search_FollowsMusicsAlbumRule_ExclusiveAlbumsOnlyForTheOwnerOrListedMembers()
    {
        var id = AddEvent(new DateTime(2030, 5, 3));
        var exclusive = AddAlbum("Só para alguns", exclusive: true);
        var hidden = AddSong("Segredo", exclusive);
        AddSong("Pública");
        AddSong("Privada", AddAlbum("Privado", isPrivate: true));

        (await _service.SearchSongsAsync(id, "", Admin)).Value!.Select(s => s.Title).Should().BeEquivalentTo("Pública", "Privada");
        (await _service.SearchSongsAsync(id, "", Owner)).Value!.Select(s => s.Title).Should().Contain("Segredo");
        (await _service.AddAsync(id, new EventRepertoireAddInput(hidden, "2030-05-03"), Admin)).Errors!.Keys.Should().Contain("songId",
            "a song the Admin cannot see in Music cannot be added by id either");

        GrantAccess(exclusive, "admin-1");
        (await _service.SearchSongsAsync(id, "", Admin)).Value!.Select(s => s.Title).Should().Contain("Segredo");
    }

    [Fact]
    public void TheRepertoireContracts_CarryNoInternalField()
    {
        typeof(EventRepertoireItemDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Title");
        typeof(EventRepertoireSongDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Title", "Album");
    }

    // ---------- helpers ----------

    private static ClaimsPrincipal SignedIn(string role, string id) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Name, "u-" + role),
            new Claim(ClaimTypes.Role, role),
        }, "test"));

    private int AddEvent(DateTime date, DateTime? end = null)
    {
        using var db = _db.CreateContext();
        var e = Event.Create("Festival", date, "Bragança", EventType.Festival, "");
        e.EndDate = end;
        db.Events.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private int AddAlbum(string title, bool isPrivate = false, bool exclusive = false)
    {
        using var db = _db.CreateContext();
        var a = Album.Create(title, 2020, isPrivate: isPrivate, isExclusive: exclusive);
        db.Albums.Add(a);
        db.SaveChanges();
        return a.Id;
    }

    private int AddSong(string title, int? album = null)
    {
        using var db = _db.CreateContext();
        var s = Song.Create(title, album ?? _album);
        db.Songs.Add(s);
        db.SaveChanges();
        return s.Id;
    }

    private void GrantAccess(int album, string userId)
    {
        using var db = _db.CreateContext();
        db.AlbumAccesses.Add(new AlbumAccess { AlbumId = album, UserId = userId });
        db.SaveChanges();
    }

    private int AddItem(int eventId, int songId, DateTime day, int order)
    {
        using var db = _db.CreateContext();
        var r = EventRepertoire.Create(eventId, songId, order, day);
        db.EventRepertoires.Add(r);
        db.SaveChanges();
        return r.Id;
    }

    private async Task<List<string>> TitlesAsync(int eventId, string day)
    {
        var date = DateTime.Parse(day);
        await using var db = _db.CreateContext();
        return await db.EventRepertoires.Where(r => r.EventId == eventId && r.RepertoireDate == date)
            .OrderBy(r => r.DisplayOrder).Select(r => r.Song!.Title).ToListAsync();
    }

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
