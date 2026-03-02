using RTUB.Core.Enums;

namespace RTUB.Application.Configuration;

/// <summary>
/// Static configuration for village unit types.
/// </summary>
public static class VillageUnitsConfig
{
    public record UnitDef(
        VillageUnitType Type,
        string Name,
        string Icon,
        string Building,
        double CostWood,
        double CostStone,
        double CostFood,
        int TrainTimeSec,
        int Attack,
        int Defense,
        int Carry,
        int Speed,
        double Upkeep
    );

    public static readonly IReadOnlyDictionary<VillageUnitType, UnitDef> Units =
        new Dictionary<VillageUnitType, UnitDef>
        {
            [VillageUnitType.Leitao] = new(
                VillageUnitType.Leitao, "Leitão", "🐷", "CentroAcademico",
                80, 50, 40, 15, 16, 12, 40, 8, 0.02
            ),
            [VillageUnitType.Caloiro] = new(
                VillageUnitType.Caloiro, "Caloiro", "🎓", "CentroAcademico",
                110, 75, 60, 20, 24, 18, 48, 6, 0.03
            ),
            [VillageUnitType.Tuno] = new(
                VillageUnitType.Tuno, "Tuno", "🎵", "CentroAcademico",
                150, 110, 90, 30, 34, 25, 55, 5, 0.06
            ),
        };

    public static UnitDef Get(VillageUnitType type) => Units[type];

    public static IEnumerable<UnitDef> All => Units.Values;
}
