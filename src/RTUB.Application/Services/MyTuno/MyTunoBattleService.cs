using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Core.Combat;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services.MyTuno;

public class MyTunoBattleService
{
    private const int WinnerXpReward = 20;
    private const int LoserXpReward = 5;
    private const decimal WinnerFidelisReward = 5m;
    private const decimal LoserFidelisReward = 1m;

    private readonly ApplicationDbContext _context;
    private readonly MyTunoCharacterService _characterService;
    private readonly MyTunoMatchmakingService _matchmakingService;
    private readonly ICombatEngine _combatEngine;

    public MyTunoBattleService(
        ApplicationDbContext context,
        MyTunoCharacterService characterService,
        MyTunoMatchmakingService matchmakingService,
        ICombatEngine combatEngine)
    {
        _context = context;
        _characterService = characterService;
        _matchmakingService = matchmakingService;
        _combatEngine = combatEngine;
    }

    public async Task<MyTunoBattle> RunAsyncBattleAsync(string userId)
    {
        var attacker = await _characterService.GetOrCreateAsync(userId);
        var defender = await _matchmakingService.FindOpponentAsync(attacker);

        if (defender == null)
        {
            throw new InvalidOperationException("Nenhum adversário disponível.");
        }

        return await RunBattleAsync(attacker, defender, MyTunoBattleMode.Async);
    }

    public async Task<MyTunoBattle> RunLiveBattleAsync(string attackerUserId, string defenderUserId)
    {
        var attacker = await _characterService.GetOrCreateAsync(attackerUserId);
        var defender = await _characterService.GetOrCreateAsync(defenderUserId);

        return await RunBattleAsync(attacker, defender, MyTunoBattleMode.Live);
    }

    private async Task<MyTunoBattle> RunBattleAsync(Character attacker, Character defender, MyTunoBattleMode mode)
    {
        var seed = RandomNumberGenerator.GetInt64(long.MinValue, long.MaxValue);
        var combatResult = _combatEngine.Simulate(
            new CombatantStats("A", attacker.Hp, attacker.Power, attacker.Speed),
            new CombatantStats("B", defender.Hp, defender.Power, defender.Speed),
            seed);

        var replay = new CombatReplay(1, seed, combatResult.Events);
        var replayJson = JsonSerializer.Serialize(replay);

        var battle = new MyTunoBattle
        {
            Mode = mode,
            Seed = seed,
            ReplayJson = replayJson,
            AttackerCharacterId = attacker.Id,
            DefenderCharacterId = defender.Id,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        var attackerUser = await _context.Users.FirstAsync(u => u.Id == attacker.UserId);
        var defenderUser = await _context.Users.FirstAsync(u => u.Id == defender.UserId);

        ApplyRewards(battle, combatResult, attacker, defender, attackerUser, defenderUser);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        _context.MyTunoBattles.Add(battle);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();

        return battle;
    }

    private static void ApplyRewards(
        MyTunoBattle battle,
        CombatResult combatResult,
        Character attacker,
        Character defender,
        ApplicationUser attackerUser,
        ApplicationUser defenderUser)
    {
        var attackerWon = combatResult.WinnerId == "A";
        var isDraw = combatResult.AttackerFinalHp == combatResult.DefenderFinalHp;
        if (isDraw)
        {
            battle.Outcome = MyTunoBattleOutcome.Draw;
        }
        else
        {
            battle.Outcome = attackerWon ? MyTunoBattleOutcome.AttackerWin : MyTunoBattleOutcome.DefenderWin;
        }

        if (isDraw)
        {
            battle.AttackerXpDelta = LoserXpReward;
            battle.DefenderXpDelta = LoserXpReward;
            battle.AttackerFidelisDelta = LoserFidelisReward;
            battle.DefenderFidelisDelta = LoserFidelisReward;
        }
        else
        {
            battle.AttackerXpDelta = attackerWon ? WinnerXpReward : LoserXpReward;
            battle.DefenderXpDelta = attackerWon ? LoserXpReward : WinnerXpReward;
            battle.AttackerFidelisDelta = attackerWon ? WinnerFidelisReward : LoserFidelisReward;
            battle.DefenderFidelisDelta = attackerWon ? LoserFidelisReward : WinnerFidelisReward;
        }

        attacker.Xp += battle.AttackerXpDelta;
        defender.Xp += battle.DefenderXpDelta;

        UpdateLevel(attacker);
        UpdateLevel(defender);

        attackerUser.FidelisBalance += battle.AttackerFidelisDelta;
        defenderUser.FidelisBalance += battle.DefenderFidelisDelta;
    }

    private static void UpdateLevel(Character character)
    {
        while (character.Xp >= character.Level * 100)
        {
            character.Level += 1;
        }
    }
}
