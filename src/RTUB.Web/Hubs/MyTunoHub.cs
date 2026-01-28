using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RTUB.Application.Services.MyTuno;

namespace RTUB.Web.Hubs;

[Authorize]
public class MyTunoHub : Hub
{
    private readonly MyTunoPresenceService _presenceService;
    private readonly MyTunoLiveChallengeService _challengeService;

    public MyTunoHub(
        MyTunoPresenceService presenceService,
        MyTunoLiveChallengeService challengeService)
    {
        _presenceService = presenceService;
        _challengeService = challengeService;
    }

    public async Task JoinMyTuno()
    {
        var userId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        _presenceService.AddConnection(userId, Context.ConnectionId);
        await Clients.Caller.SendAsync("Presence/OnlineList", _presenceService.GetOnlineUserIds());
    }

    public Task LeaveMyTuno()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _presenceService.RemoveConnection(userId, Context.ConnectionId);
        }

        return Task.CompletedTask;
    }

    public async Task SendChallenge(string targetUserId)
    {
        var userId = Context.UserIdentifier ?? string.Empty;
        var challenge = await _challengeService.SendChallengeAsync(userId, targetUserId);

        await Clients.Caller.SendAsync("Challenge/ChallengeSent", challenge.Id);
        await Clients.User(targetUserId).SendAsync("Challenge/ChallengeReceived", challenge);
    }

    public async Task AcceptChallenge(int requestId)
    {
        var userId = Context.UserIdentifier ?? string.Empty;
        var (challenge, battle) = await _challengeService.AcceptChallengeAsync(requestId, userId);

        var recipients = new[] { challenge.RequesterUserId, challenge.TargetUserId };

        await Clients.User(challenge.RequesterUserId).SendAsync("Challenge/ChallengeAccepted", challenge.Id, battle.Id);
        await Clients.Users(recipients).SendAsync("Match/MatchStarted", battle.Id, battle.Seed, battle.AttackerCharacterId, battle.DefenderCharacterId);

        var replay = battle.ReplayJson;
        await Clients.Users(recipients).SendAsync("Match/MatchEventBatch", replay);
        await Clients.Users(recipients).SendAsync("Match/MatchFinished", battle.Id, battle.Outcome, battle.AttackerFidelisDelta, battle.DefenderFidelisDelta);
    }

    public async Task DeclineChallenge(int requestId)
    {
        var userId = Context.UserIdentifier ?? string.Empty;
        var challenge = await _challengeService.DeclineChallengeAsync(requestId, userId);

        await Clients.User(challenge.RequesterUserId).SendAsync("Challenge/ChallengeDeclined", challenge.Id);
        await Clients.Caller.SendAsync("Challenge/ChallengeDeclined", challenge.Id);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            _presenceService.RemoveConnection(userId, Context.ConnectionId);
            await _challengeService.ExpirePendingChallengesAsync(userId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
