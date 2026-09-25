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

    private static PlayerActionReceipt Begin(PlayerCycleState state, PlayerActionKind action, string request) =>
        new() { PlayerCycleStateId = state.Id, Action = action, Request = request, LevelBefore = state.Level };

    private static ActionAttempt Finish(PlayerCycleState state, PlayerActionReceipt receipt)
    {
        receipt.LevelAfter = state.Level;
        return ActionAttempt.Accept(receipt);
    }
}
