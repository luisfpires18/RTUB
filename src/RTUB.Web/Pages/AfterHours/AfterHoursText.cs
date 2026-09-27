using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Pages.AfterHours;

/// <summary>Short English result lines for After Hours receipts. Display only; no balance logic.</summary>
public static class AfterHoursText
{
    public static string Describe(PlayerActionReceipt r)
    {
        var line = r.Action switch
        {
            PlayerActionKind.Crime when r.Succeeded =>
                $"{CrimeCatalogue.Find(r.CrimeId)?.SuccessText} +{r.WalletDelta} cash{Cargo(r)}, +{r.XpDelta} XP, +{r.HeatDelta} heat.",
            PlayerActionKind.Crime =>
                $"It went wrong. +{r.XpDelta} XP, +{r.HeatDelta} heat."
                + (r.Jailed ? $" Caught: jailed until {r.JailUntilUtc:HH:mm} UTC." : " You got away."),
            PlayerActionKind.CoverJob =>
                $"You kept your head down on a cover job. +{r.WalletDelta} cash, +{r.XpDelta} XP, {r.HeatDelta} heat.",
            PlayerActionKind.Deposit =>
                $"Deposited {-r.WalletDelta} (fee {-r.WalletDelta - r.BankDelta}). Bank +{r.BankDelta}.",
            PlayerActionKind.Withdraw =>
                $"Withdrew {r.WalletDelta} to your wallet.",
            PlayerActionKind.FenceSale =>
                $"The fence took {-r.CargoDelta} {CargoCatalogue.Name(r.CargoType!.Value, -r.CargoDelta)}. +{r.WalletDelta} cash.",
            PlayerActionKind.TrainSkill =>
                $"{r.Skill} trained to {r.SkillRankAfter}. {r.EnergyDelta} energy, -${-r.WalletDelta}.",
            PlayerActionKind.PurchaseGear =>
                $"Bought {GearCatalogue.Find(r.GearKey)?.Name} for ${-r.WalletDelta}.",
            PlayerActionKind.EquipGear =>
                $"Equipped {GearCatalogue.Find(r.GearKey)?.Name}.",
            PlayerActionKind.UnequipGear =>
                $"Unequipped {GearCatalogue.Find(r.GearKey)?.Name}.",
            PlayerActionKind.ContractDelivery =>
                $"Delivered {-r.CargoDelta} {CargoCatalogue.Name(r.CargoType!.Value, -r.CargoDelta)}. +{r.WalletDelta} cash, +{r.XpDelta} XP.",
            _ => "Done."
        };

        return r.LevelAfter > r.LevelBefore ? $"{line} Level up: {r.LevelAfter}!" : line;
    }

    private static string Cargo(PlayerActionReceipt r) =>
        r.CargoType is { } cargo && r.CargoDelta > 0 ? $", +{r.CargoDelta} {CargoCatalogue.Name(cargo, r.CargoDelta)}" : "";

    public static string CargoLabel(RTUB.Core.Enums.AfterHours.CargoType cargo, int quantity) =>
        $"{quantity} {CargoCatalogue.Name(cargo, quantity)}";

    /// <summary>"in 3h 20m" style time left until a UTC instant.</summary>
    public static string TimeLeft(DateTime untilUtc, DateTime utcNow)
    {
        var left = untilUtc - utcNow;
        if (left <= TimeSpan.Zero) return "expired";
        return left.TotalHours >= 1 ? $"{(int)left.TotalHours}h {left.Minutes}m" : $"{Math.Max(1, (int)Math.Ceiling(left.TotalMinutes))}m";
    }

    public static string JailRemaining(PlayerCycleState state, DateTime utcNow)
    {
        var minutes = (int)Math.Ceiling((state.JailUntilUtc!.Value - utcNow).TotalMinutes);
        return minutes <= 1 ? "less than a minute" : $"{minutes} minutes";
    }
}
