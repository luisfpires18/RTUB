using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Data;
using RTUB.Application.Interfaces.AfterHours;
using RTUB.Core.Entities;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using Xunit;

namespace RTUB.Integration.Tests.Application;

/// <summary>
/// AH-006 PvP against the real SQLite schema. Frozen clock and scripted dice: an unscripted roll is 1,
/// which gives both sides the same −2 factor. Attacks use Negotiation, neutral against the default
/// Counterattack defence, so equal players tie every round and the attacker wins the tie-break (1 ≤ 50). Players are back-dated past new-player protection unless a test says otherwise.
/// </summary>
public class AfterHoursPvpTests : IClassFixture<AfterHoursPlayFactory>
{
    private readonly AfterHoursPlayFactory _factory;
    private static DateTime Start => AfterHoursPlayFactory.Start;

    public AfterHoursPvpTests(AfterHoursPlayFactory factory)
    {
        _factory = factory;
        factory.Dice.Reset();
        factory.Now = Start;
        factory.EnsureActiveCycleAsync().GetAwaiter().GetResult();
    }

    // ------------------------------------------------------------------ accepted attack

    [Fact]
    public async Task Attack_CommitsBattleStateAndReceiptTogether()
    {
        var (attacker, _) = await PlayerAsync();
        var (defender, defenderId) = await PlayerAsync(bank: 5_000);

        var result = await Attack(attacker, defenderId, "a1");

        result.Accepted.Should().BeTrue();
        var battle = await BattleAsync(result.Receipt!.PvpBattle!.Id);
        battle.ReceiptId.Should().Be(result.Receipt.Id);
        battle.Rounds.Should().HaveCount(3);
        (battle.AttackerWon, battle.TieBreakRoll, battle.LootMultiplier, battle.WalletStolen).Should().Be((true, (int?)1, 1m, 40L));

        var a = await StateAsync(attacker);
        var d = await StateAsync(defender);
        (a.Energy, a.WalletCash, a.PvpCooldownUntilUtc, a.PvpInitiatedAtUtc).Should().Be((220, 440L, Start.AddMinutes(5), (DateTime?)Start));
        (d.WalletCash, d.BankCash, d.PvpProtectedUntilUtc, d.PvpRecoveryUntilUtc).Should().Be((360L, 5_000L, Start.AddHours(6), Start.AddMinutes(15)));
    }

    [Fact]
    public async Task Attack_MovesCargoRows_AndRecordsThem()
    {
        var (attacker, _) = await PlayerAsync();
        var (defender, defenderId) = await PlayerAsync();
        await GiveCargoAsync(defender, CargoType.ArtPiece, 10); // 800 → budget 160 → 2 art pieces

        var battleId = (await Attack(attacker, defenderId, "cargo")).Receipt!.PvpBattle!.Id;

        (await CargoAsync(defender, CargoType.ArtPiece)).Should().Be(8);
        (await CargoAsync(attacker, CargoType.ArtPiece)).Should().Be(2);
        (await BattleAsync(battleId)).Cargo.Select(c => (c.CargoType, c.Quantity)).Should().Equal((CargoType.ArtPiece, 2));
    }

    // ------------------------------------------------------------------ idempotency

    [Fact]
    public async Task Replay_ReturnsTheSameBattle_WithoutSpendingRollingOrExtending()
    {
        var (attacker, _) = await PlayerAsync();
        var (defender, defenderId) = await PlayerAsync();

        var first = await Attack(attacker, defenderId, "same");
        var rolls = _factory.Dice.Calls;
        var before = (await StateAsync(attacker), await StateAsync(defender));

        _factory.Now = Start.AddMinutes(2);
        try
        {
            var replay = await Attack(attacker, defenderId, "same");

            replay.Replayed.Should().BeTrue();
            replay.Receipt!.PvpBattle!.Id.Should().Be(first.Receipt!.PvpBattle!.Id);
            _factory.Dice.Calls.Should().Be(rolls, "a replay never rolls");
            var after = (await StateAsync(attacker), await StateAsync(defender));
            (after.Item1.WalletCash, after.Item1.PvpCooldownUntilUtc, after.Item1.EnergyUpdatedAtUtc)
                .Should().Be((before.Item1.WalletCash, before.Item1.PvpCooldownUntilUtc, before.Item1.EnergyUpdatedAtUtc));
            (after.Item2.WalletCash, after.Item2.PvpProtectedUntilUtc).Should().Be((before.Item2.WalletCash, before.Item2.PvpProtectedUntilUtc));
            await using var db = await DbAsync();
            (await db.AfterHoursPvpBattles.CountAsync(b => b.AttackerUserId == attacker)).Should().Be(1);
        }
        finally
        {
            _factory.Now = Start;
        }
    }

    [Fact]
    public async Task Replay_WithAChangedRequest_IsKeyMisuse()
    {
        var (attacker, _) = await PlayerAsync();
        var (_, defenderId) = await PlayerAsync();
        var (_, otherId) = await PlayerAsync();
        await Attack(attacker, defenderId, "k");

        var actions = Actions();
        (await actions.AttackAsync(attacker, defenderId, PvpTactic.Setup, RiskStance.Standard, null, null, null, "k")).Error.Should().Contain("already used");
        (await actions.AttackAsync(attacker, defenderId, PvpTactic.Ambush, RiskStance.Reckless, null, null, null, "k")).Error.Should().Contain("already used");
        (await actions.AttackAsync(attacker, otherId, PvpTactic.Ambush, RiskStance.Standard, null, null, null, "k")).Error.Should().Contain("already used");
    }

    // ------------------------------------------------------------------ restrictions

    [Fact]
    public async Task NewPlayers_AreProtected_UntilTheyAttack()
    {
        var (veteran, _) = await PlayerAsync();
        var (newbie, newbieId) = await PlayerAsync(backdate: false);
        var (_, targetId) = await PlayerAsync();

        (await Attack(veteran, newbieId, "n1")).Error.Should().Be("That player is new and still protected.");

        await UpdateAsync(newbie, s => s.Energy = 5);
        (await Attack(newbie, targetId, "n2")).Error.Should().Be("Not enough energy.");
        (await StateAsync(newbie)).PvpInitiatedAtUtc.Should().BeNull("a refused attack keeps the protection");

        await UpdateAsync(newbie, s => s.Energy = 240);
        (await Attack(newbie, targetId, "n3")).Accepted.Should().BeTrue();
        (await StateAsync(newbie)).PvpInitiatedAtUtc.Should().Be(Start);

        _factory.Now = Start.AddHours(7); // past cooldowns and the target's 6 h
        try
        {
            (await Attack(veteran, newbieId, "n4")).Accepted.Should().BeTrue("the first attack ended the newbie's protection");
        }
        finally
        {
            _factory.Now = Start;
        }
    }

    [Fact]
    public async Task SameTarget_IsLimitedTo_OnceIn24Hours()
    {
        var (attacker, _) = await PlayerAsync();
        var (_, defenderId) = await PlayerAsync(skills: 12); // wins, so no 6 h protection

        (await Attack(attacker, defenderId, "s1")).Receipt!.PvpBattle!.AttackerWon.Should().BeFalse();

        try
        {
            _factory.Now = Start.AddHours(1); // past cooldown and recovery
            (await Attack(attacker, defenderId, "s2")).Error.Should().StartWith("You already attacked that player");
            _factory.Now = Start.AddHours(24);
            (await Attack(attacker, defenderId, "s3")).Accepted.Should().BeTrue();
        }
        finally
        {
            _factory.Now = Start;
        }
    }

    [Fact]
    public async Task UnknownTarget_IsRefused()
    {
        var (attacker, attackerId) = await PlayerAsync();

        (await Attack(attacker, 987_654, "u1")).Error.Should().Be("That player is not in this cycle.");
        (await Attack(attacker, attackerId, "u2")).Error.Should().Be("You can't attack yourself.");
    }

    // ------------------------------------------------------------------ concurrency

    [Fact]
    public async Task OneAttacker_ManyTargets_AtOnce_OneAcceptedByCooldown()
    {
        var (attacker, _) = await PlayerAsync();
        var targets = new List<int>();
        for (var i = 0; i < 8; i++) targets.Add((await PlayerAsync()).StateId);

        var results = await Parallel(8, i => Attack(attacker, targets[i], $"many{i}"));

        results.Count(r => r.Accepted).Should().Be(1);
        results.Where(r => !r.Accepted).Should().OnlyContain(r => r.Error!.StartsWith("You just fought"));
        (await StateAsync(attacker)).Energy.Should().Be(220);
    }

    [Fact]
    public async Task ManyAttackers_OneDefender_AtOnce_OneWinsAndProtectionBlocksTheRest()
    {
        var (defender, defenderId) = await PlayerAsync(wallet: 1_000);
        await GiveCargoAsync(defender, CargoType.Electronics, 5);
        var attackers = new List<string>();
        for (var i = 0; i < 8; i++) attackers.Add((await PlayerAsync()).UserId);

        var results = await Parallel(8, i => Attack(attackers[i], defenderId, $"pile{i}"));

        results.Count(r => r.Accepted).Should().Be(1);
        results.Where(r => !r.Accepted).Should().OnlyContain(r => r.Error == "That player was beaten recently and is protected.");
        var d = await StateAsync(defender);
        (d.WalletCash, await CargoAsync(defender, CargoType.Electronics)).Should().Be((900L, 4));
        d.WalletCash.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task SameAttackerSameTarget_AtOnce_OneBattle()
    {
        var (attacker, _) = await PlayerAsync();
        var (_, defenderId) = await PlayerAsync(skills: 12);

        var results = await Parallel(6, i => Attack(attacker, defenderId, $"dup{i}"));
        var sameKey = await Parallel(6, _ => Attack(attacker, defenderId, "dup0"));

        results.Count(r => r.Accepted).Should().Be(1);
        sameKey.Select(r => r.Receipt?.PvpBattle?.Id).Distinct().Should().HaveCountLessThanOrEqualTo(1);
        await using var db = await DbAsync();
        (await db.AfterHoursPvpBattles.CountAsync(b => b.AttackerUserId == attacker)).Should().Be(1);
    }

    // ------------------------------------------------------------------ defence

    [Fact]
    public async Task Defence_SavesAtomically_AndIsUsedAsSaved()
    {
        var (defender, defenderId) = await PlayerAsync(gear: ["W1", "W2", "O1"]);
        var (attacker, _) = await PlayerAsync();

        var saves = await Parallel(6, i => Actions().SaveDefenceAsync(defender,
            i % 2 == 0 ? PvpTactic.Setup : PvpTactic.Negotiation,
            i % 2 == 0 ? "W1" : "W2", i % 2 == 0 ? "O1" : null, null, $"def{i}"));
        saves.Should().OnlyContain(r => r.Accepted);

        var d = await StateAsync(defender);
        new[] { (PvpTactic.Setup, "W1", "O1"), (PvpTactic.Negotiation, "W2", (string?)null) }
            .Should().Contain((d.DefenceTactic!.Value, d.DefenceWeaponKey!, d.DefenceOutfitKey), "never a mix of two saves");

        await Actions().EquipGearAsync(defender, "W1", "eq");
        var battle = await BattleAsync((await Attack(attacker, defenderId, "vs-def")).Receipt!.PvpBattle!.Id);
        (battle.DefenderTactic, battle.DefenderWeaponKey, battle.DefenderUsedSavedDefence).Should().Be((d.DefenceTactic!.Value, d.DefenceWeaponKey, true));

        (await Actions().SaveDefenceAsync(defender, PvpTactic.Setup, "O1", null, null, "bad")).Error.Should().Contain("does not go in that slot");
    }

    // ------------------------------------------------------------------ reports

    [Fact]
    public async Task Report_IsVisibleToBothSides_Only_AndReloadsIdentically()
    {
        var (attacker, _) = await PlayerAsync();
        var (defender, defenderId) = await PlayerAsync();
        var (stranger, _) = await PlayerAsync();
        var battleId = (await Attack(attacker, defenderId, "rep")).Receipt!.PvpBattle!.Id;
        var rolls = _factory.Dice.Calls;

        var forAttacker = await Pvp().GetBattleAsync(attacker, battleId);
        var forDefender = await Pvp().GetBattleAsync(defender, battleId);
        (await Pvp().GetBattleAsync(stranger, battleId)).Should().BeNull();

        forAttacker.Should().NotBeNull();
        forDefender!.Battle.Should().BeEquivalentTo(forAttacker!.Battle, o => o.Excluding(b => b.Receipt));
        forAttacker.Battle.Rounds.Select(r => r.Round).Should().Equal(1, 2, 3);
        _factory.Dice.Calls.Should().Be(rolls, "reading a report never rolls");
        (await Pvp().GetRecentBattlesAsync(defender)).Should().Contain(v => v.Battle.Id == battleId);
        (await Pvp().GetRecentBattlesAsync(stranger)).Should().NotContain(v => v.Battle.Id == battleId);
    }

    [Fact]
    public async Task Targets_ShowPowerAndStatus_ButNoMoney()
    {
        var (me, _) = await PlayerAsync();
        var (_, newbieId) = await PlayerAsync(backdate: false);
        var (_, strongId) = await PlayerAsync(skills: 10, gear: ["W1"]);

        var targets = await Pvp().GetTargetsAsync(me);

        targets.Single(t => t.StateId == newbieId).BlockReason.Should().Be("That player is new and still protected.");
        var strong = targets.Single(t => t.StateId == strongId);
        (strong.EffectivePower, strong.BlockReason).Should().Be((42, (string?)null));
        typeof(PvpTargetView).GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("Cash") || n.Contains("Wallet") || n.Contains("Cargo"));
    }

    [Fact]
    public async Task Pvp_LeavesBankMyTunoAndFinanceUntouched()
    {
        var (attacker, _) = await PlayerAsync(bank: 700);
        var (defender, defenderId) = await PlayerAsync(bank: 900);
        var before = await OutsideAsync();

        await Attack(attacker, defenderId, "outside");

        ((await StateAsync(attacker)).BankCash, (await StateAsync(defender)).BankCash).Should().Be((700L, 900L));
        (await OutsideAsync()).Should().Be(before);
    }

    // ------------------------------------------------------------------ helpers

    private IAfterHoursActionService Actions() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IAfterHoursActionService>();

    private IPvpService Pvp() => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<IPvpService>();

    private Task<AfterHoursActionResult> Attack(string attackerUserId, int defenderStateId, string key) =>
        Actions().AttackAsync(attackerUserId, defenderStateId, PvpTactic.Negotiation, RiskStance.Standard, null, null, null, key);

    private Task<ApplicationDbContext> DbAsync() =>
        _factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContextAsync();

    private static Task<AfterHoursActionResult[]> Parallel(int count, Func<int, Task<AfterHoursActionResult>> action) =>
        Task.WhenAll(Enumerable.Range(0, count).Select(i => Task.Run(() => action(i))));

    /// <summary>A player with a state in the active cycle; back-dated 4 days so new-player protection is over.</summary>
    private async Task<(string UserId, int StateId)> PlayerAsync(
        bool backdate = true, int skills = 4, long wallet = 400, long bank = 0, params string[] gear)
    {
        var name = $"ah6-{Guid.NewGuid():N}";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = name, Email = $"{name}@test.com", FirstName = "A", LastName = "H", Nickname = name };
        (await users.CreateAsync(user)).Succeeded.Should().BeTrue();
        var state = await scope.ServiceProvider.GetRequiredService<IPlayerCycleStateService>().GetOrCreateForActiveCycleAsync(user.Id);

        await UpdateAsync(user.Id, s =>
        {
            if (backdate) s.CreatedAt = Start.AddDays(-4);
            (s.Toughness, s.Stealth, s.Smarts, s.Charisma) = (skills, skills, skills, skills);
            (s.WalletCash, s.BankCash) = (wallet, bank);
        });
        if (gear.Length > 0)
        {
            await using var db = await DbAsync();
            foreach (var key in gear)
            {
                var item = RTUB.Core.Helpers.AfterHours.GearCatalogue.Find(key)!;
                db.AfterHoursPlayerGear.Add(new PlayerGear { PlayerCycleStateId = state!.Id, ItemKey = key, Slot = item.Slot, Tier = item.Tier });
            }
            await db.SaveChangesAsync();
        }
        return (user.Id, state!.Id);
    }

    private async Task UpdateAsync(string userId, Action<PlayerCycleState> change)
    {
        await using var db = await DbAsync();
        var state = await db.AfterHoursPlayerCycleStates.SingleAsync(s => s.UserId == userId && s.GameCycle!.Status == GameCycleStatus.Active);
        change(state);
        await db.SaveChangesAsync();
    }

    private async Task<PlayerCycleState> StateAsync(string userId)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerCycleStates.AsNoTracking()
            .SingleAsync(s => s.UserId == userId && s.GameCycle!.Status == GameCycleStatus.Active);
    }

    private async Task<PvpBattle> BattleAsync(int id)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPvpBattles.AsNoTracking().Include(b => b.Rounds).Include(b => b.Cargo).SingleAsync(b => b.Id == id);
    }

    private async Task GiveCargoAsync(string userId, CargoType cargo, int quantity)
    {
        var stateId = (await StateAsync(userId)).Id;
        await using var db = await DbAsync();
        db.AfterHoursPlayerCargo.Add(new PlayerCargo { PlayerCycleStateId = stateId, CargoType = cargo, Quantity = quantity });
        await db.SaveChangesAsync();
    }

    private async Task<int> CargoAsync(string userId, CargoType cargo)
    {
        await using var db = await DbAsync();
        return await db.AfterHoursPlayerCargo.Where(c => c.CargoType == cargo)
            .Join(db.AfterHoursPlayerCycleStates.Where(s => s.UserId == userId), c => c.PlayerCycleStateId, s => s.Id, (c, _) => c.Quantity)
            .SumAsync();
    }

    private async Task<(int, int, int, int)> OutsideAsync()
    {
        await using var db = await DbAsync();
        return (await db.Characters.CountAsync(), await db.InventoryItems.CountAsync(), await db.Transactions.CountAsync(), await db.GameScores.CountAsync());
    }
}
