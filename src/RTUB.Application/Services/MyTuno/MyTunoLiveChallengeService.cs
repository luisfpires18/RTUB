using System.Linq;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services.MyTuno;

public class MyTunoLiveChallengeService
{
    private static readonly TimeSpan ChallengeTimeout = TimeSpan.FromSeconds(45);

    private readonly ApplicationDbContext _context;
    private readonly MyTunoPresenceService _presenceService;
    private readonly MyTunoBattleService _battleService;

    public MyTunoLiveChallengeService(
        ApplicationDbContext context,
        MyTunoPresenceService presenceService,
        MyTunoBattleService battleService)
    {
        _context = context;
        _presenceService = presenceService;
        _battleService = battleService;
    }

    public async Task<MyTunoChallengeRequest> SendChallengeAsync(string requesterUserId, string targetUserId)
    {
        if (!_presenceService.IsOnline(targetUserId))
        {
            throw new InvalidOperationException("O adversário não está online.");
        }

        var challenge = new MyTunoChallengeRequest
        {
            RequesterUserId = requesterUserId,
            TargetUserId = targetUserId,
            Status = MyTunoChallengeStatus.Pending,
            ExpiresAt = DateTime.UtcNow.Add(ChallengeTimeout)
        };

        _context.MyTunoChallengeRequests.Add(challenge);
        await _context.SaveChangesAsync();

        return challenge;
    }

    public async Task<(MyTunoChallengeRequest Challenge, MyTunoBattle Battle)> AcceptChallengeAsync(int requestId, string targetUserId)
    {
        var challenge = await _context.MyTunoChallengeRequests
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (challenge == null)
        {
            throw new InvalidOperationException("Pedido não encontrado.");
        }

        if (challenge.TargetUserId != targetUserId)
        {
            throw new InvalidOperationException("Pedido inválido.");
        }

        if (challenge.Status != MyTunoChallengeStatus.Pending || challenge.ExpiresAt < DateTime.UtcNow)
        {
            challenge.Status = MyTunoChallengeStatus.Expired;
            challenge.ResolvedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            throw new InvalidOperationException("Pedido expirado.");
        }

        challenge.Status = MyTunoChallengeStatus.Accepted;
        challenge.ResolvedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var battle = await _battleService.RunLiveBattleAsync(challenge.RequesterUserId, challenge.TargetUserId);

        return (challenge, battle);
    }

    public async Task<MyTunoChallengeRequest> DeclineChallengeAsync(int requestId, string targetUserId)
    {
        var challenge = await _context.MyTunoChallengeRequests
            .FirstOrDefaultAsync(r => r.Id == requestId);

        if (challenge == null)
        {
            throw new InvalidOperationException("Pedido não encontrado.");
        }

        if (challenge.TargetUserId != targetUserId)
        {
            throw new InvalidOperationException("Pedido inválido.");
        }

        challenge.Status = MyTunoChallengeStatus.Declined;
        challenge.ResolvedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return challenge;
    }

    public async Task ExpirePendingChallengesAsync(string userId)
    {
        var pending = await _context.MyTunoChallengeRequests
            .Where(r => r.TargetUserId == userId && r.Status == MyTunoChallengeStatus.Pending)
            .ToListAsync();

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var challenge in pending)
        {
            challenge.Status = MyTunoChallengeStatus.Expired;
            challenge.ResolvedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }
}
