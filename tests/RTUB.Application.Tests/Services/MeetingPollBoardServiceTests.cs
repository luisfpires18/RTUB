using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RTUB.Application.DTOs;
using RTUB.Application.Factories;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public sealed class MeetingPollBoardServiceTests : IDisposable
{
    private readonly DatabaseFixture _database = new();
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task CvPoll_IsHiddenOutsideAudience_AndOwnerManagementDoesNotGrantVoting()
    {
        var president = Member("cv-president", position: Position.PresidenteConselhoVeteranos);
        var veteran = Member("veteran", yearTuno: 2010);
        var tuno = Member("tuno", yearTuno: 2026);
        var magister = Member("magister", position: Position.Magister);
        var owner = Member("owner");
        await SeedAsync(president, veteran, tuno, magister, owner);
        var service = Service();

        var created = await service.CreateAsync(Poll("ConselhoVeteranos"), SignedIn(president.Id));
        created.Status.Should().Be(EventResultStatus.Ok);

        var veteranBoard = (await service.GetBoardAsync(SignedIn(veteran.Id))).Value!;
        veteranBoard.Polls.Should().ContainSingle();
        veteranBoard.Polls[0].CanVote.Should().BeTrue();

        var magisterBoard = (await service.GetBoardAsync(SignedIn(magister.Id))).Value!;
        magisterBoard.Polls.Should().ContainSingle();

        var tunoBoard = (await service.GetBoardAsync(SignedIn(tuno.Id))).Value!;
        tunoBoard.Polls.Should().BeEmpty();
        (await service.VoteAsync(created.Value, new MeetingPollVoteInput(1), SignedIn(tuno.Id)))
            .Status.Should().Be(EventResultStatus.NotFound);

        var ownerBoard = (await service.GetBoardAsync(SignedIn(owner.Id, "Owner"))).Value!;
        ownerBoard.Polls.Should().ContainSingle();
        ownerBoard.Polls[0].CanManage.Should().BeTrue();
        ownerBoard.Polls[0].CanVote.Should().BeFalse();
        (await service.VoteAsync(created.Value, new MeetingPollVoteInput(1), SignedIn(owner.Id, "Owner")))
            .Status.Should().Be(EventResultStatus.Forbidden);
    }

    [Fact]
    public async Task EligibleMemberCanChangeVote_AndOnlyAggregateTotalsAreReturned()
    {
        var president = Member("cv-president", position: Position.PresidenteConselhoVeteranos);
        var veteran = Member("veteran", yearTuno: 2010);
        await SeedAsync(president, veteran);
        var service = Service();
        var created = await service.CreateAsync(Poll("ConselhoVeteranos"), SignedIn(president.Id));
        var poll = (await service.GetBoardAsync(SignedIn(veteran.Id))).Value!.Polls.Single();
        var first = poll.Options[0].Id;
        var second = poll.Options[1].Id;

        (await service.VoteAsync(created.Value, new MeetingPollVoteInput(first), SignedIn(veteran.Id)))
            .Status.Should().Be(EventResultStatus.Ok);
        (await service.VoteAsync(created.Value, new MeetingPollVoteInput(second), SignedIn(veteran.Id)))
            .Status.Should().Be(EventResultStatus.Ok);

        var updated = (await service.GetBoardAsync(SignedIn(veteran.Id))).Value!.Polls.Single();
        updated.TotalVotes.Should().Be(1);
        updated.MyOptionId.Should().Be(second);
        updated.Options.Single(option => option.Id == first).Votes.Should().Be(0);
        updated.Options.Single(option => option.Id == second).Votes.Should().Be(1);
    }

    [Fact]
    public async Task PollClosingDateAndManualCloseBothStopVoting()
    {
        var president = Member("cv-president", position: Position.PresidenteConselhoVeteranos);
        var veteran = Member("veteran", yearTuno: 2010);
        await SeedAsync(president, veteran);
        var service = Service();
        var closesAt = "2026-11-17T13:00";
        var created = await service.CreateAsync(Poll("ConselhoVeteranos", closesAt), SignedIn(president.Id));
        var poll = (await service.GetBoardAsync(SignedIn(veteran.Id))).Value!.Polls.Single();

        _clock.Set(new DateTimeOffset(2026, 11, 17, 14, 0, 0, TimeSpan.Zero));
        var expired = (await service.GetBoardAsync(SignedIn(veteran.Id))).Value!.Polls.Single();
        expired.Closed.Should().BeTrue();
        (await service.VoteAsync(created.Value, new MeetingPollVoteInput(poll.Options[0].Id), SignedIn(veteran.Id)))
            .Status.Should().Be(EventResultStatus.Closed);

        var manual = await service.CreateAsync(Poll("ConselhoVeteranos"), SignedIn(president.Id));
        (await service.CloseAsync(manual.Value, SignedIn(president.Id))).Status.Should().Be(EventResultStatus.Ok);
        (await service.VoteAsync(manual.Value, new MeetingPollVoteInput(1), SignedIn(veteran.Id)))
            .Status.Should().Be(EventResultStatus.Closed);
    }

    [Fact]
    public async Task OnlyTheBodyPresidentOrOwnerCanCreatePolls()
    {
        var president = Member("cv-president", position: Position.PresidenteConselhoVeteranos);
        var tuno = Member("tuno", yearTuno: 2026);
        await SeedAsync(president, tuno);

        (await Service().CreateAsync(Poll("ConselhoVeteranos"), SignedIn(tuno.Id)))
            .Status.Should().Be(EventResultStatus.Forbidden);
        (await Service().CreateAsync(Poll("ConselhoVeteranos"), SignedIn(president.Id)))
            .Status.Should().Be(EventResultStatus.Ok);
    }

    [Fact]
    public async Task NewCvPoll_PushesOnlyToItsAudience()
    {
        var president = Member("cv-president", position: Position.PresidenteConselhoVeteranos);
        var veteran = Member("veteran", yearTuno: 2010);
        var tuno = Member("tuno", yearTuno: 2026);
        var magister = Member("magister", position: Position.Magister);
        await SeedAsync(president, veteran, tuno, magister);

        (await Service().CreateAsync(Poll("ConselhoVeteranos"), SignedIn(president.Id))).Status.Should().Be(EventResultStatus.Ok);

        _push.Verify(push => push.SendToSelectedUsersAsync(
            It.Is<IEnumerable<string>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { "cv-president", "magister", "veteran" })),
            It.Is<SendPushNotificationDto>(dto => dto.Url == "/meetings/polls" && dto.PreferenceKey == PushPreferenceCatalog.MeetingsPolls)),
            Times.Once);
    }

    [Fact]
    public async Task NewAgPoll_PushesToEveryoneButLeitoes()
    {
        var president = Member("ag-president", position: Position.PresidenteMesaAssembleia);
        var tuno = Member("tuno", yearTuno: 2026);
        var leitao = Member("leitao");
        leitao.Categories = [MemberCategory.Leitao];
        await SeedAsync(president, tuno, leitao);

        (await Service().CreateAsync(Poll("AssembleiaGeral"), SignedIn(president.Id))).Status.Should().Be(EventResultStatus.Ok);

        _push.Verify(push => push.SendToSelectedUsersAsync(
            It.Is<IEnumerable<string>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { "ag-president", "tuno" })),
            It.IsAny<SendPushNotificationDto>()),
            Times.Once);
    }

    [Fact]
    public async Task Delete_IsForTheCreatorOrOwner_AndRemovesOptionsAndVotes()
    {
        var president = Member("cv-president", position: Position.PresidenteConselhoVeteranos);
        var veteran = Member("veteran", yearTuno: 2010);
        var tuno = Member("tuno", yearTuno: 2026);
        var owner = Member("owner");
        await SeedAsync(president, veteran, tuno, owner);
        var service = Service();

        // Created by Owner: the body's president manages it (may close) but did not create it.
        var ownerPoll = (await service.CreateAsync(Poll("ConselhoVeteranos"), SignedIn(owner.Id, "Owner"))).Value;
        var card = (await service.GetBoardAsync(SignedIn(president.Id))).Value!.Polls.Single();
        card.CanManage.Should().BeTrue();
        card.CanDelete.Should().BeFalse();
        (await service.DeleteAsync(ownerPoll, SignedIn(president.Id))).Status.Should().Be(EventResultStatus.Forbidden);
        (await service.DeleteAsync(ownerPoll, SignedIn(veteran.Id))).Status.Should().Be(EventResultStatus.Forbidden);
        (await service.DeleteAsync(ownerPoll, SignedIn(tuno.Id))).Status.Should().Be(EventResultStatus.NotFound);
        (await service.DeleteAsync(ownerPoll, SignedIn(owner.Id, "Owner"))).Status.Should().Be(EventResultStatus.Ok);

        // Created by the president: they delete it, votes included.
        var ownPoll = (await service.CreateAsync(Poll("ConselhoVeteranos"), SignedIn(president.Id))).Value;
        var own = (await service.GetBoardAsync(SignedIn(president.Id))).Value!.Polls.Single();
        own.CanDelete.Should().BeTrue();
        (await service.VoteAsync(ownPoll, new MeetingPollVoteInput(own.Options[0].Id), SignedIn(veteran.Id))).Status.Should().Be(EventResultStatus.Ok);
        (await service.DeleteAsync(ownPoll, SignedIn(president.Id))).Status.Should().Be(EventResultStatus.Ok);

        await using var db = _database.CreateContext();
        db.MeetingPolls.Should().BeEmpty();
        db.MeetingPollOptions.Should().BeEmpty();
        db.MeetingPollVotes.Should().BeEmpty();
        (await service.DeleteAsync(ownPoll, SignedIn(president.Id))).Status.Should().Be(EventResultStatus.NotFound);
    }

    [Fact]
    public async Task Delete_APollWithNoKnownCreator_IsForOwnerOnly()
    {
        var president = Member("cv-president", position: Position.PresidenteConselhoVeteranos);
        var owner = Member("owner");
        await SeedAsync(president, owner);
        int pollId;
        await using (var db = _database.CreateContext())
        {
            var poll = new MeetingPoll { Audience = MeetingPollAudience.ConselhoVeteranos, Question = "Antiga?", Options = [new() { Text = "Sim" }, new() { Text = "Não" }] };
            db.MeetingPolls.Add(poll);
            await db.SaveChangesAsync();
            pollId = poll.Id;
        }

        (await Service().DeleteAsync(pollId, SignedIn(president.Id))).Status.Should().Be(EventResultStatus.Forbidden);
        (await Service().DeleteAsync(pollId, SignedIn(owner.Id, "Owner"))).Status.Should().Be(EventResultStatus.Ok);
    }

    private readonly Mock<IPushNotificationService> _push = new();

    private IMeetingPollBoardService Service() =>
        new MeetingPollBoardService(_database.CreateContextFactory(), _clock, _push.Object, new PushNotificationFactory(),
            NullLogger<MeetingPollBoardService>.Instance);

    private async Task SeedAsync(params ApplicationUser[] users)
    {
        await using var db = _database.CreateContext();
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
    }

    private static ApplicationUser Member(string id, int? yearTuno = null, Position? position = null)
    {
        var user = new ApplicationUser
        {
            Id = id,
            UserName = id,
            Email = $"{id}@test.invalid",
            FirstName = id,
            LastName = "Test",
            Nickname = id,
            PhoneNumber = "123456789",
            YearTuno = yearTuno,
            MonthTuno = 1,
            Categories = [MemberCategory.Tuno],
        };
        if (position is { } held)
        {
            user.Positions = [held];
        }

        return user;
    }

    private static MeetingPollInput Poll(string audience, string? closesAt = null) =>
        new(audience, "Qual a melhor opção?", ["Primeira opção", "Segunda opção"], closesAt);

    private static ClaimsPrincipal SignedIn(string userId, string? role = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    public void Dispose() => _database.Dispose();

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Set(DateTimeOffset value) => _now = value;
    }
}
