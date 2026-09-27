using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Helpers.AfterHours;

/// <summary>Either an accepted receipt (the state has been changed) or a refusal (the state must be discarded).</summary>
public sealed record ActionAttempt(PlayerActionReceipt? Receipt, string? Error)
{
    public static ActionAttempt Accept(PlayerActionReceipt receipt) => new(receipt, null);
    public static ActionAttempt Reject(string error) => new(null, error);
}

/// <summary>
/// The After Hours solo actions as pure rules over a <see cref="PlayerCycleState"/>. They validate
/// first and mutate only when accepting, but they do reconcile energy and heat to
/// <c>utcNow</c>, so callers must persist the state only for an accepted attempt.
/// Persistence, transactions and idempotency are the application service's job.
/// </summary>
public static class AfterHoursActions
{
    // Cover jobs. Energy cost and heat reduction are Game Manual v2; the high-heat threshold,
    // cash and XP are AH-003 implementation choices (the manual does not give them).
    public const int CoverJobEnergyCost = 10;
    public const int CoverJobHeatReduction = 15;
    public const int CoverJobHighHeat = 50;
    public const long CoverJobCash = 20;
    public const long CoverJobXp = 12;

    /// <summary>Deposit fee, Game Manual v2. Withdrawals are free (AH-003 choice).</summary>
    public const int DepositFeePercent = 2;

    public static string CrimeRequest(string crimeId, CrimeApproach approach) => $"crime:{crimeId}:{approach}";
    public const string CoverJobRequest = "cover";
    public static string DepositRequest(long amount) => $"deposit:{amount}";
    public static string WithdrawRequest(long amount) => $"withdraw:{amount}";
    public static string FenceRequest(CargoType cargo, int quantity) => $"fence:{cargo}:{quantity}";
    public static string ContractRequest(int buyerContractId) => $"contract:{buyerContractId}";
    public static string TrainRequest(PlayerSkill skill) => $"train:{skill}";
    public static string PurchaseGearRequest(string itemKey) => $"gear-buy:{itemKey}";
    public static string EquipGearRequest(string itemKey) => $"gear-equip:{itemKey}";
    public static string UnequipGearRequest(string itemKey) => $"gear-unequip:{itemKey}";

    /// <summary>Everything the attacker chose, so a key replayed with any change is refused.</summary>
    public static string AttackRequest(int defenderStateId, PvpTactic tactic, RiskStance stance, string? weapon, string? outfit, string? vehicleTool) =>
        $"pvp:{defenderStateId}:{(int)tactic}{(int)stance}:{weapon ?? "-"}:{outfit ?? "-"}:{vehicleTool ?? "-"}";

    public static string DefenceRequest(PvpTactic tactic, string? weapon, string? outfit, string? vehicleTool) =>
        $"defence:{(int)tactic}:{weapon ?? "-"}:{outfit ?? "-"}:{vehicleTool ?? "-"}";

    /// <param name="rollPercent">Server dice: 1..100 inclusive. Success when roll ≤ chance.</param>
    public static ActionAttempt CommitCrime(
        PlayerCycleState state, string crimeId, CrimeApproach approach, DateTime utcNow, Func<int> rollPercent)
    {
        var crime = CrimeCatalogue.Find(crimeId);
        if (crime is null) return ActionAttempt.Reject("Unknown crime.");
        if (!Enum.IsDefined(approach)) return ActionAttempt.Reject("Unknown approach.");

        state.Reconcile(utcNow);
        if (CrimeBlockReason(state, crime, utcNow) is { } blocked) return ActionAttempt.Reject(blocked);

        var odds = CrimeRules.Odds(crime, approach, state);
        var heatBefore = state.Heat;
        var receipt = Begin(state, PlayerActionKind.Crime, CrimeRequest(crime.Id, approach));
        receipt.CrimeId = crime.Id;
        receipt.Approach = approach;
        receipt.SuccessChance = odds.Chance;
        receipt.SuccessRoll = rollPercent();
        receipt.Succeeded = receipt.SuccessRoll <= odds.Chance;

        state.Energy -= crime.EnergyCost;
        receipt.EnergyDelta = -crime.EnergyCost;

        if (receipt.Succeeded)
        {
            state.WalletCash += odds.Cash;
            receipt.WalletDelta = odds.Cash;
            receipt.XpDelta = odds.Xp;
            if (crime.Cargo is { } cargo)
            {
                state.AddCargo(cargo, crime.CargoQuantity);
                receipt.CargoType = cargo;
                receipt.CargoDelta = crime.CargoQuantity;
            }
        }
        else
        {
            receipt.XpDelta = odds.FailureXp;
            if (rollPercent() <= JailPolicy.Chance(crime.RequiredLevel, heatBefore))
            {
                state.JailUntilUtc = utcNow + JailPolicy.Duration(crime.RequiredLevel, heatBefore);
                receipt.Jailed = true;
                receipt.JailUntilUtc = state.JailUntilUtc;
            }
        }

        state.AddXp(receipt.XpDelta);
        state.AddHeat(odds.Heat, utcNow);
        receipt.HeatDelta = odds.Heat;
        return Finish(state, receipt);
    }

    /// <summary>
    /// 10 energy, −15 heat (not below 0), +cash, +XP. At heat ≥ 50 it can be repeated; below that it
    /// is limited to one per UTC calendar day, and only those low-heat uses count against the day.
    /// Blocked in jail (AH-003 choice).
    /// </summary>
    public static ActionAttempt TakeCoverJob(PlayerCycleState state, DateTime utcNow)
    {
        state.Reconcile(utcNow);
        if (CoverJobBlockReason(state, utcNow) is { } blocked) return ActionAttempt.Reject(blocked);

        var today = DateOnly.FromDateTime(utcNow);
        var highHeat = state.Heat >= CoverJobHighHeat;

        var receipt = Begin(state, PlayerActionKind.CoverJob, CoverJobRequest);
        receipt.Succeeded = true;

        state.Energy -= CoverJobEnergyCost;
        receipt.EnergyDelta = -CoverJobEnergyCost;

        var heatBefore = state.Heat;
        state.AddHeat(-CoverJobHeatReduction, utcNow);
        receipt.HeatDelta = state.Heat - heatBefore;

        state.WalletCash += CoverJobCash;
        receipt.WalletDelta = CoverJobCash;
        receipt.XpDelta = CoverJobXp;
        state.AddXp(CoverJobXp);

        if (!highHeat) state.CoverJobDailyUsedOn = today;
        return Finish(state, receipt);
    }

    /// <summary>Why this crime cannot be attempted now, or null. Expects a reconciled state.</summary>
    public static string? CrimeBlockReason(PlayerCycleState state, CrimeDefinition crime, DateTime utcNow)
    {
        if (state.Level < crime.RequiredLevel) return $"Requires level {crime.RequiredLevel}.";
        if (state.IsJailedAt(utcNow)) return "You are in jail.";
        if (state.Heat >= CrimeRules.HeatBlockThreshold) return "Too much heat. Lie low first.";
        if (state.Energy < crime.EnergyCost) return "Not enough energy.";
        return null;
    }

    /// <summary>Why a cover job cannot be taken now, or null. Expects a reconciled state.</summary>
    public static string? CoverJobBlockReason(PlayerCycleState state, DateTime utcNow)
    {
        if (state.IsJailedAt(utcNow)) return "You are in jail.";
        if (state.Energy < CoverJobEnergyCost) return "Not enough energy.";
        if (state.Heat < CoverJobHighHeat && state.CoverJobDailyUsedOn == DateOnly.FromDateTime(utcNow))
            return "You already worked a cover job today.";
        return null;
    }

    public static long DepositFee(long amount) => (amount * DepositFeePercent + 99) / 100;

    /// <summary>Wallet −amount; bank +(amount − ⌈2% of amount⌉). Refused when the bank would get nothing.</summary>
    public static ActionAttempt Deposit(PlayerCycleState state, long amount)
    {
        if (amount <= 0) return ActionAttempt.Reject("Enter an amount above zero.");
        if (state.WalletCash < amount) return ActionAttempt.Reject("Not enough cash in your wallet.");

        var net = amount - DepositFee(amount);
        if (net <= 0) return ActionAttempt.Reject("That deposit is too small to cover the fee.");

        var receipt = Begin(state, PlayerActionKind.Deposit, DepositRequest(amount));
        receipt.Succeeded = true;
        state.WalletCash -= amount;
        state.BankCash += net;
        receipt.WalletDelta = -amount;
        receipt.BankDelta = net;
        return Finish(state, receipt);
    }

    public static ActionAttempt Withdraw(PlayerCycleState state, long amount)
    {
        if (amount <= 0) return ActionAttempt.Reject("Enter an amount above zero.");
        if (state.BankCash < amount) return ActionAttempt.Reject("Not enough cash in the bank.");

        var receipt = Begin(state, PlayerActionKind.Withdraw, WithdrawRequest(amount));
        receipt.Succeeded = true;
        state.BankCash -= amount;
        state.WalletCash += amount;
        receipt.BankDelta = -amount;
        receipt.WalletDelta = amount;
        return Finish(state, receipt);
    }

    /// <summary>Sells cargo at the base fence: quantity × the server's price. Always available.</summary>
    public static ActionAttempt SellToFence(PlayerCycleState state, CargoType cargo, int quantity)
    {
        if (!Enum.IsDefined(cargo)) return ActionAttempt.Reject("Unknown cargo.");
        if (quantity <= 0) return ActionAttempt.Reject("Enter a quantity above zero.");
        if (state.CargoQuantity(cargo) < quantity) return ActionAttempt.Reject("You don't have that much.");

        var cash = quantity * CargoCatalogue.FencePrice(cargo);
        var receipt = Begin(state, PlayerActionKind.FenceSale, FenceRequest(cargo, quantity));
        receipt.Succeeded = true;
        state.AddCargo(cargo, -quantity);
        state.WalletCash += cash;
        receipt.CargoType = cargo;
        receipt.CargoDelta = -quantity;
        receipt.WalletDelta = cash;
        return Finish(state, receipt);
    }

    /// <summary>Why this player cannot deliver this contract now, or null.</summary>
    public static string? ContractBlockReason(PlayerCycleState state, BuyerContract contract, bool alreadyCompleted, DateTime utcNow)
    {
        if (contract.GameCycleId != state.GameCycleId) return "That contract is not part of this cycle.";
        if (!contract.IsOpenAt(utcNow)) return "That contract has expired.";
        if (alreadyCompleted) return "You already delivered this contract.";
        if (state.CargoQuantity(contract.CargoType) < contract.Quantity)
            return $"You need {contract.Quantity} {CargoCatalogue.Name(contract.CargoType, contract.Quantity)}.";
        return null;
    }

    /// <summary>Delivers a buyer contract: −cargo, +cash, +XP. The caller records the completion row.</summary>
    public static ActionAttempt DeliverContract(PlayerCycleState state, BuyerContract contract, bool alreadyCompleted, DateTime utcNow)
    {
        if (ContractBlockReason(state, contract, alreadyCompleted, utcNow) is { } blocked) return ActionAttempt.Reject(blocked);

        var receipt = Begin(state, PlayerActionKind.ContractDelivery, ContractRequest(contract.Id));
        receipt.Succeeded = true;
        state.AddCargo(contract.CargoType, -contract.Quantity);
        state.WalletCash += contract.CashReward;
        state.AddXp(contract.XpReward);
        receipt.CargoType = contract.CargoType;
        receipt.CargoDelta = -contract.Quantity;
        receipt.WalletDelta = contract.CashReward;
        receipt.XpDelta = contract.XpReward;
        return Finish(state, receipt);
    }

    /// <summary>Why this skill cannot be trained now, or null. Expects a reconciled state.</summary>
    public static string? TrainingBlockReason(PlayerCycleState state, PlayerSkill skill)
    {
        var rank = CrimeRules.SkillRank(state, skill);
        if (rank >= TrainingRules.SkillCap(state.Level)) return "At your current cap. Level up to train further.";
        if (state.TrainingPoints < 1) return "No training points. You get one each day.";
        if (state.Energy < TrainingRules.EnergyCost) return "Not enough energy.";
        if (state.WalletCash < TrainingRules.CashCost(rank)) return "Not enough cash in your wallet.";
        return null;
    }

    /// <summary>Raises one skill by 1: -1 training point, -20 energy, -(120 + 40(rank - 4)) wallet cash.</summary>
    public static ActionAttempt TrainSkill(PlayerCycleState state, PlayerSkill skill, DateTime utcNow)
    {
        if (!Enum.IsDefined(skill)) return ActionAttempt.Reject("Unknown skill.");

        state.Reconcile(utcNow);
        if (TrainingBlockReason(state, skill) is { } blocked) return ActionAttempt.Reject(blocked);

        var rank = CrimeRules.SkillRank(state, skill);
        var cost = TrainingRules.CashCost(rank);
        var receipt = Begin(state, PlayerActionKind.TrainSkill, TrainRequest(skill));
        receipt.Succeeded = true;
        state.TrainingPoints -= 1;
        state.Energy -= TrainingRules.EnergyCost;
        state.WalletCash -= cost;
        CrimeRules.SetSkillRank(state, skill, rank + 1);
        receipt.Skill = skill;
        receipt.SkillRankAfter = rank + 1;
        receipt.EnergyDelta = -TrainingRules.EnergyCost;
        receipt.WalletDelta = -cost;
        return Finish(state, receipt);
    }

    /// <summary>Why this item cannot be bought now, or null.</summary>
    public static string? PurchaseBlockReason(PlayerCycleState state, GearItem item)
    {
        if (state.OwnsGear(item.Key)) return "You already own this.";
        if (state.Level < item.UnlockLevel) return $"Requires level {item.UnlockLevel}.";
        if (state.WalletCash < item.Price) return "Not enough cash in your wallet.";
        return null;
    }

    /// <summary>
    /// Buys an item with wallet cash; it stays owned for the cycle. If nothing is equipped in its slot
    /// it is equipped straight away (AH-005 choice).
    /// </summary>
    public static ActionAttempt PurchaseGear(PlayerCycleState state, string itemKey)
    {
        var item = GearCatalogue.Find(itemKey);
        if (item is null) return ActionAttempt.Reject("Unknown item.");
        if (PurchaseBlockReason(state, item) is { } blocked) return ActionAttempt.Reject(blocked);

        var receipt = Begin(state, PlayerActionKind.PurchaseGear, PurchaseGearRequest(item.Key));
        receipt.Succeeded = true;
        state.WalletCash -= item.Price;
        state.Gear.Add(new PlayerGear { PlayerCycleStateId = state.Id, ItemKey = item.Key, Slot = item.Slot, Tier = item.Tier });
        if (state.EquippedKey(item.Slot) is null) state.SetEquipped(item.Slot, item.Key);
        receipt.GearKey = item.Key;
        receipt.WalletDelta = -item.Price;
        return Finish(state, receipt);
    }

    /// <summary>Equips an owned item, replacing whatever was in its slot.</summary>
    public static ActionAttempt EquipGear(PlayerCycleState state, string itemKey)
    {
        var item = GearCatalogue.Find(itemKey);
        if (item is null) return ActionAttempt.Reject("Unknown item.");
        if (!state.OwnsGear(item.Key)) return ActionAttempt.Reject("You don't own this.");
        if (state.EquippedKey(item.Slot) == item.Key) return ActionAttempt.Reject("Already equipped.");

        var receipt = Begin(state, PlayerActionKind.EquipGear, EquipGearRequest(item.Key));
        receipt.Succeeded = true;
        state.SetEquipped(item.Slot, item.Key);
        receipt.GearKey = item.Key;
        return Finish(state, receipt);
    }

    public static ActionAttempt UnequipGear(PlayerCycleState state, string itemKey)
    {
        var item = GearCatalogue.Find(itemKey);
        if (item is null) return ActionAttempt.Reject("Unknown item.");
        if (state.EquippedKey(item.Slot) != item.Key) return ActionAttempt.Reject("That item is not equipped.");

        var receipt = Begin(state, PlayerActionKind.UnequipGear, UnequipGearRequest(item.Key));
        receipt.Succeeded = true;
        state.SetEquipped(item.Slot, null);
        receipt.GearKey = item.Key;
        return Finish(state, receipt);
    }

    /// <summary>
    /// Resolves a loadout choice: each key must be an owned item of that slot, or null for empty.
    /// Tiers always come from the catalogue, never from the caller.
    /// </summary>
    public static string? ValidateLoadout(PlayerCycleState state, string? weapon, string? outfit, string? vehicleTool, out GearTiers tiers)
    {
        tiers = new GearTiers(0, 0, 0);
        var picked = new List<int>(3);
        foreach (var (slot, key) in new[] { (GearSlot.Weapon, weapon), (GearSlot.Outfit, outfit), (GearSlot.VehicleTool, vehicleTool) })
        {
            if (key is null) { picked.Add(0); continue; }
            var item = GearCatalogue.Find(key);
            if (item is null) return "Unknown item.";
            if (item.Slot != slot) return $"{item.Name} does not go in that slot.";
            if (!state.OwnsGear(key)) return $"You don't own {item.Name}.";
            picked.Add(item.Tier);
        }
        tiers = new GearTiers(picked[0], picked[1], picked[2]);
        return null;
    }

    /// <summary>Saves the player's defence setup in one change. No cost.</summary>
    public static ActionAttempt SaveDefence(PlayerCycleState state, PvpTactic tactic, string? weapon, string? outfit, string? vehicleTool)
    {
        if (!Enum.IsDefined(tactic)) return ActionAttempt.Reject("Unknown tactic.");
        if (ValidateLoadout(state, weapon, outfit, vehicleTool, out _) is { } invalid) return ActionAttempt.Reject(invalid);

        var receipt = Begin(state, PlayerActionKind.SaveDefence, DefenceRequest(tactic, weapon, outfit, vehicleTool));
        receipt.Succeeded = true;
        state.DefenceTactic = tactic;
        state.DefenceWeaponKey = weapon;
        state.DefenceOutfitKey = outfit;
        state.DefenceVehicleToolKey = vehicleTool;
        return Finish(state, receipt);
    }

    /// <summary>
    /// One PvP attack, fully resolved: restrictions, energy, three rounds, loot, recovery, protection
    /// and cooldown, all applied to the two tracked states, and an immutable <see cref="PvpBattle"/>
    /// attached to the receipt. The caller commits both in one transaction.
    /// </summary>
    /// <param name="lastAttackOnTargetUtc">When this attacker last attacked this target (accepted), if ever.</param>
    public static ActionAttempt Attack(
        PlayerCycleState attacker, PlayerCycleState defender, PvpTactic tactic, RiskStance stance,
        string? weapon, string? outfit, string? vehicleTool, DateTime? lastAttackOnTargetUtc,
        DateTime utcNow, Func<int> rollPercent)
    {
        if (!Enum.IsDefined(tactic)) return ActionAttempt.Reject("Unknown tactic.");
        if (!Enum.IsDefined(stance)) return ActionAttempt.Reject("Unknown stance.");

        if (defender.Id == attacker.Id || defender.UserId == attacker.UserId) return ActionAttempt.Reject("You can't attack yourself.");

        attacker.Reconcile(utcNow);
        if (PvpRules.AttackerBlockReason(attacker, utcNow) is { } attackerBlocked)
            return ActionAttempt.Reject(attackerBlocked);
        if (PvpRules.TargetBlockReason(attacker, defender, lastAttackOnTargetUtc, utcNow) is { } targetBlocked)
            return ActionAttempt.Reject(targetBlocked);
        if (ValidateLoadout(attacker, weapon, outfit, vehicleTool, out var attackerTiers) is { } invalid)
            return ActionAttempt.Reject(invalid);

        // The defender's saved setup, or the default: Counterattack with what is equipped right now.
        var savedDefence = defender.DefenceTactic is not null;
        var defenderTactic = defender.DefenceTactic ?? PvpRules.DefaultDefenceTactic;
        string? Owned(string? key) => key is not null && defender.OwnsGear(key) ? key : null;
        var dWeapon = Owned(savedDefence ? defender.DefenceWeaponKey : defender.EquippedWeaponKey);
        var dOutfit = Owned(savedDefence ? defender.DefenceOutfitKey : defender.EquippedOutfitKey);
        var dVehicle = Owned(savedDefence ? defender.DefenceVehicleToolKey : defender.EquippedVehicleToolKey);
        ValidateLoadout(defender, dWeapon, dOutfit, dVehicle, out var defenderTiers);

        var battle = new PvpBattle
        {
            GameCycleId = attacker.GameCycleId,
            AttackerStateId = attacker.Id,
            DefenderStateId = defender.Id,
            AttackerUserId = attacker.UserId,
            DefenderUserId = defender.UserId,
            AcceptedAtUtc = utcNow,
            AttackerTactic = tactic,
            RiskStance = stance,
            DefenderTactic = defenderTactic,
            DefenderUsedSavedDefence = savedDefence,
            AttackerWeaponKey = weapon, AttackerWeaponTier = attackerTiers.Weapon,
            AttackerOutfitKey = outfit, AttackerOutfitTier = attackerTiers.Outfit,
            AttackerVehicleToolKey = vehicleTool, AttackerVehicleToolTier = attackerTiers.VehicleTool,
            DefenderWeaponKey = dWeapon, DefenderWeaponTier = defenderTiers.Weapon,
            DefenderOutfitKey = dOutfit, DefenderOutfitTier = defenderTiers.Outfit,
            DefenderVehicleToolKey = dVehicle, DefenderVehicleToolTier = defenderTiers.VehicleTool,
            AttackerEffectivePower = PvpRules.EffectivePower(attacker),
            DefenderEffectivePower = PvpRules.EffectivePower(defender),
            AttackerLoadoutPower = PvpRules.LoadoutPower(attacker, attackerTiers),
            DefenderLoadoutPower = PvpRules.LoadoutPower(defender, defenderTiers),
            AttackerSpecialisation = PvpRules.Specialisation(tactic, attacker, attackerTiers),
            DefenderSpecialisation = PvpRules.Specialisation(defenderTactic, defender, defenderTiers),
            AttackerMatchupBonus = PvpRules.Matchup(tactic, defenderTactic),
            DefenderMatchupBonus = PvpRules.Matchup(defenderTactic, tactic),
            AttackerRiskModifier = PvpRules.RiskModifier(stance),
        };

        var receipt = Begin(attacker, PlayerActionKind.PvpAttack, AttackRequest(defender.Id, tactic, stance, weapon, outfit, vehicleTool));
        receipt.PvpBattle = battle;

        attacker.Energy -= PvpRules.AttackEnergy;
        receipt.EnergyDelta = -PvpRules.AttackEnergy;
        if (attacker.PvpInitiatedAtUtc is null)
        {
            battle.EndedAttackerNewPlayerProtection = PvpRules.HasNewPlayerProtection(attacker, utcNow);
            attacker.PvpInitiatedAtUtc = utcNow;
        }

        var fight = PvpRules.Fight(
            new PvpSide(tactic, battle.AttackerLoadoutPower, battle.AttackerSpecialisation, battle.AttackerMatchupBonus, battle.AttackerRiskModifier),
            new PvpSide(defenderTactic, battle.DefenderLoadoutPower, battle.DefenderSpecialisation, battle.DefenderMatchupBonus, 0),
            rollPercent);
        battle.Rounds = fight.Rounds.ToList();
        battle.AttackerTotalDamage = fight.AttackerTotalDamage;
        battle.DefenderTotalDamage = fight.DefenderTotalDamage;
        battle.TieBreakRoll = fight.TieBreakRoll;
        battle.AttackerWon = fight.AttackerWon;
        receipt.Succeeded = fight.AttackerWon;

        battle.LootMultiplier = PvpRules.LootMultiplier(battle.DefenderEffectivePower, battle.AttackerEffectivePower);
        if (fight.AttackerWon)
        {
            var stolen = PvpRules.WalletLoot(defender.WalletCash, battle.LootMultiplier);
            defender.WalletCash -= stolen;
            attacker.WalletCash += stolen;
            battle.WalletStolen = stolen;
            receipt.WalletDelta = stolen;

            var budget = PvpRules.CargoBudget(defender.Cargo, battle.LootMultiplier);
            foreach (var (cargo, quantity) in PvpRules.SelectCargo(defender.Cargo, budget))
            {
                defender.AddCargo(cargo, -quantity);
                attacker.AddCargo(cargo, quantity);
                battle.Cargo.Add(new PvpBattleCargo { CargoType = cargo, Quantity = quantity });
            }

            defender.PvpRecoveryUntilUtc = battle.DefenderRecoveryUntilUtc = utcNow + PvpRules.DefeatedDefenderRecovery;
            defender.PvpProtectedUntilUtc = battle.DefenderProtectedUntilUtc = utcNow + PvpRules.DefeatedDefenderProtection;
        }
        else
        {
            attacker.PvpRecoveryUntilUtc = battle.AttackerRecoveryUntilUtc = utcNow + PvpRules.AttackerRecovery(stance);
        }

        attacker.PvpCooldownUntilUtc = battle.AttackerCooldownUntilUtc = utcNow + PvpRules.AttackCooldown;
        return Finish(attacker, receipt);
    }

    /// <summary>
    /// An accepted receipt for an action whose checks need the database and so live in the application
    /// service (families). The caller has already applied the change.
    /// </summary>
    public static ActionAttempt Accepted(PlayerCycleState state, PlayerActionKind action, string request, int? familyId, long walletDelta = 0)
    {
        var receipt = Begin(state, action, request);
        receipt.Succeeded = true;
        receipt.FamilyId = familyId;
        receipt.WalletDelta = walletDelta;
        return Finish(state, receipt);
    }

    private static PlayerActionReceipt Begin(PlayerCycleState state, PlayerActionKind action, string request) =>
        new() { PlayerCycleStateId = state.Id, Action = action, Request = request, LevelBefore = state.Level };

    private static ActionAttempt Finish(PlayerCycleState state, PlayerActionReceipt receipt)
    {
        receipt.LevelAfter = state.Level;
        return ActionAttempt.Accept(receipt);
    }
}
