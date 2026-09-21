using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Services.Geocoding;
using RTUB.Core.Configuration;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;
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
               .AddJsonFile("scaling.config.json", optional: true, reloadOnChange: true)
               .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables();

        // Configure logging to suppress benign circuit/navigation errors
        builder.Logging.AddFilter((category, level) =>
        {
            // Suppress TaskCanceledException errors from CircuitHost (benign navigation cancellations)
            if (category == "Microsoft.AspNetCore.Components.Server.Circuits.CircuitHost" &&
                level == LogLevel.Error)
            {
                return false; // Don't log circuit TaskCanceledException errors
            }
            return true; // Log everything else
        });

        // Initialize QuestPDF license early to avoid native library loading issues
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var services = builder.Services;

        // Add HttpContextAccessor for user tracking
        services.AddHttpContextAccessor();
        services.AddScoped<AuditContext>(); // For audit logging in Blazor InteractiveServer components

        // Register all IOptions<T> configuration bindings
        services.AddConfigurationOptions(builder.Configuration);

        // Per-client throttle on POST /auth/login only. Named policy, no global limiter.
        services.AddLoginRateLimiting(builder.Configuration);

        var myTunoScaling = builder.Configuration
            .GetSection(RTUB.Application.Configuration.MyTunoScalingConfiguration.SectionName)
            .Get<RTUB.Application.Configuration.MyTunoScalingConfiguration>();

        if (myTunoScaling != null)
        {
            MyTunoScaling.Configure(
                myTunoScaling.BaseStats.Level,
                myTunoScaling.BaseStats.XP,
                myTunoScaling.BaseStats.HP,
                myTunoScaling.BaseStats.Power,
                myTunoScaling.BaseStats.Speed,
                myTunoScaling.BaseStats.Defense,
                myTunoScaling.BaseStats.CriticalChance,
                myTunoScaling.LevelScaling.MaxLevel,
                myTunoScaling.LevelScaling.BonusPerLevel,
                myTunoScaling.LevelScaling.PostPiggiesStartLevel,
                myTunoScaling.LevelScaling.PostPiggiesBonusPerLevel,
                myTunoScaling.LevelScaling.XpPerLevelBase,
                myTunoScaling.LevelScaling.XpGrowthExponent,
                myTunoScaling.Upgrades.HP.FlatBonus,
                myTunoScaling.Upgrades.Power.FlatBonus,
                myTunoScaling.Upgrades.Speed.FlatBonus,
                myTunoScaling.Upgrades.CriticalChance.FlatBonus,
                myTunoScaling.Upgrades.Defense.FlatBonus,
                myTunoScaling.Combat.DefenseK,
                myTunoScaling.Combat.MinDamage,
                myTunoScaling.Combat.CriticalChanceCap,
                myTunoScaling.Combat.CritMultiplier,
                myTunoScaling.Combat.ShotBuffMultiplier,
                baseMaxEnergy: 10,
                energyAmountPerUpgrade: myTunoScaling.Improvements.EnergyAmount.FlatBonus,
                baseRegenInterval: myTunoScaling.Gathering.RegenIntervalSeconds,
                regenReductionPerUpgrade: myTunoScaling.Improvements.EnergyRegen.FlatBonus,
                baseCastTime: myTunoScaling.Gathering.CastTimeSeconds,
                castTimeReductionPerUpgrade: myTunoScaling.Improvements.CastSpeed.FlatBonus,
                minCastTime: myTunoScaling.Improvements.MinCastTime,
                finoHealPercent: myTunoScaling.Consumables.FinoHealPercent,
                finoCooldownSeconds: myTunoScaling.Consumables.FinoCooldownSeconds,
                canecaHealPercent: myTunoScaling.Consumables.CanecaHealPercent,
                canecaCooldownSeconds: myTunoScaling.Consumables.CanecaCooldownSeconds,
                shotBuffRuns: myTunoScaling.Consumables.ShotBuffRuns,
                shotBuffMultiplierConsumable: myTunoScaling.Consumables.ShotBuffMultiplier,
                cigarroBuffRuns: myTunoScaling.Consumables.CigarroBuffRuns,
                cigarroDodgeChance: myTunoScaling.Consumables.CigarroDodgeChance,
                canhaoBuffMinutes: myTunoScaling.Consumables.CanhaoBuffMinutes,
                penaltyBuffMinutes: myTunoScaling.Consumables.PenaltyBuffMinutes,
                penaltyLifestealPercent: myTunoScaling.Consumables.PenaltyLifestealPercent,
                rareSetCritPerPiece: myTunoScaling.StageMode.RareSet.CritBonusPerPiece,
                rareSetSpeedPerPiece: myTunoScaling.StageMode.RareSet.SpeedReductionPerPiece,
                rareSetBonusMinPieces: myTunoScaling.StageMode.RareSet.SetBonusMinPieces,
                rareSetBonusCrit: myTunoScaling.StageMode.RareSet.SetBonusCrit,
                rareSetBonusSpeedReduction: myTunoScaling.StageMode.RareSet.SetBonusSpeedReduction,
                doubleGatheringChancePerUpgrade: myTunoScaling.Improvements.DoubleGathering.FlatBonus,
                maxDoubleGatheringChance: myTunoScaling.Improvements.MaxDoubleGatheringChance,
                cigarroDodgePerUpgrade: 0.04,
                maxCigarroDodge: 0.25,
                shotBuffPerUpgrade: 0.05,
                maxShotBuffMultiplier: 1.30,
                canhaoMinutesPerUpgrade: 1,
                maxCanhaoMinutes: 5,
                penaltyMinutesPerUpgrade: 1,
                maxPenaltyMinutes: 5,
                penaltyLifestealPerUpgrade: 0.003333,
                maxPenaltyLifesteal: 0.015,
                upgradeGrowthRate: myTunoScaling.Upgrades.UpgradeGrowthRate);
        }

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
            // with WAL mode and cause cross-connection deadlocks in Blazor Server.
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
        services.AddLogisticsServices();
        services.AddMeetingServices();
        services.AddInventoryServices();
        services.AddDiscussionServices();
        services.AddQuestionServices();
        services.AddRankingServices();
        services.AddGameServices();
        services.AddFinanceServices();
        services.AddBettingServices();
        services.AddEmailServices();
        services.AddStorageServices();
        services.AddDatabaseBackupServices(builder.Configuration);
        services.AddMemberQueryServices();
        services.AddMemberServices();
        services.AddRoleServices();
        services.AddPushNotificationServices();
        services.AddMessagingServices();

        // --------- Social ---------
        services.AddScoped<IMentionService, MentionService>();

        // --------- Geocoding + HTTP clients ---------
        services.AddGeocodingServices();

        // --------- Background workers (schedulers, queues) ---------
        services.AddBackgroundServices();

        // --------- UI State, Interop, Blazor + Web infrastructure ---------
        services.AddWebUiServices();
        services.AddBlazorAndWebServices(builder.Environment);

        var app = builder.Build();

        // ---------- Migrate + seed ----------
        // Skip migration and seeding in Test environment (integration tests handle this)
        if (app.Environment.EnvironmentName != "Test")
        {
            using (var scope = app.Services.CreateScope())
            {
                var sp = scope.ServiceProvider;
                var logger = sp.GetRequiredService<ILogger<Program>>();

                try
                {
                    var db = sp.GetRequiredService<ApplicationDbContext>();

                    // Only migrate if there are pending migrations (performance optimization)
                    var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
                    bool hadMemberStatusMigration = pendingMigrations.Any(m =>
                        m.Contains("AddMemberStatusTable") ||
                        m.Contains("AddTotalActivitiesCountToMemberStatus"));
                    bool hadCompressUpgradeLevels = pendingMigrations.Any(m =>
                        m.Contains("CompressUpgradeLevels"));

                    if (pendingMigrations.Any())
                    {
                        await db.Database.MigrateAsync();
                        logger.LogInformation("Database migrations applied successfully");
                    }

                    await SeedData.InitializeAsync(sp, builder.Configuration);

                    // Seed item type configs (weapons, drinks, equipment)
                    var itemTypeConfigInitializer = sp.GetRequiredService<ItemTypeConfigInitializer>();
                    await itemTypeConfigInitializer.InitializeAsync();

                    // Sync default group conversations after seeding
                    var groupSyncService = sp.GetRequiredService<IGroupConversationSyncService>();
                    await groupSyncService.SyncDefaultGroupsAsync();

                    // Initialize MemberStatus table if the migration just ran
                    // This ensures "Gestao de membros ativos" has data immediately after deployment
                    // instead of waiting for the scheduled daily update
                    if (hadMemberStatusMigration)
                    {
                        try
                        {
                            logger.LogInformation("MemberStatus migration detected. Starting initial population...");
                            var memberStatusService = sp.GetRequiredService<IMemberStatusService>();
                            var updatedCount = await memberStatusService.UpdateAllMemberStatusesAsync();
                            logger.LogInformation("Initial MemberStatus population completed. Updated {Count} members", updatedCount);
                        }
                        catch (Exception ex)
                        {
                            // Log error but don't fail startup - scheduled update will populate later
                            logger.LogError(ex, "Failed to populate MemberStatus table on startup. Data will be populated during next scheduled update.");
                        }
                    }

                    // Recalculate weapon and equipment stats after upgrade level compression
                    if (hadCompressUpgradeLevels)
                    {
                        try
                        {
                            logger.LogInformation("CompressUpgradeLevels migration detected. Recalculating weapon and equipment stats...");
                            var inventoryService = sp.GetRequiredService<IInventoryService>();
                            var scalingConfig = sp.GetRequiredService<IOptions<MyTunoScalingConfiguration>>().Value;

                            // Recalculate all forged weapon stats with new per-level bonuses
                            var weapons = await db.ForgedWeapons.Where(w => w.Level > 0).ToListAsync();
                            var hpPerLvl = scalingConfig.StageMode.EquipmentHpPerLevel;
                            var powPerLvl = scalingConfig.StageMode.EquipmentPowerPerLevel;
                            var defPerLvl = scalingConfig.StageMode.EquipmentDefensePerLevel;
                            var forging = scalingConfig.StageMode.Forging;

                            foreach (var weapon in weapons)
                            {
                                var baseStats = scalingConfig.StageMode.EquipmentStats.Instrument;
                                var drinkResource = scalingConfig.Gathering.Resources
                                    .FirstOrDefault(r => r.Type == weapon.SourceDrink.ToString());
                                var drinkEnergyCost = drinkResource?.EnergyCost ?? 1;
                                var drinkTierMult = 1.0 + (drinkEnergyCost - 1) * forging.DrinkStatBonusPerTier;
                                var handedMult = weapon.IsTwoHanded ? forging.TwoHandedMultiplier : 1.0;
                                var scaleMult = drinkTierMult * handedMult;

                                weapon.BonusHP = (int)Math.Round((baseStats.HP + weapon.Level * hpPerLvl) * scaleMult);
                                weapon.BonusPower = (int)Math.Round((baseStats.Power + weapon.Level * powPerLvl) * scaleMult);
                                weapon.BonusDefense = (int)Math.Round((baseStats.Defense + weapon.Level * defPerLvl) * scaleMult);
                            }
                            await db.SaveChangesAsync();
                            logger.LogInformation("Recalculated stats for {Count} weapons", weapons.Count);

                            // Recalculate equipment bonuses for all characters
                            var userIds = await db.Characters.Select(c => c.UserId).ToListAsync();
                            foreach (var userId in userIds)
                            {
                                await inventoryService.RecalculateEquipmentBonusesForUserAsync(userId);
                            }
                            logger.LogInformation("Recalculated equipment bonuses for {Count} characters", userIds.Count);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to recalculate weapon/equipment stats after migration. Stats may be stale until next equipment change.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while migrating or seeding the database");
                    throw;
                }
            }
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        // --------- Security headers ---------
        // One central place. Runs before static files, the Blazor 404 short-circuit and the
        // endpoints, so every response carries these. It also runs again when
        // UseExceptionHandler (registered above) re-executes the pipeline, which is why the
        // headers are assigned by indexer rather than appended - reassignment is idempotent.
        //
        // Strict-Transport-Security is NOT set here: UseHsts above already emits it in every
        // non-Development environment, and production returns max-age=2592000 today.
        //
        // There is deliberately NO Content-Security-Policy yet. The script-side blockers are
        // now cleared: unit 022 removed all 15 JSRuntime.InvokeAsync("eval", ...) calls, and
        // unit 023 removed every inline <script> block and inline on* handler attribute, so
        // script-src no longer needs 'unsafe-eval' or 'unsafe-inline'. What remains is
        // style-src: the app still ships inline <style> blocks and style="..." attributes,
        // so a policy today would need 'unsafe-inline' for styles. Enabling CSP is unit 024.
        // See STATE.md for the enumerated blockers.
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            // The app serves user-uploaded media and JSON/manifest documents; stop MIME sniffing.
            headers["X-Content-Type-Options"] = "nosniff";

            // Full URL to same-origin, origin only to other https origins, nothing on downgrade.
            // Matters for the target="_blank" links out to YouTube/Spotify.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            // RTUB never embeds itself: no window.top/window.parent logic, the PWA is
            // display:standalone and the Android TWA uses a Custom Tab, not a frame. The app's
            // own <iframe>s embed R2-hosted PDFs, whose headers are R2's, not these. So DENY is
            // safe and strictly better than SAMEORIGIN for a Blazor Server circuit.
            headers["X-Frame-Options"] = "DENY";

            // Only features verified unused across wwwroot/js, Pages and Shared. `fullscreen`
            // (PDF viewer iframes use allow="fullscreen") and `clipboard-write`
            // (Share.razor, clipboardCopy.js) are in use and are deliberately left alone, as is
            // the long tail of exotic features, where a blanket deny buys nothing measurable.
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            await next();
        });

        // Only use HTTPS redirection in development
        // In production (Azure App Service), HTTPS is handled at the load balancer level
        if (app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        // Enable response compression (must be before UseStaticFiles)
        // Only enable in production to avoid conflicts with BrowserLink/BrowserRefresh dev tools
        if (!app.Environment.IsDevelopment())
        {
            app.UseResponseCompression();
        }

        // Enable response caching
        app.UseResponseCaching();

        app.UseRouting();

        // Must follow UseRouting so the endpoint's RequireRateLimiting metadata is resolved, and
        // precedes authentication/antiforgery so a throttled client is answered 429 before any
        // credential or token work is done.
        app.UseRateLimiter();

        // Serve static files EXCEPT /images/* (handled by ImagesController for E-Tag support)
        // We'll serve /images through the controller, all other static content through middleware
        app.UseWhen(
            context => !context.Request.Path.StartsWithSegments("/images"),
            appBuilder =>
            {
                // Configure content type provider to serve .webmanifest and .well-known files with correct MIME types
                var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
                provider.Mappings[".webmanifest"] = "application/manifest+json";
                // Ensure .well-known/assetlinks.json is served as application/json for Android Digital Asset Links
                provider.Mappings[".json"] = "application/json";

                appBuilder.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
                {
                    ContentTypeProvider = provider,
                    OnPrepareResponse = ctx =>
                    {
                        var path = ctx.Context.Request.Path.Value?.ToLowerInvariant() ?? "";

                        // Digital Asset Links for Android TWA - minimal caching for verification
                        // FileExtensionContentTypeProvider handles Content-Type automatically
                        if (path.Equals("/.well-known/assetlinks.json"))
                        {
                            ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=3600");
                        }
                        // Service Worker must always be revalidated so browsers detect updates immediately
                        // Without this, the SW file gets the 30-day cache and users see stale UI
                        else if (path.EndsWith("service-worker.js"))
                        {
                            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache");
                        }
                        // PWA icons and manifest should have shorter cache to allow updates
                        else if (path.Contains("/icons/") || path.EndsWith("manifest.json") || path.EndsWith("manifest.webmanifest") || path.StartsWith("/apple-touch-icon"))
                        {
                            // Cache for 1 hour with must-revalidate to ensure updates are picked up
                            ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=3600,must-revalidate");
                        }
                        // Cache other static files for 30 days in production
                        else if (!app.Environment.IsDevelopment())
                        {
                            ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=2592000");
                        }
                    }
                });
            });

        // Handle Blazor 404s gracefully - placed after UseRouting
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value;

            // Handle initializers requests
            if (path?.EndsWith("/_blazor/initializers") == true)
            {
                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("[]");
                return;
            }

            await next();
        });

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        // --------- Health Checks ---------
        app.MapHealthChecks("/health");

        // LOGIN (HTTP POST) — sets cookie, then redirects
        // RequireRateLimiting below caps attempts per client IP; it complements, and does not
        // replace, Identity's per-account lockout. See AddLoginRateLimiting.
        // The IFormCollection parameter makes this endpoint an antiforgery-protected form
        // endpoint: the framework requires a valid token and returns 400 before the handler
        // runs. Do not replace it with HttpContext.Request.ReadFormAsync() — that silently
        // removes CSRF protection. The token is rendered by <AntiforgeryToken /> in Login.razor.
        app.MapPost("/auth/login", async (IFormCollection form,
                                          SignInManager<ApplicationUser> signInManager,
                                          UserManager<ApplicationUser> userManager,
                                          ApplicationDbContext db,
                                          ILogger<Program> logger,
                                          AuditContext auditContext,
                                          IMemoryCache cache) =>
        {
            var username = form["Username"].ToString();
            var password = form["Password"].ToString();
            var rememberRaw = form["RememberMe"].ToString();
            var remember = bool.TryParse(rememberRaw, out var b) ? b : string.Equals(rememberRaw, "on", StringComparison.OrdinalIgnoreCase);
            var returnUrl = form["ReturnUrl"].ToString();

            var user = await userManager.FindByNameAsync(username);

            // If not found by username, try to find by email (for users who might enter their email)
            if (user is null && username.Contains("@"))
            {
                user = await userManager.FindByEmailAsync(username);
            }

            if (user is null || !await userManager.IsEmailConfirmedAsync(user))
            {
                return Results.Redirect("/login?error=Invalid");
            }

            // Check if account is locked out before any other checks
            if (await userManager.IsLockedOutAsync(user))
            {
                return Results.Redirect("/login?error=Locked");
            }

            // Check if member has been expelled
            if (user.IsExpelled)
            {
                return Results.Redirect("/login?error=Expelled");
            }

            // Check password is valid BEFORE updating last login date to avoid race condition
            // We need to verify credentials first, then update the timestamp BEFORE signing in
            // to ensure the LastLoginDate is persisted before the user can make any requests
            var passwordValid = await userManager.CheckPasswordAsync(user, password);
            if (!passwordValid)
            {
                // Track failed login attempt for lockout purposes
                await userManager.AccessFailedAsync(user);
                return Results.Redirect("/login?error=Invalid");
            }

            // Password is valid - reset access failed count
            await userManager.ResetAccessFailedCountAsync(user);

            // Password verified - update last login date BEFORE signing in
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

            // Sign in the user using SignInAsync (password already validated above)
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

            // Validate and redirect to return URL if provided and is a local URL, otherwise redirect to home
            if (!string.IsNullOrEmpty(returnUrl) && RTUB.Application.Helpers.UrlHelper.IsLocalUrl(returnUrl))
            {
                return Results.Redirect(returnUrl);
            }
            return Results.Redirect("/");
        })
        .RequireRateLimiting(RTUB.Web.Extensions.ServiceCollectionExtensions.LoginRateLimitPolicy);

        // LOGOUT (HTTP POST)
        // The unused IFormCollection parameter is what enables antiforgery validation — see the
        // note on /auth/login. The token is rendered by <AntiforgeryToken /> in MainLayout.razor.
        app.MapPost("/auth/logout", async (IFormCollection form,
                                           SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/");
        });

        app.MapRazorComponents<RTUB.App>()
           .AddInteractiveServerRenderMode();

        // Map SignalR hubs
        app.MapHub<RTUB.Web.Hubs.MessagesHub>("/hubs/messages");

        // Map API controllers
        app.MapControllers();

        app.Run();
    }
}
