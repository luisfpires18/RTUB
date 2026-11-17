using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;
using RTUB.Security;
using RTUB.Web.Endpoints;
using RTUB.Web.Extensions;
using ApplicationUser = RTUB.Core.Entities.ApplicationUser;

namespace RTUB;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration
               .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
               .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables();

        // Initialize QuestPDF license early to avoid native library loading issues
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var services = builder.Services;

        // Add HttpContextAccessor for user tracking
        services.AddHttpContextAccessor();
        services.AddScoped<AuditContext>(); // Names the signed-in user on audit rows (e.g. /auth/login)

        // Register all IOptions<T> configuration bindings
        services.AddConfigurationOptions(builder.Configuration);

        // Per-client throttle on POST /auth/login only. Named policy, no global limiter.
        services.AddLoginRateLimiting(builder.Configuration);
        services.AddPublicRequestRateLimiting(builder.Configuration);

        // ---------- DB: SQLite only ----------
        var connectionString = builder.Configuration.GetConnectionString("SqliteConnection")
                               ?? "Data Source=app.db";

        // Ensure database directory exists for SQLite and configure connection string for concurrency
        string finalConnectionString = connectionString;
        try
        {
            var connectionStringBuilder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
            var dbPath = connectionStringBuilder.DataSource;

            if (!string.IsNullOrEmpty(dbPath) && !dbPath.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
            {
                var dbDirectory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
                {
                    Directory.CreateDirectory(dbDirectory);
                }
            }

            // Configure SQLite busy timeout: wait up to 30 seconds if database is locked.
            // Do NOT use Cache=Shared — shared-cache introduces table-level locks that conflict
            // with WAL mode and cause cross-connection deadlocks.
            connectionStringBuilder.DefaultTimeout = 30;
            finalConnectionString = connectionStringBuilder.ToString();
        }
        catch (Exception ex)
        {
            // Log but don't fail if directory creation fails - let SQLite handle the error
            Console.WriteLine($"Warning: Could not ensure database directory exists: {ex.Message}");
        }

        // --------- Database (SQLite + EF Core) ---------
        services.AddDatabaseServices(finalConnectionString);

        // --------- Identity + Cookie Auth ---------
        services.AddIdentityServices();
        services.AddCookieAuthenticationServices();

        // --------- Infrastructure (email, PDF, SQL validation, R2 storage client) ---------
        services.AddInfrastructureServices(builder.Configuration);

        // --------- Repositories (Data Access Layer) ---------
        services.AddRepositories();

        // --------- Application Services (organized by domain) ---------
        services.AddApplicationServices();
        services.AddRehearsalServices();
        services.AddMeetingServices();
        services.AddInventoryServices();
        services.AddDiscussionServices();
        services.AddQuestionServices();
        services.AddRankingServices();
        services.AddFinanceServices();
        services.AddEmailServices();
        services.AddStorageServices(builder.Configuration, builder.Environment);
        services.AddDatabaseBackupServices(builder.Configuration);
        services.AddMemberQueryServices();
        services.AddMemberServices();
        services.AddRoleServices();
        services.AddPushNotificationServices();

        // --------- Social ---------
        services.AddScoped<IMentionService, MentionService>();

        // --------- Geocoding + HTTP clients ---------
        services.AddGeocodingServices();

        // --------- Background workers (schedulers, queues) ---------
        services.AddBackgroundServices();

        // --------- Web infrastructure ---------
        services.AddWebServices(builder.Environment);

        var app = builder.Build();

        // ---------- Migrate + seed ----------
        // Skip migration and seeding in Test environment (integration tests handle this)
        await app.MigrateAndSeedDatabaseAsync(builder.Configuration, connectionString);

        app.UseErrorHandlingAndSecurityHeaders();

        app.UseRoutingStaticFilesAndAuth();

        app.MapSystemAndAuthEndpoints();

        app.MapFeatureEndpoints();

        app.MapPortalShell();

        // Map API controllers
        app.MapControllers();

        app.Run();
    }

    /// <summary>
    /// Where a signed-in member goes: a local return URL (a deep link, a PWA target) always wins; otherwise the (React)
    /// events page. Task 085 (owner request): the 071 landing on "As minhas ações pendentes" for active members is gone;
    /// the page is reached from the menu and the daily reminder push.
    /// </summary>
    internal static string MemberLanding(string? returnUrl) =>
        RTUB.Application.Helpers.UrlHelper.IsLocalUrl(returnUrl) ? returnUrl! : "/events";

    /// <summary>
    /// Why an account may not sign in, whatever the method (password or passkey): <c>Invalid</c> for an
    /// unconfirmed account, <c>Locked</c>, <c>Expelled</c>; null when it may.
    /// </summary>
    internal static async Task<string?> SignInBlockAsync(ApplicationUser user, UserManager<ApplicationUser> userManager)
    {
        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            return "Invalid";
        }

        // Check if account is locked out before any other checks
        if (await userManager.IsLockedOutAsync(user))
        {
            return "Locked";
        }

        // Check if member has been expelled
        return user.IsExpelled ? "Expelled" : null;
    }

    /// <summary>
    /// The shared end of every sign-in once the credential (password or passkey) is verified: reset the failed
    /// count, record the login, issue the cookie, answer { redirect } (JSON) or redirect.
    /// </summary>
    internal static async Task<IResult> CompleteSignInAsync(ApplicationUser user, bool remember, string? returnUrl, bool wantsJson,
        SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ApplicationDbContext db,
        ILogger logger, AuditContext auditContext, IMemoryCache cache)
    {
        // Credential is valid - reset access failed count
        await userManager.ResetAccessFailedCountAsync(user);

        // Credential verified - update last login date BEFORE signing in
        // This ensures the timestamp is set before the authentication cookie is issued
        // This fixes the race condition where users could make requests before LastLoginDate was set
        try
        {
            // Set audit context so the audit log shows the correct user instead of "System"
            auditContext.SetUser(user.UserName, user.Id);

            user.LastLoginDate = DateTime.UtcNow;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                logger.LogWarning("Failed to update LastLoginDate for user {UserId}: {Errors}",
                    user.Id, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            }

            // Task 050: the sign-in history behind the Owner dashboard (LastLoginDate keeps only the latest).
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO LoginEvents (UserId, At, IsSignIn) VALUES ({user.Id}, {user.LastLoginDate}, 1);");

        }
        catch (Exception ex)
        {
            // Log error but don't fail login
            logger.LogError(ex, "Exception while updating LastLoginDate for user {UserId}", user.Id);
        }
        finally
        {
            // Always clear the audit context to prevent leaking to other requests
            auditContext.Clear();
        }

        // Sign in the user using SignInAsync (credential already validated by the caller)
        // Note: We use SignInAsync instead of PasswordSignInAsync to avoid duplicate password validation
        // All security checks (lockout, password validation, failed attempt tracking) are handled above
        await signInManager.SignInAsync(user, remember);

        // Log successful login (use cache to prevent duplicate logs from concurrent requests)
        var loginLogCacheKey = $"login-success:{user.Id}";
        if (!cache.TryGetValue(loginLogCacheKey, out _))
        {
            logger.LogInformation("User {UserName} successfully logged in at {LoginTime}",
                user.UserName,
                DateTime.UtcNow);

            // Cache for 30 seconds to prevent duplicate logs from concurrent login requests
            cache.Set(loginLogCacheKey, true, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            });
        }

        // A local return URL is honoured; anything else (missing, external, malformed) lands on
        // the members' landing page.
        var target = MemberLanding(returnUrl);
        return wantsJson ? Results.Json(new { redirect = target }) : Results.Redirect(target);
    }
}
