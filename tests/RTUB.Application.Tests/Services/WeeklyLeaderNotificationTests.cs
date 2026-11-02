using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RTUB.Application.Configuration;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Task 058: "Novo 1º Lugar no Ranking" once a week, from the all-time leaderboard, against the leader recorded last
/// time in the audit log. The first run only records; the same leader is silent; a new one is broadcast once.
/// </summary>
public sealed class WeeklyLeaderNotificationTests : IDisposable
{
    private readonly DatabaseFixture _db = new();
    private readonly Mock<IRankingService> _ranking = new();
    private readonly Mock<IPushNotificationService> _push = new();
    private readonly Mock<IPushNotificationFactory> _pushFactory = new();
    private readonly Dictionary<string, RankProgressInfo> _progress = new();
    private readonly List<ApplicationUser> _users =
    [
        new() { Id = "ana", Nickname = "Ana" },
        new() { Id = "rui", Nickname = "Rui" },
        new() { Id = "zé", Nickname = "Zé" },
    ];

    public WeeklyLeaderNotificationTests()
    {
        _ranking.Setup(r => r.GetRankProgressBatchAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new Dictionary<string, RankProgressInfo>(_progress));
        _pushFactory.Setup(f => f.CreateLeaderboardFirstPlaceNotification(It.IsAny<string>(), It.IsAny<int>()))
            .Returns((string nick, int level) => new SendPushNotificationDto { Title = "Novo 1º Lugar no Ranking!", Body = $"{nick} - Nível {level}" });
    }

    private void Xp(string id, int level, int xp) => _progress[id] = new RankProgressInfo { CurrentLevel = level, CurrentXp = xp };

    private async Task RunAsync()
    {
        var service = new WeeklyNotificationBackgroundService(NullLogger<WeeklyNotificationBackgroundService>.Instance,
            Mock.Of<IServiceScopeFactory>(), Options.Create(new WeeklyNotificationOptions()));
        await using var context = _db.CreateContext();
        await service.NotifyNewLeaderAsync(context, _ranking.Object, _push.Object, _pushFactory.Object, _users, CancellationToken.None);
    }

    private async Task<List<AuditLog>> RecordsAsync()
    {
        await using var context = _db.CreateContext();
        return await context.AuditLogs.Where(a => a.EntityType == WeeklyNotificationBackgroundService.LeaderAuditEntityType)
            .OrderBy(a => a.Id).ToListAsync();
    }

    [Fact]
    public async Task FirstRun_OnlyRecordsTheLeader()
    {
        Xp("ana", 3, 300);
        Xp("rui", 2, 150);

        await RunAsync();

        _push.VerifyNoOtherCalls();
        var record = (await RecordsAsync()).Should().ContainSingle().Subject;
        record.Action.Should().Be("Recorded");
        record.EntityDisplayName.Should().Be("Ana");
        record.UserName.Should().Be("System", "a system entry, hidden by the tracing's \"Esconder sistema\"");
    }

    [Fact]
    public async Task SameLeader_NextWeek_IsSilent()
    {
        Xp("ana", 3, 300);
        await RunAsync();
        Xp("ana", 3, 340);

        await RunAsync();

        _push.VerifyNoOtherCalls();
        (await RecordsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task NewLeader_IsBroadcastOnce_AndRecorded()
    {
        Xp("ana", 3, 300);
        await RunAsync();
        Xp("rui", 4, 520);

        await RunAsync();
        await RunAsync();

        _push.Verify(p => p.BroadcastAsync(It.Is<SendPushNotificationDto>(n => n.Body == "Rui - Nível 4")), Times.Once);
        (await RecordsAsync()).Select(r => (r.Action, r.EntityDisplayName))
            .Should().Equal(("Recorded", "Ana"), ("Changed", "Rui"));
    }

    [Fact]
    public async Task Ties_FollowTheUsersOrder_AsTheLeaderboard()
    {
        Xp("rui", 2, 150);
        Xp("zé", 2, 150);

        await RunAsync();

        (await RecordsAsync()).Single().EntityDisplayName.Should().Be("Rui");
    }

    [Fact]
    public async Task NobodyWithXp_HasNoLeader()
    {
        Xp("ana", 1, 0);

        await RunAsync();

        _push.VerifyNoOtherCalls();
        (await RecordsAsync()).Should().BeEmpty();
    }

    public void Dispose() => _db.Dispose();
}
