using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

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
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");

        // Seed default games (runs even for existing databases)
        await gameService.SeedDefaultGamesAsync();

        // Seed all biome stage enemies (runs even for existing databases)
        await SeedAllBiomeEnemiesAsync(dbContext);

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
}
