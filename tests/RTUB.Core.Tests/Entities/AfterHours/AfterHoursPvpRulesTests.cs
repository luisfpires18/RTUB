using FluentAssertions;
using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Core.Tests.Entities.AfterHours;

/// <summary>AH-006 PvP rules as pure functions, with scripted dice.</summary>
public class AfterHoursPvpRulesTests
{
    private static readonly DateTime Created = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T0 = Created.AddDays(5); // past everyone's new-player protection

    private static PlayerCycleState Player(int id, int t = 4, int st = 4, int sm = 4, int ch = 4, long wallet = 400, params string[] gear)
    {
        var s = PlayerCycleState.CreateInitial(1, $"user-{id}", Created);
        s.Id = id;
        (s.Toughness, s.Stealth, s.Smarts, s.Charisma, s.WalletCash) = (t, st, sm, ch, wallet);
        foreach (var key in gear)
        {
            var item = GearCatalogue.Find(key)!;
            s.Gear.Add(new PlayerGear { PlayerCycleStateId = id, ItemKey = key, Slot = item.Slot, Tier = item.Tier });
        }
        return s;
    }

    private static Func<int> Rolls(params int[] rolls)
    {
        var queue = new Queue<int>(rolls);
        return () => queue.Count > 0 ? queue.Dequeue() : 3; // 3 → random factor 0
    }

    // ------------------------------------------------------------------ power

    [Fact]
    public void EffectivePower_IsTheManualFormula_OverBestOwnedGear_IgnoringEquipped()
    {
        var s = Player(1, t: 4, st: 5, sm: 6, ch: 7, gear: ["W1", "W2", "O1", "V3"]);
        s.EquippedWeaponKey = null;

        PvpRules.EffectivePower(s).Should().Be(22 + 2 * 2 + 1 + 3);

        s.EquippedWeaponKey = "W1";
        PvpRules.EffectivePower(s).Should().Be(30, "equipping never changes effective power");
        PvpRules.LoadoutPower(s, new GearTiers(1, 0, 0)).Should().Be(22 + 2);
    }

    // ------------------------------------------------------------------ tactics

    [Fact]
    public void Matchups_FourAdvantages_EverythingElseNeutral()
    {
        var wins = new[]
        {
            (PvpTactic.Ambush, PvpTactic.Negotiation), (PvpTactic.Negotiation, PvpTactic.Setup),
            (PvpTactic.Setup, PvpTactic.Counterattack), (PvpTactic.Counterattack, PvpTactic.Ambush)
        };
        foreach (var a in Enum.GetValues<PvpTactic>())
        foreach (var b in Enum.GetValues<PvpTactic>())
            PvpRules.Matchup(a, b).Should().Be(wins.Contains((a, b)) ? 4 : 0, $"{a} vs {b}");
    }

    [Fact]
    public void Specialisation_Ah006Defaults()
    {
        var s = Player(1, t: 6, st: 7, sm: 8, ch: 9);
        var gear = new GearTiers(2, 1, 3);

        PvpRules.Specialisation(PvpTactic.Ambush, s, gear).Should().Be(3 + 2);
        PvpRules.Specialisation(PvpTactic.Negotiation, s, gear).Should().Be(5 + 4);
        PvpRules.Specialisation(PvpTactic.Counterattack, s, gear).Should().Be(2 + 6);
        PvpRules.Specialisation(PvpTactic.Setup, s, gear).Should().Be(4 + 6);
    }

    [Theory]
    [InlineData(RiskStance.Cautious, -2, 10)]
    [InlineData(RiskStance.Standard, 0, 15)]
    [InlineData(RiskStance.Reckless, 2, 30)]
    public void RiskStance_ModifierAndRecovery(RiskStance stance, int modifier, int minutes)
    {
        PvpRules.RiskModifier(stance).Should().Be(modifier);
        PvpRules.AttackerRecovery(stance).Should().Be(TimeSpan.FromMinutes(minutes));
    }

    [Theory]
    [InlineData(1, -2)]
    [InlineData(2, -1)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(6, -2)]
    [InlineData(100, 2)]
    public void RandomFactor_MapsTheDiceToMinusTwoToTwo(int roll, int factor) => PvpRules.RandomFactor(roll).Should().Be(factor);

    // ------------------------------------------------------------------ battle

    private static readonly PvpSide Even = new(PvpTactic.Ambush, 16, 0, 0, 0);

    [Fact]
    public void Fight_ThreeRounds_DamageAndTies_AndTheTieBreakIsRecorded()
    {
        // R1 +2 vs −2: attacker 18–14, deals 14. R2 0 vs 0: tie, 1 each. R3 −2 vs +2: defender deals 14.
        var fight = PvpRules.Fight(Even, Even, Rolls(5, 1, 3, 3, 1, 5, 50));

        fight.Rounds.Should().HaveCount(3);
        fight.Rounds.Select(r => (r.AttackerScore, r.DefenderScore, r.AttackerDamage, r.DefenderDamage)).Should().Equal(
            (18, 14, 14, 0), (16, 16, 1, 1), (14, 18, 0, 14));
        fight.Rounds.Select(r => (r.AttackerRandom, r.DefenderRandom)).Should().Equal((2, -2), (0, 0), (-2, 2));
        (fight.AttackerTotalDamage, fight.DefenderTotalDamage, fight.TieBreakRoll, fight.AttackerWon).Should().Be((15, 15, 50, true));

        PvpRules.Fight(Even, Even, Rolls(5, 1, 3, 3, 1, 5, 51)).AttackerWon.Should().BeFalse("a tie-break roll above 50 goes to the defender");
    }

    [Fact]
    public void Fight_DamageIsCappedAt30_AndAllModifiersCount()
    {
        var strong = new PvpSide(PvpTactic.Counterattack, 40, 5, 4, 2);
        var fight = PvpRules.Fight(strong, Even, Rolls());

        fight.Rounds.Should().OnlyContain(r => r.AttackerScore == 51 && r.DefenderScore == 16 && r.AttackerDamage == 30 && r.DefenderDamage == 0);
        (fight.AttackerTotalDamage, fight.TieBreakRoll, fight.AttackerWon).Should().Be((90, (int?)null, true));
    }

    // ------------------------------------------------------------------ loot

    [Theory]
    [InlineData(20, 40, "0.25")]
    [InlineData(40, 20, "1")]
    [InlineData(40, 40, "1")]
    [InlineData(16, 30, "0.2844")]
    [InlineData(1, 100, "0.0001")]
    public void LootMultiplier_RatioSquared_CappedAtOne_Truncated(int defender, int attacker, string expected) =>
        PvpRules.LootMultiplier(defender, attacker).Should().Be(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));

    [Theory]
    [InlineData(3_000, "1", 300)]
    [InlineData(10_000, "1", 500)]
    [InlineData(10_000, "0.25", 125)]
    [InlineData(9, "1", 0)]
    [InlineData(999, "0.2844", 28)]
    public void WalletLoot_TenPercent_CappedAt500_TimesMultiplier(long wallet, string multiplier, long stolen) =>
        PvpRules.WalletLoot(wallet, decimal.Parse(multiplier, System.Globalization.CultureInfo.InvariantCulture)).Should().Be(stolen);

    [Fact]
    public void Cargo_BudgetIsTwentyPercent_CappedAt250_AndSelectionIsHighestPriceFirst()
    {
        var mixed = Player(2);
        mixed.AddCargo(CargoType.ArtPiece, 3);    // 240
        mixed.AddCargo(CargoType.Electronics, 2); // 60
        mixed.AddCargo(CargoType.Phone, 1);       // 15
        var budget = PvpRules.CargoBudget(mixed.Cargo, 1m);

        budget.Should().Be(63);
        PvpRules.SelectCargo(mixed.Cargo, budget).Should().Equal((CargoType.Electronics, 2));

        var rich = Player(3);
        rich.AddCargo(CargoType.Phone, 5);
        rich.AddCargo(CargoType.ArtPiece, 20);
        PvpRules.CargoBudget(rich.Cargo, 1m).Should().Be(250);
        PvpRules.SelectCargo(rich.Cargo, 250).Should().Equal((CargoType.ArtPiece, 3));
        PvpRules.SelectCargo(rich.Cargo, 95).Should().Equal((CargoType.ArtPiece, 1), (CargoType.Phone, 1));

        var tiny = Player(4);
        tiny.AddCargo(CargoType.Phone, 1);
        PvpRules.CargoBudget(tiny.Cargo, 1m).Should().Be(3);
        PvpRules.SelectCargo(tiny.Cargo, 3).Should().BeEmpty();
    }

    // ------------------------------------------------------------------ protection and restrictions

    [Fact]
    public void NewPlayerProtection_LastsExactly72Hours_UnlessTheyAttackFirst()
    {
        var s = Player(1);

        PvpRules.HasNewPlayerProtection(s, Created.AddHours(72).AddTicks(-1)).Should().BeTrue();
        PvpRules.HasNewPlayerProtection(s, Created.AddHours(72)).Should().BeFalse();

        s.PvpInitiatedAtUtc = Created.AddHours(1);
        PvpRules.HasNewPlayerProtection(s, Created.AddHours(2)).Should().BeFalse();
    }

    private static ActionAttempt Attack(PlayerCycleState a, PlayerCycleState d, DateTime now, DateTime? last = null,
        string? w = null, string? o = null, string? v = null, Func<int>? rolls = null) =>
        AfterHoursActions.Attack(a, d, PvpTactic.Ambush, RiskStance.Standard, w, o, v, last, now, rolls ?? Rolls());

    [Fact]
    public void Attack_Refusals()
    {
        var d = Player(2);
        Attack(Player(1), Player(1), T0).Error.Should().Be("You can't attack yourself.");

        var jailed = Player(1);
        jailed.JailUntilUtc = T0.AddMinutes(1);
        Attack(jailed, d, T0).Error.Should().Be("You are in jail.");

        var recovering = Player(1);
        recovering.PvpRecoveryUntilUtc = T0.AddMinutes(1);
        Attack(recovering, d, T0).Error.Should().StartWith("You are still recovering");

        var cooling = Player(1);
        cooling.PvpCooldownUntilUtc = T0.AddSeconds(1);
        Attack(cooling, d, T0).Error.Should().StartWith("You just fought");

        var tired = Player(1);
        tired.Energy = 19;
        tired.EnergyUpdatedAtUtc = T0;
        Attack(tired, d, T0).Error.Should().Be("Not enough energy.");

        var newbie = PlayerCycleState.CreateInitial(1, "newbie", T0.AddHours(-1));
        newbie.Id = 9;
        Attack(Player(1), newbie, T0).Error.Should().Be("That player is new and still protected.");

        var beaten = Player(2);
        beaten.PvpProtectedUntilUtc = T0.AddHours(1);
        Attack(Player(1), beaten, T0).Error.Should().Be("That player was beaten recently and is protected.");

        Attack(Player(1), d, T0, last: T0.AddHours(-23)).Error.Should().StartWith("You already attacked that player");
        Attack(Player(1), d, T0, last: T0.AddHours(-24)).Error.Should().BeNull("the 24-hour window has passed");

        var otherCycle = Player(2);
        otherCycle.GameCycleId = 99;
        Attack(Player(1), otherCycle, T0).Error.Should().Be("That player is not in this cycle.");
    }

    [Fact]
    public void Attack_ValidatesTheLoadout()
    {
        var d = Player(2);
        Attack(Player(1, gear: ["W1"]), d, T0, w: "W1").Error.Should().BeNull();
        Attack(Player(1), d, T0, w: "W1").Error.Should().Be("You don't own Brass Knuckles.");
        Attack(Player(1, gear: ["O1"]), d, T0, w: "O1").Error.Should().Be("Hooded Jacket does not go in that slot.");
        Attack(Player(1), d, T0, v: "ZZ").Error.Should().Be("Unknown item.");
        Attack(Player(1, gear: ["W4"]), d, T0).Error.Should().BeNull("an empty slot is allowed even when gear is owned");
    }

    [Fact]
    public void Attack_Win_TransfersCappedLoot_AppliesRecoveryProtectionCooldown_AndBuildsTheBattle()
    {
        var a = Player(1, t: 8, st: 8, gear: ["W2", "O1"]);
        var d = Player(2, wallet: 3_000);
        d.BankCash = 9_999;
        d.AddCargo(CargoType.ArtPiece, 3);
        d.AddCargo(CargoType.Electronics, 2);

        var receipt = Attack(a, d, T0, w: "W2", rolls: Rolls()).Receipt!;
        var battle = receipt.PvpBattle!;

        battle.AttackerWon.Should().BeTrue();
        battle.AttackerEffectivePower.Should().Be(24 + 4 + 1);
        battle.DefenderEffectivePower.Should().Be(16);
        battle.LootMultiplier.Should().Be(Math.Round(256m / 841m, 4, MidpointRounding.ToZero));
        battle.WalletStolen.Should().Be((long)decimal.Floor(300 * battle.LootMultiplier));
        (a.WalletCash, d.WalletCash, d.BankCash).Should().Be((400 + battle.WalletStolen, 3_000 - battle.WalletStolen, 9_999L));

        var cargoBudget = (long)decimal.Floor(60 * battle.LootMultiplier); // ⌊300 × 20%⌋ = 60 → 18
        cargoBudget.Should().Be(18);
        battle.Cargo.Should().BeEmpty("18 buys nothing: the cheapest owned cargo costs 30");

        (a.Energy, receipt.EnergyDelta).Should().Be((220, -20));
        (d.PvpRecoveryUntilUtc, d.PvpProtectedUntilUtc).Should().Be((T0.AddMinutes(15), T0.AddHours(6)));
        a.PvpRecoveryUntilUtc.Should().BeNull();
        a.PvpCooldownUntilUtc.Should().Be(T0.AddMinutes(5));
        (battle.DefenderTactic, battle.DefenderUsedSavedDefence).Should().Be((PvpTactic.Counterattack, false));
        (battle.AttackerWeaponKey, battle.AttackerWeaponTier, battle.AttackerOutfitKey).Should().Be(("W2", 2, (string?)null));
        battle.Rounds.Should().HaveCount(3);
    }

    [Fact]
    public void Attack_Win_MovesCargoRows_WhenTheBudgetAllows()
    {
        var a = Player(1);
        var d = Player(2, t: 6, st: 6, sm: 6, ch: 6); // stronger than the attacker: multiplier 1
        d.AddCargo(CargoType.ArtPiece, 10);
        d.AddCargo(CargoType.Phone, 4);

        // Weaker attacker forces three tied rounds and wins the tie-break (default roll 3 ≤ 50).
        var battle = AfterHoursActions.Attack(a, d, PvpTactic.Setup, RiskStance.Reckless, null, null, null, null, T0,
            Rolls(5, 1, 5, 1, 5, 1)).Receipt!.PvpBattle!;

        // Setup beats Counterattack (+4), Reckless +2, random +2 → 16+4+2+2 = 24 vs defender 24+2+(−2) = 24: ties.
        battle.Rounds.Should().OnlyContain(r => r.AttackerScore == r.DefenderScore);
        battle.TieBreakRoll.Should().Be(3);
        battle.AttackerWon.Should().BeTrue();
        battle.LootMultiplier.Should().Be(1m);
        // Base value 800 + 60 = 860 → budget ⌊860 × 20%⌋ = 172: two art pieces (160), then 12 left < a phone (15).
        battle.Cargo.Select(c => (c.CargoType, c.Quantity)).Should().Equal((CargoType.ArtPiece, 2));
        (d.CargoQuantity(CargoType.ArtPiece), a.CargoQuantity(CargoType.ArtPiece), d.CargoQuantity(CargoType.Phone), a.CargoQuantity(CargoType.Phone))
            .Should().Be((8, 2, 4, 0));
    }

    [Fact]
    public void Attack_Loss_NoLoot_AttackerRecoversByStance_NoDefenderProtection()
    {
        var a = Player(1);
        var d = Player(2, t: 12, st: 12, sm: 12, ch: 12, wallet: 5_000);

        var battle = AfterHoursActions.Attack(a, d, PvpTactic.Ambush, RiskStance.Reckless, null, null, null, null, T0, Rolls()).Receipt!.PvpBattle!;

        battle.AttackerWon.Should().BeFalse();
        (battle.WalletStolen, a.WalletCash, d.WalletCash).Should().Be((0L, 400L, 5_000L));
        a.PvpRecoveryUntilUtc.Should().Be(T0.AddMinutes(30));
        (d.PvpRecoveryUntilUtc, d.PvpProtectedUntilUtc).Should().Be(((DateTime?)null, (DateTime?)null));
        a.PvpCooldownUntilUtc.Should().Be(T0.AddMinutes(5));
    }

    [Fact]
    public void FirstAcceptedAttack_EndsOwnNewPlayerProtection_ARefusedOneDoesNot()
    {
        var newbie = PlayerCycleState.CreateInitial(1, "newbie", T0.AddHours(-1));
        newbie.Id = 1;

        AfterHoursActions.Attack(newbie, Player(2), PvpTactic.Ambush, RiskStance.Standard, "W9", null, null, null, T0, Rolls()).Error.Should().NotBeNull();
        PvpRules.HasNewPlayerProtection(newbie, T0).Should().BeTrue();

        var battle = AfterHoursActions.Attack(newbie, Player(2), PvpTactic.Ambush, RiskStance.Standard, null, null, null, null, T0, Rolls()).Receipt!.PvpBattle!;
        battle.EndedAttackerNewPlayerProtection.Should().BeTrue();
        newbie.PvpInitiatedAtUtc.Should().Be(T0);
        PvpRules.HasNewPlayerProtection(newbie, T0).Should().BeFalse();
    }

    [Fact]
    public void Defence_SavedSetupIsUsed_AndSurvivesEquipmentChanges()
    {
        var d = Player(2, gear: ["W1", "O1", "W2"]);
        d.EquippedWeaponKey = "W1";
        AfterHoursActions.SaveDefence(d, PvpTactic.Negotiation, "W2", null, null).Error.Should().BeNull();
        AfterHoursActions.SaveDefence(d, PvpTactic.Negotiation, "O1", null, null).Error.Should().Be("Hooded Jacket does not go in that slot.");
        AfterHoursActions.SaveDefence(d, PvpTactic.Negotiation, "W3", null, null).Error.Should().Be("You don't own Compact Pistol.");
        (d.DefenceTactic, d.DefenceWeaponKey).Should().Be((PvpTactic.Negotiation, "W2"));

        AfterHoursActions.EquipGear(d, "O1");
        var battle = Attack(Player(1), d, T0).Receipt!.PvpBattle!;

        (battle.DefenderTactic, battle.DefenderUsedSavedDefence).Should().Be((PvpTactic.Negotiation, true));
        (battle.DefenderWeaponKey, battle.DefenderWeaponTier, battle.DefenderOutfitKey).Should().Be(("W2", 2, (string?)null));
    }
}
