using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        // Seed default games (runs even for existing databases)
        await gameService.SeedDefaultGamesAsync();

        // Development-only: reset passwords, emails and clear push subscriptions
        await ResetDevDataAsync(dbContext, environment, logger);

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

        // My Tuno - sSeed all biome stage enemies for the first time
        await SeedAllBiomeEnemiesAsync(dbContext);
    }

    /// <summary>
    /// Resets development data: clears push subscriptions, resets all passwords to a
    /// common dev password, and normalises emails to {UserName}@rtub.pt.
    /// This method is explicitly guarded to NEVER run in Production.
    /// </summary>
    private static async Task ResetDevDataAsync(
        ApplicationDbContext dbContext,
        IHostEnvironment environment,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        // SAFETY: only execute in Development / local environments — never in Production
        if (environment.IsProduction())
        {
            return;
        }

        Console.WriteLine($"[SeedData] Environment is '{environment.EnvironmentName}' — applying dev-only data reset...");

        // 1. Clear all push subscriptions (prevents stale browser subscriptions in dev)
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM PushSubscriptions");

        // 2. Reset every user's password to the shared dev password
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE AspNetUsers SET PasswordHash = 'AQAAAAIAAYagAAAAEGwYEFQkEX1qRc/y9PN4xCOZJzOrGdT2WJAO/NqxKRR7ifpvA1B/T6o68ves9EGV4A=='");

        // 3. Normalise emails to {UserName}@rtub.pt
        await dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE AspNetUsers SET Email = UserName || '@rtub.pt', NormalizedEmail = UPPER(UserName || '@rtub.pt')");

        Console.WriteLine("[SeedData] Dev-only data reset complete (push subs cleared, passwords & emails reset).");
    }
}
