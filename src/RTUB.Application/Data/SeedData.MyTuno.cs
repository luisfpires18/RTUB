using Microsoft.EntityFrameworkCore;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    // TODO: Remove this method once all player inventories have been corrected in production.
    /// <summary>
    /// One-time fix: resets all players' consumable quantities to intended values.
    /// If a player owns any amount of a consumable, it is clamped to the target quantity.
    /// Players who don't own the consumable are left untouched.
    /// </summary>
    public static async Task ResetConsumableQuantitiesAsync(ApplicationDbContext dbContext)
    {
        var targetQuantities = new Dictionary<InventoryItemType, int>
        {
            { InventoryItemType.Fino,    10 },
            { InventoryItemType.Shot,     5 },
            { InventoryItemType.Cigarro,  5 },
            { InventoryItemType.Caneca,   3 },
            { InventoryItemType.Canhao,   3 },
            { InventoryItemType.Penalty,  1 },
        };

        var consumableTypes = targetQuantities.Keys.ToList();

        var itemsToFix = await dbContext.InventoryItems
            .Where(i => consumableTypes.Contains(i.Type))
            .ToListAsync();

        var updated = 0;

        foreach (var item in itemsToFix)
        {
            var target = targetQuantities[item.Type];
            if (item.Quantity != target)
            {
                item.Quantity = target;
                updated++;
            }
        }

        if (updated > 0)
        {
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"[SeedData] Reset {updated} consumable inventory rows to intended quantities.");
        }
    }
}
