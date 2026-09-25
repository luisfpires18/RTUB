using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Helpers.AfterHours;

public sealed record GearItem(string Key, string Name, GearSlot Slot, int Tier, int UnlockLevel, long Price);

/// <summary>Best owned tier per slot (0 = nothing owned).</summary>
public sealed record GearTiers(int Weapon, int Outfit, int VehicleTool);

/// <summary>
/// Year-one gear. Names, tiers and unlock levels follow Game Manual v2; <b>prices are AH-005
/// defaults</b> (the manual gives ranges) and can be tuned here. Server-owned: never persisted,
/// never sent by a client.
/// </summary>
public static class GearCatalogue
{
    public static readonly IReadOnlyList<GearItem> All =
    [
        new("W1", "Brass Knuckles", GearSlot.Weapon, 1, 3, 400),
        new("O1", "Hooded Jacket", GearSlot.Outfit, 1, 3, 500),
        new("V1", "Lockpick Kit", GearSlot.VehicleTool, 1, 3, 600),
        new("W2", "Switchblade", GearSlot.Weapon, 2, 10, 2_000),
        new("O2", "Armored Jacket", GearSlot.Outfit, 2, 10, 2_750),
        new("V2", "Modified Scooter", GearSlot.VehicleTool, 2, 10, 3_500),
        new("W3", "Compact Pistol", GearSlot.Weapon, 3, 16, 9_000),
        new("O3", "Tailored Protection", GearSlot.Outfit, 3, 16, 12_000),
        new("V3", "Getaway Car", GearSlot.VehicleTool, 3, 16, 15_000),
        new("W4", "Collector Weapon", GearSlot.Weapon, 4, 20, 40_000),
        new("O4", "Reinforced Suit", GearSlot.Outfit, 4, 20, 50_000),
        new("V4", "Specialist Rig", GearSlot.VehicleTool, 4, 20, 60_000),
    ];

    public static GearItem? Find(string? key) =>
        All.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// The one place future PvP reads gear strength from: the best tier <b>owned</b> in each slot,
    /// whether or not it is equipped.
    /// </summary>
    public static GearTiers BestOwnedTiers(IEnumerable<PlayerGear> owned)
    {
        var list = owned as ICollection<PlayerGear> ?? owned.ToList();
        int Best(GearSlot slot) => list.Where(g => g.Slot == slot).Select(g => g.Tier).DefaultIfEmpty(0).Max();
        return new GearTiers(Best(GearSlot.Weapon), Best(GearSlot.Outfit), Best(GearSlot.VehicleTool));
    }
}
