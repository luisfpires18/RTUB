using RTUB.Core.Enums;

namespace RTUB.Application.Configuration;

/// <summary>
/// Static configuration for village buildings.
/// </summary>
public static class VillageBuildingsConfig
{
    public record ResourceCost(double Wood, double Stone, double Food);

    public record Prerequisite(VillageBuildingType Building, int Level);

    public record BuildingMeta(
        VillageBuildingType Type,
        string Name,
        string Description,
        IReadOnlyList<Prerequisite> Requirements,
        ResourceCost BaseCost,
        double CostScale,
        int BaseTimeSec,
        double TimeScale
    );

    public static readonly IReadOnlyDictionary<VillageBuildingType, BuildingMeta> Buildings =
        new Dictionary<VillageBuildingType, BuildingMeta>
        {
            [VillageBuildingType.SalaDaTuna] = new(
                VillageBuildingType.SalaDaTuna,
                "Sala da Tuna",
                "Sede central da tuna. Desbloqueia outros edifícios.",
                Array.Empty<Prerequisite>(),
                new ResourceCost(220, 180, 110),
                1.6, 60, 1.5
            ),
            [VillageBuildingType.CentroAcademico] = new(
                VillageBuildingType.CentroAcademico,
                "Centro Académico",
                "Sala de ensaios. Permite treinar tropas.",
                new[] { new Prerequisite(VillageBuildingType.SalaDaTuna, 1) },
                new ResourceCost(160, 140, 90),
                1.55, 45, 1.4
            ),
            [VillageBuildingType.Cantina] = new(
                VillageBuildingType.Cantina,
                "Cantina",
                "Servir refeições. Aumenta a produção de comida.",
                new[] { new Prerequisite(VillageBuildingType.SalaDaTuna, 1) },
                new ResourceCost(130, 120, 100),
                1.5, 40, 1.35
            ),
            [VillageBuildingType.TeatroMunicipal] = new(
                VillageBuildingType.TeatroMunicipal,
                "Teatro Municipal",
                "Permite realizar festivais e ganhar prestígio.",
                new[] { new Prerequisite(VillageBuildingType.SalaDaTuna, 2) },
                new ResourceCost(200, 180, 120),
                1.6, 55, 1.45
            ),
            [VillageBuildingType.CamaraMunicipal] = new(
                VillageBuildingType.CamaraMunicipal,
                "Câmara Municipal",
                "Entrega atuações para prestígio.",
                new[] { new Prerequisite(VillageBuildingType.SalaDaTuna, 2) },
                new ResourceCost(250, 200, 150),
                1.65, 65, 1.5
            ),
        };

    public static BuildingMeta Get(VillageBuildingType type) => Buildings[type];

    public static string DisplayName(VillageBuildingType type) => Buildings[type].Name;
}
