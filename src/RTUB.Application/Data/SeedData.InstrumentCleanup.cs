using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Data;

/// <summary>
/// Cleans up removed instruments (Baixo, Flauta, Fagote, Saxofone) from player data.
/// Unequips and deletes forged weapons, deletes inventory parts.
/// Safe to run multiple times (idempotent).
/// </summary>
public static partial class SeedData
{
    /// <summary>
    /// Removes all forged weapons and inventory parts for instruments that have been
    /// removed from the game. Unequips any equipped weapons first.
    /// </summary>
    public static async Task CleanupRemovedInstrumentsAsync(ApplicationDbContext dbContext, ILogger? logger = null)
    {
        var removedPartTypes = InstrumentTypeHelper.RemovedFromGame
            .Select(InstrumentTypeHelper.ToInventoryPartType)
            .Cast<int>()
            .ToList();

        if (removedPartTypes.Count == 0) return;

        // 1. Find all ForgedWeapons made from removed instruments
        var removedWeapons = await dbContext.ForgedWeapons
            .Where(fw => removedPartTypes.Contains((int)fw.SourceInstrument))
            .Select(fw => fw.Id)
            .ToListAsync();

        if (removedWeapons.Count > 0)
        {
            // 2. Unequip any characters using these weapons
            var affectedCharacters = await dbContext.Characters
                .Where(c => c.EquippedWeapon1.HasValue && removedWeapons.Contains(c.EquippedWeapon1.Value))
                .ToListAsync();

            foreach (var character in affectedCharacters)
            {
                character.EquippedWeapon1 = null;
            }

            if (affectedCharacters.Count > 0)
            {
                await dbContext.SaveChangesAsync();
                logger?.LogInformation("Unequipped {Count} characters from removed-instrument weapons", affectedCharacters.Count);
            }

            // 3. Delete the forged weapons
            var weaponsToDelete = await dbContext.ForgedWeapons
                .Where(fw => removedWeapons.Contains(fw.Id))
                .ToListAsync();

            dbContext.ForgedWeapons.RemoveRange(weaponsToDelete);
            await dbContext.SaveChangesAsync();
            logger?.LogInformation("Deleted {Count} forged weapons from removed instruments", weaponsToDelete.Count);
        }

        // 4. Delete inventory parts for removed instruments
        var removedItemTypes = InstrumentTypeHelper.RemovedFromGame
            .Select(InstrumentTypeHelper.ToInventoryPartType)
            .ToList();

        var partsToDelete = await dbContext.InventoryItems
            .Where(ii => removedItemTypes.Contains(ii.Type))
            .ToListAsync();

        if (partsToDelete.Count > 0)
        {
            dbContext.InventoryItems.RemoveRange(partsToDelete);
            await dbContext.SaveChangesAsync();
            logger?.LogInformation("Deleted {Count} inventory parts for removed instruments", partsToDelete.Count);
        }
    }
}
