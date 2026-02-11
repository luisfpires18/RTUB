using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Core.Entities;

namespace RTUB.Application.Data;

public static partial class SeedData
{
    /// <summary>
    /// Logs speed calculations for all characters (for verification after speed formula changes).
    /// Add to InitializeAsync after SeedDefaultGamesAsync if you want to see the logs.
    /// </summary>
    public static async Task LogAllCharacterSpeedAsync(ApplicationDbContext dbContext, ILogger? logger = null)
    {
        var characters = await dbContext.Set<Character>()
            .Include(c => c.User)
            .Where(c => c.SpeedUpgrades > 0)
            .OrderByDescending(c => c.SpeedUpgrades)
            .ToListAsync();

        if (!characters.Any())
        {
            logger?.LogInformation("No characters with speed upgrades found");
            return;
        }

        logger?.LogInformation("=== Character Speed Calculations ===");
        
        foreach (var character in characters)
        {
            var userName = character.User?.UserName ?? character.UserId;
            var logMessage = character.GetSpeedChangeLog(userName);
            logger?.LogInformation(logMessage);
        }
        
        logger?.LogInformation("=== End Speed Log ===");
    }
}
