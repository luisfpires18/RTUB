using System.Globalization;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Helpers.AfterHours;

/// <summary>
/// The live-balance values an Owner can override (AH-010). An immutable snapshot: an action loads it once and
/// uses it throughout, so a change mid-action is never half-applied. Defaults are the canonical Game Manual v2
/// values and the earlier units' defaults (the constants on the rule classes), so a database without overrides
/// plays exactly as before. Structural rules — level cap, family size, XP curve, catalogues, objectives — are
/// not tunable.
/// </summary>
public sealed record AfterHoursTuning
{
    public static readonly AfterHoursTuning Default = new();

    // Economy
    public int BankDepositFeePercent { get; init; } = AfterHoursActions.DepositFeePercent;
    public long FamilyCreationCost { get; init; } = FamilyRules.CreateCost;

    // Economy multipliers over the static catalogues (base values stay in code)
    public decimal CrimeCashMultiplier { get; init; } = 1m;
    public decimal BaseFenceCashMultiplier { get; init; } = 1m;
    public decimal BuyerContractCashMultiplier { get; init; } = 1m;
    public decimal TrainingCashCostMultiplier { get; init; } = 1m;
    public decimal EquipmentPriceMultiplier { get; init; } = 1m;

    // Time / recovery
    public int EnergyRegenMinutesPerPoint { get; init; } = 6;
    public int HeatDecayMinutesPerPoint { get; init; } = 10;
    public int CrimeHeatBlockThreshold { get; init; } = CrimeRules.HeatBlockThreshold;
    public int CoverJobEnergyCost { get; init; } = AfterHoursActions.CoverJobEnergyCost;
    public int CoverJobHeatReduction { get; init; } = AfterHoursActions.CoverJobHeatReduction;
    public int CoverJobHighHeatThreshold { get; init; } = AfterHoursActions.CoverJobHighHeat;
    public long CoverJobCashReward { get; init; } = AfterHoursActions.CoverJobCash;
    public long CoverJobXpReward { get; init; } = AfterHoursActions.CoverJobXp;

    // PvP
    public bool PvpEnabled { get; init; } = true;
    public int PvpAttackEnergyCost { get; init; } = PvpRules.AttackEnergy;
    public int PvpGlobalCooldownMinutes { get; init; } = 5;
    public int PvpDefenderProtectionHours { get; init; } = 6;
    public int PvpSameTargetCooldownHours { get; init; } = 24;
    public int PvpNewPlayerProtectionHours { get; init; } = 72;
    public int PvpRecoveryCautiousMinutes { get; init; } = 10;
    public int PvpRecoveryStandardMinutes { get; init; } = 15;
    public int PvpRecoveryRecklessMinutes { get; init; } = 30;

    // Training
    public int TrainingEnergyCost { get; init; } = TrainingRules.EnergyCost;
    public int TrainingPointStorageCap { get; init; } = TrainingRules.MaxStoredPoints;

    // Catch-up (Game Manual v2 section 20)
    public int CatchUpStartCycleDay { get; init; } = 42;
    public int CatchUpLevelCeiling { get; init; } = 12;
    public decimal CatchUpXpMultiplier { get; init; } = 1.5m;

    /// <summary>Wallet cash a successful crime pays: the approach-adjusted catalogue cash × <see cref="CrimeCashMultiplier"/>.</summary>
    public long CrimeCash(long catalogueCash) => Scale(catalogueCash, CrimeCashMultiplier);

    /// <summary>
    /// What the fence pays for a whole sale: (catalogue unit price × quantity) × <see cref="BaseFenceCashMultiplier"/>,
    /// rounded half up once, on the total (3 phones at 15 × 1.1 = 49.5 → 50, not 17 × 3).
    /// </summary>
    public long FencePayout(CargoType cargo, int quantity) => Scale(CargoCatalogue.FencePrice(cargo) * quantity, BaseFenceCashMultiplier);

    /// <summary>Cash for delivering a contract: its stored reward × <see cref="BuyerContractCashMultiplier"/>.</summary>
    public long ContractCash(long storedReward) => Scale(storedReward, BuyerContractCashMultiplier);

    /// <summary>Wallet fee to train from <paramref name="currentRank"/>: the rank formula × <see cref="TrainingCashCostMultiplier"/>.</summary>
    public long TrainingCost(int currentRank) => Scale(TrainingRules.CashCost(currentRank), TrainingCashCostMultiplier);

    /// <summary>Purchase price: catalogue price × <see cref="EquipmentPriceMultiplier"/>.</summary>
    public long GearPrice(GearItem item) => Scale(item.Price, EquipmentPriceMultiplier);

    /// <summary>amount × multiplier for non-negative amounts, rounded half up like every crime multiplier.</summary>
    public static long Scale(long amount, decimal multiplier) =>
        multiplier == 1m ? amount : (long)decimal.Round(amount * multiplier, MidpointRounding.AwayFromZero);

    public TimeSpan EnergyRegenInterval => TimeSpan.FromMinutes(EnergyRegenMinutesPerPoint);
    public TimeSpan HeatDecayInterval => TimeSpan.FromMinutes(HeatDecayMinutesPerPoint);

    /// <summary>Every tunable, in display order, with its validation. The keys are stored; do not rename them.</summary>
    public static readonly IReadOnlyList<TuningSetting> Settings =
    [
        Int("BankDepositFeePercent", "Economy", "Bank deposit fee (%)", 0, 100, t => t.BankDepositFeePercent, (t, v) => t with { BankDepositFeePercent = v }),
        Long("FamilyCreationCost", "Economy", "Family creation cost (wallet)", 0, 1_000_000, t => t.FamilyCreationCost, (t, v) => t with { FamilyCreationCost = v }),
        Dec("CrimeCashMultiplier", "Economy", "Crime cash multiplier", 0.25m, 4m, t => t.CrimeCashMultiplier, (t, v) => t with { CrimeCashMultiplier = v }),
        Dec("BaseFenceCashMultiplier", "Economy", "Fence payout multiplier", 0.25m, 4m, t => t.BaseFenceCashMultiplier, (t, v) => t with { BaseFenceCashMultiplier = v }),
        Dec("BuyerContractCashMultiplier", "Economy", "Buyer contract cash multiplier", 0.25m, 4m, t => t.BuyerContractCashMultiplier, (t, v) => t with { BuyerContractCashMultiplier = v }),
        Dec("TrainingCashCostMultiplier", "Economy", "Training cash cost multiplier", 0.25m, 4m, t => t.TrainingCashCostMultiplier, (t, v) => t with { TrainingCashCostMultiplier = v }),
        Dec("EquipmentPriceMultiplier", "Economy", "Equipment price multiplier", 0.25m, 4m, t => t.EquipmentPriceMultiplier, (t, v) => t with { EquipmentPriceMultiplier = v }),

        Int("EnergyRegenMinutesPerPoint", "Time", "Minutes per energy point", 1, 1_440, t => t.EnergyRegenMinutesPerPoint, (t, v) => t with { EnergyRegenMinutesPerPoint = v }),
        Int("HeatDecayMinutesPerPoint", "Time", "Minutes per heat point decayed", 1, 1_440, t => t.HeatDecayMinutesPerPoint, (t, v) => t with { HeatDecayMinutesPerPoint = v }),
        Int("CrimeHeatBlockThreshold", "Time", "Heat that blocks crimes", 10, 1_000, t => t.CrimeHeatBlockThreshold, (t, v) => t with { CrimeHeatBlockThreshold = v }),
        Int("CoverJobEnergyCost", "Time", "Cover job energy cost", 0, 240, t => t.CoverJobEnergyCost, (t, v) => t with { CoverJobEnergyCost = v }),
        Int("CoverJobHeatReduction", "Time", "Cover job heat reduction", 0, 1_000, t => t.CoverJobHeatReduction, (t, v) => t with { CoverJobHeatReduction = v }),
        Int("CoverJobHighHeatThreshold", "Time", "Heat at which cover jobs repeat", 0, 1_000, t => t.CoverJobHighHeatThreshold, (t, v) => t with { CoverJobHighHeatThreshold = v }),
        Long("CoverJobCashReward", "Time", "Cover job cash", 0, 100_000, t => t.CoverJobCashReward, (t, v) => t with { CoverJobCashReward = v }),
        Long("CoverJobXpReward", "Time", "Cover job XP", 0, 100_000, t => t.CoverJobXpReward, (t, v) => t with { CoverJobXpReward = v }),

        Bool("PvpEnabled", "PvP", "PvP attacks enabled", t => t.PvpEnabled, (t, v) => t with { PvpEnabled = v }),
        Int("PvpAttackEnergyCost", "PvP", "Attack energy cost", 0, 240, t => t.PvpAttackEnergyCost, (t, v) => t with { PvpAttackEnergyCost = v }),
        Int("PvpGlobalCooldownMinutes", "PvP", "Attacker cooldown (minutes)", 1, 1_440, t => t.PvpGlobalCooldownMinutes, (t, v) => t with { PvpGlobalCooldownMinutes = v }),
        Int("PvpDefenderProtectionHours", "PvP", "Beaten defender protection (hours)", 1, 168, t => t.PvpDefenderProtectionHours, (t, v) => t with { PvpDefenderProtectionHours = v }),
        Int("PvpSameTargetCooldownHours", "PvP", "Same target cooldown (hours)", 1, 168, t => t.PvpSameTargetCooldownHours, (t, v) => t with { PvpSameTargetCooldownHours = v }),
        Int("PvpNewPlayerProtectionHours", "PvP", "New player protection (hours)", 1, 720, t => t.PvpNewPlayerProtectionHours, (t, v) => t with { PvpNewPlayerProtectionHours = v }),
        Int("PvpRecoveryCautiousMinutes", "PvP", "Lost attack recovery, Cautious (minutes)", 1, 1_440, t => t.PvpRecoveryCautiousMinutes, (t, v) => t with { PvpRecoveryCautiousMinutes = v }),
        Int("PvpRecoveryStandardMinutes", "PvP", "Lost attack recovery, Standard (minutes)", 1, 1_440, t => t.PvpRecoveryStandardMinutes, (t, v) => t with { PvpRecoveryStandardMinutes = v }),
        Int("PvpRecoveryRecklessMinutes", "PvP", "Lost attack recovery, Reckless (minutes)", 1, 1_440, t => t.PvpRecoveryRecklessMinutes, (t, v) => t with { PvpRecoveryRecklessMinutes = v }),

        Int("TrainingEnergyCost", "Training", "Training energy cost", 0, 240, t => t.TrainingEnergyCost, (t, v) => t with { TrainingEnergyCost = v }),
        Int("TrainingPointStorageCap", "Training", "Training points stored at most", 1, 30, t => t.TrainingPointStorageCap, (t, v) => t with { TrainingPointStorageCap = v }),

        Int("CatchUpStartCycleDay", "Catch-up", "Catch-up starts after cycle day", 0, 366, t => t.CatchUpStartCycleDay, (t, v) => t with { CatchUpStartCycleDay = v }),
        Int("CatchUpLevelCeiling", "Catch-up", "Catch-up applies below level", 1, AfterHoursLevels.MaxLevel, t => t.CatchUpLevelCeiling, (t, v) => t with { CatchUpLevelCeiling = v }),
        Dec("CatchUpXpMultiplier", "Catch-up", "Catch-up XP multiplier", 1m, 5m, t => t.CatchUpXpMultiplier, (t, v) => t with { CatchUpXpMultiplier = v }),
    ];

    public static TuningSetting? Find(string key) => Settings.FirstOrDefault(s => s.Key == key);

    /// <summary>Applies stored overrides over the defaults. Unknown keys and invalid values are skipped and reported.</summary>
    public static AfterHoursTuning From(IEnumerable<KeyValuePair<string, string>> overrides, out IReadOnlyList<(string Key, string Error)> invalid)
    {
        var tuning = Default;
        var problems = new List<(string, string)>();
        foreach (var (key, value) in overrides)
        {
            if (Find(key) is not { } setting) { problems.Add((key, "Unknown setting.")); continue; }
            if (setting.Parse(value, out var apply) is { } error) { problems.Add((key, error)); continue; }
            tuning = apply!(tuning);
        }
        invalid = problems;
        return tuning;
    }

    private static TuningSetting Int(string key, string group, string label, int min, int max, Func<AfterHoursTuning, int> get, Func<AfterHoursTuning, int, AfterHoursTuning> set) =>
        new(key, group, label, $"{min}–{max}", t => get(t).ToString(CultureInfo.InvariantCulture),
            (string raw, out Func<AfterHoursTuning, AfterHoursTuning>? apply) =>
            {
                apply = null;
                if (!int.TryParse(raw?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return "Enter a whole number.";
                if (v < min || v > max) return $"Must be between {min} and {max}.";
                apply = t => set(t, v);
                return null;
            });

    private static TuningSetting Long(string key, string group, string label, long min, long max, Func<AfterHoursTuning, long> get, Func<AfterHoursTuning, long, AfterHoursTuning> set) =>
        new(key, group, label, $"{min}–{max}", t => get(t).ToString(CultureInfo.InvariantCulture),
            (string raw, out Func<AfterHoursTuning, AfterHoursTuning>? apply) =>
            {
                apply = null;
                if (!long.TryParse(raw?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return "Enter a whole number.";
                if (v < min || v > max) return $"Must be between {min} and {max}.";
                apply = t => set(t, v);
                return null;
            });

    private static TuningSetting Dec(string key, string group, string label, decimal min, decimal max, Func<AfterHoursTuning, decimal> get, Func<AfterHoursTuning, decimal, AfterHoursTuning> set) =>
        new(key, group, label, $"{min}–{max}", t => get(t).ToString(CultureInfo.InvariantCulture),
            (string raw, out Func<AfterHoursTuning, AfterHoursTuning>? apply) =>
            {
                apply = null;
                if (!decimal.TryParse(raw?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var v)) return "Enter a number (use . for decimals).";
                if (v < min || v > max) return $"Must be between {min} and {max}.";
                apply = t => set(t, v);
                return null;
            });

    private static TuningSetting Bool(string key, string group, string label, Func<AfterHoursTuning, bool> get, Func<AfterHoursTuning, bool, AfterHoursTuning> set) =>
        new(key, group, label, "true/false", t => get(t) ? "true" : "false",
            (string raw, out Func<AfterHoursTuning, AfterHoursTuning>? apply) =>
            {
                apply = null;
                if (!bool.TryParse(raw?.Trim(), out var v)) return "Enter true or false.";
                apply = t => set(t, v);
                return null;
            });
}

/// <summary>Parses and validates one raw value; on success returns null and a function applying it to a snapshot.</summary>
public delegate string? TuningParser(string raw, out Func<AfterHoursTuning, AfterHoursTuning>? apply);

/// <summary>One tunable: stable key, admin label, allowed range, and its parser.</summary>
public sealed record TuningSetting(string Key, string Group, string Label, string Range, Func<AfterHoursTuning, string> Read, TuningParser Parse)
{
    /// <summary>The value as it would be stored (normalized), or an error.</summary>
    public string? Normalize(string raw, out string normalized)
    {
        normalized = string.Empty;
        if (Parse(raw, out var apply) is { } error) return error;
        normalized = Read(apply!(AfterHoursTuning.Default));
        return null;
    }
}

/// <summary>
/// Catch-up XP, Game Manual v2 section 20: crimes and cover jobs pay ×1.5 XP to players below level 12
/// after cycle day 42. <b>AH-010 boundary:</b> active from exactly <c>StartUtc + 42 days</c>; eligibility is
/// the level before the action, so the action that reaches level 12 still gets it.
/// </summary>
public static class CatchUpRules
{
    public static bool IsActive(DateTime cycleStartUtc, int level, DateTime utcNow, AfterHoursTuning tuning) =>
        utcNow >= cycleStartUtc.AddDays(tuning.CatchUpStartCycleDay) && level < tuning.CatchUpLevelCeiling;

    public static decimal XpMultiplier(DateTime cycleStartUtc, int level, DateTime utcNow, AfterHoursTuning tuning) =>
        IsActive(cycleStartUtc, level, utcNow, tuning) ? tuning.CatchUpXpMultiplier : 1m;

    /// <summary>xp × multiplier, rounded half up like every crime multiplier.</summary>
    public static long Apply(long xp, decimal multiplier) => AfterHoursTuning.Scale(xp, multiplier);
}
