using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Data;

/// <summary>
/// Seeds the database with initial data such as administrator role and user.
/// </summary>
public static partial class SeedData
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var gameService = scope.ServiceProvider.GetRequiredService<IGameService>();

        // Seed default games (runs even for existing databases)
        await gameService.SeedDefaultGamesAsync();

        // TODO: Remove after next release — one-time fix for characters seeded with 0.01 base crit instead of 0.0
        await FixCriticalChanceDefaultAsync(dbContext);

        if (await dbContext.Users.AnyAsync())
        {
            return; // Data already exists, skip seeding
        }

        // Ensure roles exist (OWNER, ADMIN, MOD, and MEMBER)
        string[] roles = new[] { "Owner", "Admin", "Mod", "Member" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var isEmptyDb = true;

        Console.WriteLine(isEmptyDb ? $"Seeding just a owner..." : $"Seeding initial data...");

        await SeedMembersAsync(configuration, dbContext, userManager, isEmptyDb);


        if (isEmptyDb)
        {
            return;
        }

        await SeedSlideshowsAsync(dbContext);

        await SeedLabelsAsync(dbContext);

        await SeedEventsAsync(dbContext);

        await SeedEnrollmentsAsync(dbContext, userManager);

        await SeedRehearsalsAsync(dbContext, userManager);

        await SeedMusicAsync(dbContext);
    }

    /// <summary>
    /// Fixes characters that were created with a 0.01 (1%) base critical chance
    /// due to a wrong defaultValue in the AddCriticalChanceToCharacter migration.
    /// Sets them to the correct 0.0 (0%) base so that 100 upgrades × 0.005 = 50% cap exactly.
    /// </summary>
    /// <remarks>
    /// TODO: Remove this method and its call in InitializeAsync after the next release,
    /// once all environments have been patched.
    /// </remarks>
    private static async Task FixCriticalChanceDefaultAsync(ApplicationDbContext dbContext)
    {
        var affected = await dbContext.Characters
            .Where(c => c.CriticalChance == 0.01)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CriticalChance, 0.0));

        if (affected > 0)
        {
            Console.WriteLine($"[SeedData] Fixed CriticalChance for {affected} character(s): 0.01 → 0.0");
        }
    }
}
