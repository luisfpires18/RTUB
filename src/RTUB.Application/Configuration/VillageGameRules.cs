using RTUB.Core.Enums;

namespace RTUB.Application.Configuration;

/// <summary>
/// Core game rules and formulas for the village game.
/// </summary>
public static class VillageGameRules
{
    // ======================== Storage ========================

    /// <summary>Base storage capacity (no Storage building).</summary>
    public const double BaseStorageCap = 900;

    /// <summary>Additional capacity per Storage building level.</summary>
    public const double StorageCapPerLevel = 300;

    /// <summary>
    /// Calculate the storage cap based on Sala da Tuna building level.
    /// </summary>
    public static double GetStorageCap(int salaDaTunaLevel) => BaseStorageCap + StorageCapPerLevel * salaDaTunaLevel;

    // ======================== Field Production ========================

    /// <summary>
    /// Production rate per second for a field at a given level.
    /// Level 0 fields produce at base rate. Each level adds baseProd.
    /// </summary>
    public static double GetFieldProductionRate(double baseProd, int level) =>
        baseProd * (1 + level);

    /// <summary>
    /// Total production rate for all fields of a resource type.
    /// </summary>
    public static double GetTotalProductionRate(double baseProd, int[] fieldLevels)
    {
        double total = 0;
        foreach (var level in fieldLevels)
            total += GetFieldProductionRate(baseProd, level);
        return total;
    }

    // ======================== Field Upgrade Costs ========================

    /// <summary>
    /// Cost to upgrade a field to the next level.
    /// Uses 1.6x scaling per level (same as buildings).
    /// </summary>
    public static (double Wood, double Stone, double Food) GetFieldUpgradeCost(
        VillageResourceType resourceType, int toLevel)
    {
        var field = VillageFieldsConfig.GetByResource(resourceType);
        var scale = Math.Pow(1.6, Math.Max(0, toLevel - 1));
        return (
            Math.Max(1, Math.Round(field.BaseCostWood * scale)),
            Math.Max(1, Math.Round(field.BaseCostStone * scale)),
            Math.Max(1, Math.Round(field.BaseCostFood * scale))
        );
    }

    /// <summary>
    /// Time in seconds to upgrade a field to the next level.
    /// </summary>
    public static int GetFieldUpgradeTime(VillageResourceType resourceType, int toLevel)
    {
        var field = VillageFieldsConfig.GetByResource(resourceType);
        var scale = Math.Pow(1.35, Math.Max(0, toLevel - 1));
        return Math.Max(1, (int)Math.Round(field.BaseTimeSec * scale));
    }

    // ======================== Building Costs ========================

    /// <summary>
    /// Cost to build/upgrade a city building to the specified level.
    /// </summary>
    public static (double Wood, double Stone, double Food) GetBuildingCost(
        VillageBuildingType buildingType, int toLevel)
    {
        var meta = VillageBuildingsConfig.Get(buildingType);
        var scale = Math.Pow(meta.CostScale, Math.Max(0, toLevel - 1));
        return (
            Math.Max(1, Math.Round(meta.BaseCost.Wood * scale)),
            Math.Max(1, Math.Round(meta.BaseCost.Stone * scale)),
            Math.Max(1, Math.Round(meta.BaseCost.Food * scale))
        );
    }

    /// <summary>
    /// Time in seconds to build/upgrade a city building.
    /// </summary>
    public static int GetBuildingTime(VillageBuildingType buildingType, int toLevel, double speedMult = 1.0)
    {
        var meta = VillageBuildingsConfig.Get(buildingType);
        var scale = Math.Pow(meta.TimeScale, Math.Max(0, toLevel - 1));
        var baseSecs = meta.BaseTimeSec * scale;
        return Math.Max(1, (int)Math.Round(baseSecs / Math.Max(0.01, speedMult)));
    }

    /// <summary>
    /// Check if building prerequisites are met.
    /// </summary>
    public static bool CanBuild(VillageBuildingType buildingType, IReadOnlyDictionary<VillageBuildingType, int> cityLevels)
    {
        var meta = VillageBuildingsConfig.Get(buildingType);
        foreach (var req in meta.Requirements)
        {
            if (!cityLevels.TryGetValue(req.Building, out var level) || level < req.Level)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Get unmet requirements for a building.
    /// </summary>
    public static IReadOnlyList<string> GetUnmetRequirements(VillageBuildingType buildingType, IReadOnlyDictionary<VillageBuildingType, int> cityLevels)
    {
        var meta = VillageBuildingsConfig.Get(buildingType);
        var unmet = new List<string>();
        foreach (var req in meta.Requirements)
        {
            if (!cityLevels.TryGetValue(req.Building, out var level) || level < req.Level)
                unmet.Add($"{VillageBuildingsConfig.DisplayName(req.Building)} Lv. {req.Level}");
        }
        return unmet;
    }

    // ======================== Missions ========================

    public record DifficultyModifier(double PowerMult, double DurMult, double RewardMult);

    public static readonly IReadOnlyDictionary<VillageMissionDifficulty, DifficultyModifier> DifficultyModifiers =
        new Dictionary<VillageMissionDifficulty, DifficultyModifier>
        {
            [VillageMissionDifficulty.Easy] = new(1.0, 1.0, 1.0),
            [VillageMissionDifficulty.Medium] = new(1.6, 1.3, 1.8),
            [VillageMissionDifficulty.Hard] = new(2.4, 1.6, 2.8),
        };

    /// <summary>
    /// Compute derived mission stats (required power, duration, reward) based on template and difficulty.
    /// </summary>
    public static (int RequiredPower, int DurationSec, double RewardWood, double RewardStone, double RewardFood)
        GetMissionDerived(VillageMissionsConfig.MissionTemplate template, VillageMissionDifficulty difficulty)
    {
        var d = DifficultyModifiers[difficulty];
        return (
            (int)Math.Floor(template.BasePower * d.PowerMult),
            Math.Max(15, (int)Math.Floor(template.BaseDurationSec * d.DurMult)),
            Math.Floor(template.BaseRewardWood * d.RewardMult),
            Math.Floor(template.BaseRewardStone * d.RewardMult),
            Math.Floor(template.BaseRewardFood * d.RewardMult)
        );
    }

    /// <summary>
    /// Calculate total attack power of a troop allocation.
    /// </summary>
    public static int TotalAttackPower(Dictionary<VillageUnitType, int> troops)
    {
        return troops.Sum(kvp => kvp.Value * VillageUnitsConfig.Get(kvp.Key).Attack);
    }

    /// <summary>
    /// Calculate casualty ratio based on total attack vs required power.
    /// </summary>
    public static double CasualtiesRatio(int totalAttack, int requiredPower)
    {
        if (totalAttack <= 0) return 1.0;
        // No losses if massively overpowered (>= 1.75x)
        if (totalAttack >= requiredPower * 1.75) return 0.0;
        var r = (double)requiredPower / totalAttack;
        return Math.Clamp(0.35 * r, 0, 0.7);
    }

    /// <summary>
    /// Calculate troop losses for a mission.
    /// </summary>
    public static Dictionary<VillageUnitType, int> CalculateLosses(
        Dictionary<VillageUnitType, int> sent, int totalAttack, int requiredPower)
    {
        var ratio = CasualtiesRatio(totalAttack, requiredPower);
        var losses = new Dictionary<VillageUnitType, int>();
        foreach (var kvp in sent)
        {
            var lost = (int)Math.Floor(kvp.Value * ratio);
            if (lost > 0)
                losses[kvp.Key] = lost;
        }
        return losses;
    }

    // ======================== World Map ========================

    /// <summary>
    /// String hash for deterministic world map generation.
    /// </summary>
    public static uint StrHash(string s)
    {
        uint h = 0;
        foreach (var c in s)
        {
            h = (h << 5) - h + c;
            h &= 0xFFFFFFFF;
        }
        return h;
    }

    /// <summary>
    /// Mulberry32 PRNG seeded by a uint.
    /// </summary>
    public static Func<double> Mulberry32(uint seed)
    {
        uint state = seed;
        return () =>
        {
            unchecked
            {
                state += 0x6D2B79F5;
                uint t = state;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return (double)((t ^ (t >> 14)) >> 0) / 4294967296.0;
            }
        };
    }
}
