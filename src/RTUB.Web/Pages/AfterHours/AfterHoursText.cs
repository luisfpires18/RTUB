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
                $"{CrimeCatalogue.Find(r.CrimeId)?.SuccessText} +{r.WalletDelta} cash, +{r.XpDelta} XP, +{r.HeatDelta} heat.",
            PlayerActionKind.Crime =>
                $"It went wrong. +{r.XpDelta} XP, +{r.HeatDelta} heat."
                + (r.Jailed ? $" Caught: jailed until {r.JailUntilUtc:HH:mm} UTC." : " You got away."),
            PlayerActionKind.CoverJob =>
                $"You kept your head down on a cover job. +{r.WalletDelta} cash, +{r.XpDelta} XP, {r.HeatDelta} heat.",
            PlayerActionKind.Deposit =>
                $"Deposited {-r.WalletDelta} (fee {-r.WalletDelta - r.BankDelta}). Bank +{r.BankDelta}.",
            PlayerActionKind.Withdraw =>
                $"Withdrew {r.WalletDelta} to your wallet.",
            _ => "Done."
        };

        return r.LevelAfter > r.LevelBefore ? $"{line} Level up: {r.LevelAfter}!" : line;
    }

    public static string JailRemaining(PlayerCycleState state, DateTime utcNow)
    {
        var minutes = (int)Math.Ceiling((state.JailUntilUtc!.Value - utcNow).TotalMinutes);
        return minutes <= 1 ? "less than a minute" : $"{minutes} minutes";
    }
}
