using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Helpers.AfterHours;

/// <summary>How accepted actions turn into objective progress. Pure; the tracker persists it.</summary>
public static class ObjectiveRules
{
    /// <summary>
    /// What an accepted action counts for the player who took it, read from its receipt (and battle):
    /// never from a client. Actions not listed (bank, gear, family creation, …) count for nothing.
    /// Membership- and target-dependent metrics are added by the tracker.
    /// </summary>
    public static Dictionary<ObjectiveMetric, long> ActorMetrics(PlayerActionReceipt r)
    {
        var m = new Dictionary<ObjectiveMetric, long>();
        void Add(ObjectiveMetric metric, long amount)
        {
            if (amount > 0) m[metric] = m.GetValueOrDefault(metric) + amount;
        }

        switch (r.Action)
        {
            case PlayerActionKind.Crime:
                Add(ObjectiveMetric.EnergySpent, -r.EnergyDelta);
                Add(ObjectiveMetric.CrimeEnergy, -r.EnergyDelta);
                if (r.Succeeded)
                {
                    Add(ObjectiveMetric.SuccessfulCrimes, 1);
                    Add(ObjectiveMetric.CrimeCash, r.WalletDelta);
                }
                break;
            case PlayerActionKind.CoverJob:
                Add(ObjectiveMetric.EnergySpent, -r.EnergyDelta);
                if (r.HeatDelta < 0) Add(ObjectiveMetric.CoverJobsReducingHeat, 1);
                break;
            case PlayerActionKind.TrainSkill:
                Add(ObjectiveMetric.EnergySpent, -r.EnergyDelta);
                break;
            case PlayerActionKind.PvpAttack:
                Add(ObjectiveMetric.EnergySpent, -r.EnergyDelta);
                if (r.PvpBattle?.AttackerWon == true) Add(ObjectiveMetric.PvpWins, 1);
                break;
            case PlayerActionKind.FenceSale:
                Add(ObjectiveMetric.FencedUnits, -r.CargoDelta);
                Add(ObjectiveMetric.CargoMoved, -r.CargoDelta);
                break;
            case PlayerActionKind.ContractDelivery:
                Add(ObjectiveMetric.ContractsDelivered, 1);
                Add(ObjectiveMetric.CargoMoved, -r.CargoDelta);
                break;
            case PlayerActionKind.DonateToFamily:
                Add(ObjectiveMetric.Donated, -r.WalletDelta);
                break;
        }
        return m;
    }

    /// <summary>Adds progress (clamped at the target). True exactly once: when this call completes it.</summary>
    public static bool Advance(PlayerObjectiveProgress row, long amount, DateTime utcNow)
    {
        if (row.CompletedAtUtc is not null || amount <= 0) return false;
        row.Progress = Math.Min(row.Target, row.Progress + amount);
        if (row.Progress < row.Target) return false;
        row.CompletedAtUtc = utcNow;
        return true;
    }

    public static bool Advance(FamilyObjectiveProgress row, long amount, DateTime utcNow)
    {
        if (row.CompletedAtUtc is not null || amount <= 0) return false;
        row.Progress = Math.Min(row.Target, row.Progress + amount);
        if (row.Progress < row.Target) return false;
        row.CompletedAtUtc = utcNow;
        return true;
    }
}
