using Microsoft.EntityFrameworkCore;
using RTUB.Core.Entities;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    /// <summary>
    /// If a character's ActionTime is already at the 1.0s floor,
    /// set SpeedUpgrades to max (40) so the UI shows it as maxed and locked.
    /// </summary>
    // TODO: REMOVE AFTER NEXT RELEASE. This only needs to run once.
    public static async Task CorrectSpeedUpgradesAsync(ApplicationDbContext dbContext)
    {
        const int maxSpeedUpgrades = 40;

        var characters = await dbContext.Characters
            .Where(c => c.SpeedUpgrades < maxSpeedUpgrades)
            .ToListAsync();

        var corrected = 0;

        foreach (var character in characters)
        {
            if (character.ActionTime <= Character.MinActionTime)
            {
                character.SpeedUpgrades = maxSpeedUpgrades;
                corrected++;
            }
        }

        if (corrected > 0)
        {
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"Set SpeedUpgrades to {maxSpeedUpgrades} for {corrected} character(s) already at min action time.");
        }
    }
}
