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
/// Prize (Trophies) management behind the React Prémios modal (React track 012B), through the real
/// TrophyService on the test database. Nothing here touches storage, email or push: those are strict
/// mocks that fail the test if called.
/// </summary>
public class EventPrizesTests
{
    private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());
    private static readonly ClaimsPrincipal Member = SignedIn("Member");
    private static readonly ClaimsPrincipal Mod = SignedIn("Mod");
    private static readonly ClaimsPrincipal Admin = SignedIn("Admin");
    private static readonly ClaimsPrincipal Owner = SignedIn("Owner");

    private readonly DatabaseFixture _db = new();
    private readonly EventAdminService _service;
    private readonly EventAgendaService _agenda;
    private readonly DateTime _past = DateTime.Today.AddDays(-30);

    public EventPrizesTests()
    {
        var contexts = new Contexts(_db);
        _service = new EventAdminService(contexts, Mock.Of<IEventService>(MockBehavior.Strict), Mock.Of<IEmailNotificationService>(MockBehavior.Strict),
            Mock.Of<IPushNotificationService>(MockBehavior.Strict), Mock.Of<IPushNotificationFactory>(MockBehavior.Strict),
            Mock.Of<IAuditLogService>(MockBehavior.Strict), new TrophyService(new TrophyRepository(contexts)), NullLogger<EventAdminService>.Instance);
        _agenda = new EventAgendaService(contexts, Mock.Of<IEnrollmentService>(), Mock.Of<IEventService>(), Mock.Of<IPushNotificationFactory>(),
            Mock.Of<IPushNotificationService>(), Mock.Of<IAuditLogService>(), NullLogger<EventAgendaService>.Instance);
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
    public async Task OnlyAdminOrOwner_MayListAddRenameOrDeletePrizes(string who, EventResultStatus expected)
    {
        var user = who switch { "visitor" => Visitor, "member" => Member, _ => Mod };
        var id = AddEvent("Festival", _past, EventType.Festival);
        var prize = AddPrize(id, "1.º Prémio");

        (await _service.GetPrizesAsync(id, user)).Status.Should().Be(expected);
        (await _service.AddPrizeAsync(id, new EventPrizeInput("Novo"), user)).Status.Should().Be(expected);
        (await _service.UpdatePrizeAsync(id, prize, new EventPrizeInput("Mudado"), user)).Status.Should().Be(expected);
        (await _service.DeletePrizeAsync(id, prize, user)).Status.Should().Be(expected);

        (await NamesAsync(id)).Should().Equal("1.º Prémio");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Owner")]
    public async Task AdminOrOwner_AddRenameAndDelete_AndEachAnswerIsTheOrderedList(string role)
    {
        var user = role == "Owner" ? Owner : Admin;
        var id = AddEvent("Festival", _past, EventType.Festival);

        var added = await _service.AddPrizeAsync(id, new EventPrizeInput("  Melhor Pandeireta  "), user);
        added.Status.Should().Be(EventResultStatus.Ok);
        added.Value!.Select(p => p.Name).Should().Equal("Melhor Pandeireta");

        var both = (await _service.AddPrizeAsync(id, new EventPrizeInput("1.º Prémio"), user)).Value!;
        both.Select(p => p.Name).Should().Equal(new[] { "1.º Prémio", "Melhor Pandeireta" }, "ordered by name, as the agenda shows them");

        var pandeireta = both.Single(p => p.Name == "Melhor Pandeireta").Id;
        var renamed = (await _service.UpdatePrizeAsync(id, pandeireta, new EventPrizeInput("Melhor Estandarte"), user)).Value!;
        renamed.Select(p => p.Name).Should().Equal("1.º Prémio", "Melhor Estandarte");
        renamed.Single(p => p.Name == "Melhor Estandarte").Id.Should().Be(pandeireta, "a rename keeps the row");

        (await _service.DeletePrizeAsync(id, pandeireta, user)).Value!.Select(p => p.Name).Should().Equal("1.º Prémio");
        (await _service.GetPrizesAsync(id, user)).Value!.Should().ContainSingle();
    }

    // ---------- rules ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task APrize_NeedsAName(string? name)
    {
        var id = AddEvent("Festival", _past, EventType.Festival);
        var prize = AddPrize(id, "1.º Prémio");

        (await _service.AddPrizeAsync(id, new EventPrizeInput(name), Admin)).Errors!.Keys.Should().Contain("name");
        (await _service.UpdatePrizeAsync(id, prize, new EventPrizeInput(name), Admin)).Errors!.Keys.Should().Contain("name");
        (await NamesAsync(id)).Should().Equal("1.º Prémio");
    }

    [Fact]
    public async Task APrizeName_IsAtMost200Characters()
    {
        var id = AddEvent("Festival", _past, EventType.Festival);

        (await _service.AddPrizeAsync(id, new EventPrizeInput(new string('p', 201)), Admin)).Errors!.Keys.Should().Contain("name");
        (await _service.AddPrizeAsync(id, new EventPrizeInput(new string('p', 200)), Admin)).Status.Should().Be(EventResultStatus.Ok);
    }

    [Fact]
    public async Task NewPrizes_GoOnlyToPastFestivals_AsOnTheOldPage()
    {
        var upcomingFestival = AddEvent("Festival futuro", DateTime.Today.AddDays(10), EventType.Festival);
        var pastSerenata = AddEvent("Serenata", _past, EventType.Serenata);
        var lastDayToday = AddEvent("Festival a decorrer", DateTime.Today.AddDays(-2), EventType.Festival, endDate: DateTime.Today);

        (await _service.AddPrizeAsync(upcomingFestival, new EventPrizeInput("x"), Admin)).Status.Should().Be(EventResultStatus.Closed);
        (await _service.AddPrizeAsync(pastSerenata, new EventPrizeInput("x"), Admin)).Status.Should().Be(EventResultStatus.Closed);
        (await _service.AddPrizeAsync(lastDayToday, new EventPrizeInput("x"), Admin)).Status.Should().Be(EventResultStatus.Closed,
            "still upcoming until its last day has passed");
    }

    [Fact]
    public async Task ExistingPrizes_StayEditable_OnAnEventThatNoLongerTakesNewOnes()
    {
        var id = AddEvent("Era festival", _past, EventType.Atuacao);
        var prize = AddPrize(id, "Antigo");

        (await _service.UpdatePrizeAsync(id, prize, new EventPrizeInput("Corrigido"), Admin)).Status.Should().Be(EventResultStatus.Ok);
        (await _service.DeletePrizeAsync(id, prize, Admin)).Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task APrize_IsOnlyReachedThroughItsOwnEvent()
    {
        var mine = AddEvent("Festival A", _past, EventType.Festival);
        var other = AddEvent("Festival B", _past, EventType.Festival);
        var theirs = AddPrize(other, "Deles");

        (await _service.UpdatePrizeAsync(mine, theirs, new EventPrizeInput("Roubado"), Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.DeletePrizeAsync(mine, theirs, Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.AddPrizeAsync(999_999, new EventPrizeInput("x"), Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await _service.GetPrizesAsync(999_999, Admin)).Status.Should().Be(EventResultStatus.NotFound);
        (await NamesAsync(other)).Should().Equal("Deles");
    }

    // ---------- what the pages read ----------

    [Fact]
    public async Task TheAgenda_FollowsEveryChange_AndDeletingTheLastPrizeEmptiesIt()
    {
        var id = AddEvent("Festival", _past, EventType.Festival);
        var added = (await _service.AddPrizeAsync(id, new EventPrizeInput("Único"), Admin)).Value!.Single();

        (await PublicTrophiesAsync(id)).Should().Equal("Único");

        await _service.DeletePrizeAsync(id, added.Id, Admin);

        (await PublicTrophiesAsync(id)).Should().BeEmpty("the visitors' Prémios button and history read this list");
    }

    [Fact]
    public async Task TheEventPage_OffersPrizeManagement_OnlyToAdminOrOwner_OnAPastFestival()
    {
        var festival = AddEvent("Festival", _past, EventType.Festival);
        var serenata = AddEvent("Serenata", _past, EventType.Serenata);

        (await _agenda.GetEventAsync(festival, Owner))!.CanManagePrizes.Should().BeTrue();
        (await _agenda.GetEventAsync(festival, Admin))!.CanManagePrizes.Should().BeTrue();
        (await _agenda.GetEventAsync(festival, Mod))!.CanManagePrizes.Should().BeFalse();
        (await _agenda.GetEventAsync(festival, Member))!.CanManagePrizes.Should().BeFalse();
        (await _agenda.GetEventAsync(festival, Visitor))!.CanManagePrizes.Should().BeFalse();
        (await _agenda.GetEventAsync(serenata, Admin))!.CanManagePrizes.Should().BeFalse();
    }

    [Fact]
    public void ThePrizeContracts_CarryOnlyIdAndName()
    {
        typeof(EventPrizeDto).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Name");
        typeof(EventPrizeInput).GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Name");
    }

    // ---------- helpers ----------

    private static ClaimsPrincipal SignedIn(string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "u-" + role),
            new Claim(ClaimTypes.Role, role),
        }, "test"));

    private int AddEvent(string name, DateTime date, EventType type, DateTime? endDate = null)
    {
        using var db = _db.CreateContext();
        var e = Event.Create(name, date, "Bragança", type, "");
        e.EndDate = endDate;
        db.Events.Add(e);
        db.SaveChanges();
        return e.Id;
    }

    private int AddPrize(int eventId, string name)
    {
        using var db = _db.CreateContext();
        var t = Trophy.Create(name, eventId);
        db.Trophies.Add(t);
        db.SaveChanges();
        return t.Id;
    }

    private async Task<List<string>> NamesAsync(int eventId)
    {
        await using var db = _db.CreateContext();
        return await db.Trophies.AsNoTracking().Where(t => t.EventId == eventId).OrderBy(t => t.Name).Select(t => t.Name).ToListAsync();
    }

    private async Task<IReadOnlyList<string>> PublicTrophiesAsync(int eventId) =>
        (await _agenda.GetEventAsync(eventId, Visitor))!.Event.Trophies;

    private sealed class Contexts(DatabaseFixture db) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => db.CreateContext();
    }
}
