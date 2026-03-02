using RTUB.Core.Enums;

namespace RTUB.Application.Configuration;

/// <summary>
/// Static configuration for village missions.
/// </summary>
public static class VillageMissionsConfig
{
    public record MissionTemplate(
        string Key,
        string Name,
        string[] Beasts,
        int BasePower,
        double BaseRewardWood,
        double BaseRewardStone,
        double BaseRewardFood,
        int BaseDurationSec
    );

    public static readonly IReadOnlyList<MissionTemplate> Templates = new[]
    {
        new MissionTemplate(
            "missao_ronda", "Ronda Noturna",
            new[] { "Caloiros rebeldes", "Gatos vadios" },
            60, 60, 40, 40, 60
        ),
        new MissionTemplate(
            "missao_serenata", "Serenata Rival",
            new[] { "Tuna rival", "Público hostil" },
            120, 120, 80, 60, 90
        ),
        new MissionTemplate(
            "missao_festival", "Festival Regional",
            new[] { "Tunas concorrentes", "Júri exigente" },
            170, 100, 120, 70, 110
        ),
    };

    public static MissionTemplate? GetByKey(string key) => Templates.FirstOrDefault(t => t.Key == key);
}
