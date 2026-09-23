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

        // Opt-in, Development-only: reset passwords, emails and clear push subscriptions.
        // Disabled by default — a normal startup never touches existing credentials.
        await ResetDevDataAsync(configuration, dbContext, userManager, environment, logger);

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

        // Which seed runs against a fresh database. Opt-in, so an unset flag keeps the behaviour
        // this had when it was a hardcoded `true` — production included.
        //   true  = bootstrap the Owner account only (the normal path, incl. production).
        //   false = also seed the full development member dataset in SeedData.Member.cs. Azure
        //           DEV opts in; hardcoding it would arm the bulk seed on any fresh database.
        //
        // Three conditions must all hold before a member is created this way: no users exist
        // (checked above), the flag is true, and SeedData:MemberPassword is set — SeedMembersAsync
        // throws otherwise, and there is no default member password.
        //
        // Accepts "true"/"false" only, case-insensitive; absent is false. Anything else ("1",
        // "yes", "") throws out of GetValue<bool> and the app refuses to start, which is the
        // right failure — a typo stops the host rather than silently picking a seed.
        var isEmptyDb = !configuration.GetValue<bool>("SeedData:SeedFullDataset");

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
    /// Destructive, opt-in development-data reset: clears push subscriptions, resets every
    /// user's password to the configured development reset password, and normalises emails to
    /// {UserName}@rtub.pt.
    ///
    /// It runs only when the host environment is Development <b>or Staging</b> (the Azure DEV
    /// App Service) <b>and</b> <c>DevelopmentDataReset:Enabled</c> is true. Production, Test and
    /// any other environment never run it, whatever the configuration says, and it is disabled by
    /// default in the allowed environments too, so a normal startup leaves seeded credentials
    /// intact.
    ///
    /// Internal rather than private so a test can drive it explicitly; the environment guard
    /// keeps it inert on the normal test-host startup path.
    /// </summary>
    internal static async Task ResetDevDataAsync(
        IConfiguration configuration,
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IHostEnvironment environment,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        // SAFETY: an ALLOW-LIST, deliberately, not a "not Production" check. Only Development and
        // Staging can ever reach the rest of this method; Production, Test and any environment
        // name nobody has thought of yet return here, and no configuration can switch them on.
        //
        // Staging is the Azure DEV App Service, whose users exist to be reset to one shared
        // development password on demand. It was excluded until unit 027, which is why Azure DEV
        // could never run this.
        //
        // This check comes FIRST on purpose: a disallowed environment must not so much as read
        // DevelopmentDataReset:Password, let alone touch a credential or a push subscription.
        if (!environment.IsDevelopment() && !environment.IsStaging())
        {
            return;
        }

        // Opt-in. Absent, unparseable or false all mean "do not touch anything".
        if (!bool.TryParse(configuration["DevelopmentDataReset:Enabled"], out var enabled) || !enabled)
        {
            return;
        }

        // Validated before the first mutation: a missing password must leave the database exactly
        // as it was, not half reset. There is no fallback value, and the supplied one is never logged.
        var resetPassword = configuration["DevelopmentDataReset:Password"];
        RequireSeedPassword(resetPassword, "DevelopmentDataReset:Password", "reset development data");

        logger.LogWarning(
            "[SeedData] DevelopmentDataReset is enabled in environment '{Environment}' - clearing push "
            + "subscriptions and resetting every user's password and email.",
            environment.EnvironmentName);

        // 1. Clear all push subscriptions (prevents stale browser subscriptions in dev).
        // Materialised first so the change tracker is not mutated mid-enumeration, and done
        // through EF rather than raw SQL so it works on every provider and is audited like any
        // other delete.
        var subscriptions = await dbContext.PushSubscriptions.ToListAsync();
        dbContext.PushSubscriptions.RemoveRange(subscriptions);
        await dbContext.SaveChangesAsync();

        // 2. Reset every user's password and normalise their email.
        var users = await dbContext.Users.ToListAsync();
        foreach (var user in users)
        {
            // Identity's own password-reset flow, in one step. Deliberately NOT
            // RemovePasswordAsync followed by AddPasswordAsync: that is a two-step credential
            // mutation, and a failure between the two would leave the account with no password
            // at all. ResetPasswordAsync validates first and only then writes, so a failure
            // leaves the existing password intact.
            //
            // It also routes through UpdatePasswordHash, so the configured IPasswordHasher
            // produces the hash and the security stamp is rotated — any authentication cookie
            // issued before the reset stops validating.
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var reset = await userManager.ResetPasswordAsync(user, token, resetPassword!);
            if (!reset.Succeeded)
            {
                // Identity's error codes and descriptions never echo the password or the token.
                throw new InvalidOperationException(
                    $"Development data reset failed to set the password for '{user.UserName}': "
                    + string.Join(", ", reset.Errors.Select(e => $"{e.Code}: {e.Description}")));
            }

            // Assigned after the reset so a rejected password does not rewrite the email either,
            // and persisted by its own update.
            user.Email = $"{user.UserName}@rtub.pt";
            user.NormalizedEmail = userManager.NormalizeEmail(user.Email);

            var updated = await userManager.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Development data reset failed to normalise the email for '{user.UserName}': "
                    + string.Join(", ", updated.Errors.Select(e => $"{e.Code}: {e.Description}")));
            }
        }

        logger.LogWarning(
            "[SeedData] Development data reset complete - push subscriptions cleared, {UserCount} "
            + "user password(s) and email(s) reset.",
            users.Count);
    }
}
