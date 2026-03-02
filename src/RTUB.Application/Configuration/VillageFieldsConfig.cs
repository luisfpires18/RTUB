using RTUB.Core.Enums;

namespace RTUB.Application.Configuration;

/// <summary>
/// Static configuration for village resource fields.
/// </summary>
public static class VillageFieldsConfig
{
    public record FieldDef(
        string Key,
        string Name,
        VillageResourceType Resource,
        double BaseProd,
        double BaseCostWood,
        double BaseCostStone,
        double BaseCostFood,
        int BaseTimeSec,
        int Count
    );

    public static readonly FieldDef Lumber = new("madeira", "Serralharia", VillageResourceType.Wood, 0.25, 60, 40, 20, 25, 4);
    public static readonly FieldDef Quarry = new("pedra", "Mina", VillageResourceType.Stone, 0.25, 40, 60, 20, 25, 4);
    public static readonly FieldDef Farm = new("comida", "Horta", VillageResourceType.Food, 0.25, 30, 30, 40, 25, 4);

    public static readonly IReadOnlyList<FieldDef> All = new[] { Lumber, Quarry, Farm };

    public static FieldDef GetByResource(VillageResourceType type) => type switch
    {
        VillageResourceType.Wood => Lumber,
        VillageResourceType.Stone => Quarry,
        VillageResourceType.Food => Farm,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    /// <summary>
    /// Total field count for new village initialization: 4+4+4 = 12.
    /// </summary>
    public static int TotalFieldCount => All.Sum(f => f.Count);
}
