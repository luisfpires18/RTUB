using RTUB.Core.Enums;

namespace RTUB.Application.DTOs;

/// <summary>
/// Full snapshot of a village's state, sent to the UI.
/// </summary>
public class VillageStateDto
{
    public int VillageId { get; set; }
    public string Kingdom { get; set; } = "Vila";
    public int PosX { get; set; }
    public int PosY { get; set; }

    // Resources
    public double Wood { get; set; }
    public double Stone { get; set; }
    public double Food { get; set; }
    public double StorageCap { get; set; }

    // Production rates (per second)
    public double WoodRate { get; set; }
    public double StoneRate { get; set; }
    public double FoodRate { get; set; }

    // Unique display stats
    public int Population { get; set; }
    public double Prestige { get; set; }
    public double Money { get; set; }

    // Fields: grouped by resource type
    public List<FieldDto> Fields { get; set; } = new();

    // City buildings
    public Dictionary<VillageBuildingType, int> Buildings { get; set; } = new();

    // Active build job (null if none)
    public BuildJobDto? ActiveBuildJob { get; set; }

    // Troops
    public Dictionary<VillageUnitType, int> Troops { get; set; } = new();

    // Missions
    public List<MissionDto> ActiveMissions { get; set; } = new();
    public List<MissionDto> CompletedMissions { get; set; } = new();

    // Available buildings that can be constructed (prerequisites met, not yet built)
    public List<VillageBuildingType> AvailableBuildings { get; set; } = new();
}

public class FieldDto
{
    public VillageResourceType ResourceType { get; set; }
    public int SlotIndex { get; set; }
    public int Level { get; set; }
    public double NextCostWood { get; set; }
    public double NextCostStone { get; set; }
    public double NextCostFood { get; set; }
    public int NextUpgradeTimeSec { get; set; }
}

public class BuildJobDto
{
    public int Id { get; set; }
    public bool IsFieldUpgrade { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public int ToLevel { get; set; }
    public DateTime StartedAt { get; set; }
    public int DurationSecs { get; set; }
    public DateTime FinishesAt { get; set; }
    public double RemainingSeconds { get; set; }
    public double Progress { get; set; }
}

public class MissionDto
{
    public int Id { get; set; }
    public string MissionKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, int> SentTroops { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public DateTime FinishesAt { get; set; }
    public double RemainingSeconds { get; set; }
    public double? RewardWood { get; set; }
    public double? RewardStone { get; set; }
    public double? RewardFood { get; set; }
    public Dictionary<string, int>? Losses { get; set; }
    public bool Claimed { get; set; }
}
