using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Services.Geocoding;
using RTUB.Web.Extensions;
using System.Security.Claims;
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

        // Configure Ranking system
        services.Configure<RTUB.Application.Configuration.RankingConfiguration>(
            builder.Configuration.GetSection(RTUB.Application.Configuration.RankingConfiguration.SectionName));
        
        // Configure XP Settings (same section as RankingConfiguration, but focused on XP values)
        services.Configure<RTUB.Application.Configuration.XpSettings>(
            builder.Configuration.GetSection(RTUB.Application.Configuration.XpSettings.SectionName));

        // Configure App Settings
        services.Configure<RTUB.Application.Configuration.Toggles>(
            builder.Configuration.GetSection(RTUB.Application.Configuration.Toggles.SectionName));

        // Configure Web Push
        services.Configure<RTUB.Application.Configuration.WebPushOptions>(
            builder.Configuration.GetSection(RTUB.Application.Configuration.WebPushOptions.SectionName));

        // Configure Birthday Email Scheduler
        services.Configure<RTUB.Application.Configuration.BirthdayEmailSchedulerOptions>(
            builder.Configuration.GetSection(RTUB.Application.Configuration.BirthdayEmailSchedulerOptions.SectionName));

        // ---------- DB: SQLite only ----------
        var connectionString = builder.Configuration.GetConnectionString("SqliteConnection")
                               ?? "Data Source=app.db";

        // Ensure database directory exists for SQLite
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
        }
        catch (Exception ex)
        {
            // Log but don't fail if directory creation fails - let SQLite handle the error
            Console.WriteLine($"Warning: Could not ensure database directory exists: {ex.Message}");
        }

        services.AddDbContext<ApplicationDbContext>(o =>
        {
            o.UseSqlite(connectionString, b =>
            {
                b.MigrationsAssembly("RTUB");
                // Configure query splitting to prevent N+1 query performance issues when loading multiple collections
                b.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });


        // ---------- Identity ----------
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
            options.User.RequireUniqueEmail = true;
            // Simplified password requirements for easier use by older people
            options.Password.RequiredLength = 4;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireDigit = false;
            // Account lockout settings to protect against brute-force attacks
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        var loginMade = false;
        // Configure cookie authentication to redirect to /login instead of /Account/Login
        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/login";
            options.Events = new CookieAuthenticationEvents
            {
                OnValidatePrincipal = async context =>
                {
                    var logger = context.HttpContext?.RequestServices?.GetRequiredService<ILogger<Program>>();
                    var cache = context.HttpContext?.RequestServices?.GetService<IMemoryCache>();
                    var userManager = context.HttpContext?.RequestServices?.GetService<UserManager<ApplicationUser>>();
                    var signInManager = context.HttpContext?.RequestServices?.GetService<SignInManager<ApplicationUser>>();

                    if (logger == null || cache == null || userManager == null || signInManager == null)
                    {
                        return;
                    }

                    var userName = context.Principal?.Identity?.Name;

                    if (string.IsNullOrWhiteSpace(userName))
                    {
                        return;
                    }

                    // Validate security stamp to force sign-out when credentials or roles change
                    var validatedUser = await signInManager.ValidateSecurityStampAsync(context.Principal);

                    if (validatedUser == null)
                    {
                        await signInManager.SignOutAsync();
                        context.RejectPrincipal();
                        return;
                    }

                    var hasAdminClaim = context.Principal?.IsInRole("Admin") ?? false;
                    var isAdminInDatabase = await userManager.IsInRoleAsync(validatedUser, "Admin");

                    if (hasAdminClaim && !isAdminInDatabase)
                    {
                        // Roles were changed since the cookie was issued; force logout to refresh claims
                        await signInManager.SignOutAsync();
                        context.RejectPrincipal();
                        Console.WriteLine($"User {userName} forced to logout due to role change.");

                        return;
                    }

                    var issuedUtc = context.Properties?.IssuedUtc?.UtcDateTime ?? DateTime.MinValue;

                    // Log user authentication once per session (cache for 1 hour to avoid duplicate logs)
                    // The cache key includes issuedUtc.Ticks to ensure each new login session is logged once
                    var logCacheKey = $"login-log:{userName}:{issuedUtc.Ticks}";

                    if (!cache.TryGetValue(logCacheKey, out _))
                    {
                        logger.LogInformation(
                            "User {UserName} authenticated via cookie validation at {LoginTime}",
                            userName,
                            DateTime.UtcNow);

                        loginMade = true;

                        // Cache for 1 hour to prevent duplicate logs from the same session
                        // This ensures the log appears only once per login session
                        cache.Set(logCacheKey, true, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                        });
                    }

                    try
                    {
                        var db = context.HttpContext?.RequestServices?.GetService<ApplicationDbContext>();
                        if (db is null)
                        {
                            logger.LogWarning("ApplicationDbContext not available in OnValidatePrincipal");
                            return;
                        }

                        // Get user id directly from claims (faster, no extra query)
                        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                        if (string.IsNullOrWhiteSpace(userId))
                        {
                            return;
                        }

                        var now = DateTime.UtcNow;
                        var today = now.Date;

                        // Update LastLoginDate to track user activity (both normal login and cookie validation)
                        // This is throttled by the cache above to prevent excessive DB writes
                        await db.Database.ExecuteSqlInterpolatedAsync($@"
                            UPDATE AspNetUsers
                            SET LastLoginDate = {now}
                            WHERE Id = {userId};");

                        // Track login count per day
                        // Try to increment existing record, or create a new one if it doesn't exist
                        var existingCount = await db.LoginCounts
                            .FirstOrDefaultAsync(lc => lc.UserId == userId && lc.Date == today);

                        if (existingCount != null)
                        {
                            existingCount.Count++;
                            existingCount.UpdatedAt = now;
                            existingCount.UpdatedBy = userName;
                        }
                        else
                        {
                            db.LoginCounts.Add(new RTUB.Core.Entities.LoginCount
                            {
                                UserId = userId,
                                Date = today,
                                Count = 1,
                                CreatedAt = now,
                                CreatedBy = userName
                            });
                        }

                        await db.SaveChangesAsync();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Error while tracking login for {UserName}", userName);
                    }

                }
            };
        });

        // PDF generation service - moved to Application layer
        services.AddScoped<RTUB.Application.Services.ReportPdfService>();

        // Email sender for Identity (forgot password, etc.)
        services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, RTUB.Application.Services.EmailSender>();

        // Add MVC services for Razor view engine (needed for email templates)
        services.AddControllersWithViews();

        // Email template services
        services.AddScoped<RTUB.Web.Services.IEmailTemplateService, RTUB.Web.Services.EmailTemplateService>();
        services.AddScoped<RTUB.Application.Interfaces.IEmailTemplateRenderer, RTUB.Web.Services.RazorEmailTemplateRenderer>();

        // SQL Validation service for Database Viewer
        services.AddScoped<RTUB.Web.Services.ISqlValidationService, RTUB.Web.Services.SqlValidationService>();

        // --------- Cloudflare R2 S3 Client (Singleton) ---------
        // Register a single shared AmazonS3Client with exact config that works with Cloudflare R2
        services.AddSingleton<Amazon.S3.IAmazonS3>(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            var accessKey = configuration["Cloudflare:R2:AccessKeyId"];
            var secretKey = configuration["Cloudflare:R2:SecretAccessKey"];
            var accountId = configuration["Cloudflare:R2:AccountId"];

            if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            {
                var errorMsg = "Cloudflare R2 credentials not configured. Set Cloudflare:R2:AccessKeyId and Cloudflare:R2:SecretAccessKey.";
                logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            if (string.IsNullOrEmpty(accountId))
            {
                var errorMsg = "Cloudflare R2 account ID not configured. Set Cloudflare:R2:AccountId.";
                logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
            var config = new Amazon.S3.AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true,
                AuthenticationRegion = "auto" // Required for Cloudflare R2
            };

            return new Amazon.S3.AmazonS3Client(credentials, config);
        });

        // --------- Repositories (Data Access Layer) ---------
        services.AddRepositories();

        // --------- Application Services (organized by domain) ---------
        services.AddApplicationServices();
        services.AddRehearsalServices();
        services.AddLogisticsServices();
        services.AddMeetingServices();
        services.AddInventoryServices();
        services.AddDiscussionServices();
        services.AddRankingServices();
        services.AddEmailServices();
        services.AddStorageServices();
        services.AddMemberQueryServices();
        services.AddPushNotificationServices();
        services.AddMessagingServices();

        // --------- Mention Service (Social feature) ---------
        services.AddScoped<IMentionService, MentionService>();

        // --------- Geocoding Service ---------
        // Register HttpClient for Nominatim geocoding service
        services.AddHttpClient("Nominatim")
            .ConfigureHttpClient(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });

        // Geocoding queue (singleton - shared state across all requests)
        services.AddSingleton<IGeocodingQueue, InMemoryGeocodingQueue>();

        // NominatimGeocodingService (scoped) - for background worker
        services.AddScoped<NominatimGeocodingService>();

        // CachedGeocodingService - cache-only reads for UI (scoped to work with scoped DbContext)
        // This is the default IGeocodingService used by UI components
        services.AddScoped<IGeocodingService, CachedGeocodingService>();

        // Background worker for geocoding cities from the queue
        services.AddHostedService<BackgroundGeocodingWorker>();

        // Background worker for sending birthday emails automatically
        services.AddHostedService<BirthdayEmailSchedulerService>();

        // --------- UI State Services ---------
        services.AddScoped<RTUB.Web.Services.ProfilePictureUpdateService>();

        // Messaging notification service for server-side Blazor real-time updates
        services.AddSingleton<RTUB.Web.Services.MessagesNotificationService>();

        // Media Session API interop for lock screen / system media overlay
        services.AddScoped<RTUB.Web.Interop.MediaSessionInterop>();
        
        // Audio Player interop for reliable audio playback
        services.AddScoped<RTUB.Web.Interop.AudioPlayerInterop>();
        
        // PWA Helper interop for PWA mode detection
        services.AddScoped<RTUB.Web.Interop.PwaHelperInterop>();
        
        // Media Queue Service for PWA playback queue management
        services.AddScoped<RTUB.Web.Services.MediaQueueService>();

        // ---------- Blazor + Authentication ----------
        services.AddRazorComponents()
                .AddInteractiveServerComponents(options =>
                {
                    options.DetailedErrors = builder.Environment.IsDevelopment();
                    // Configure SignalR for larger messages (image uploads)
                    options.MaxBufferedUnacknowledgedRenderBatches = 10;
                });

        // Configure SignalR hub options for larger messages (image uploads)
        services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
        {
            options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        });

        // Configure circuit options for better stability
        services.AddServerSideBlazor(options =>
        {
            options.DetailedErrors = builder.Environment.IsDevelopment();
        });

        // Configure circuit options to ensure absolute path for SignalR hub
        services.Configure<Microsoft.AspNetCore.Components.Server.CircuitOptions>(options =>
        {
            options.DetailedErrors = builder.Environment.IsDevelopment();
        });

        services.AddCascadingAuthenticationState();

        services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

        services.AddAuthorization(o =>
        {
            o.AddPolicy("RequireAdministratorRole", p => p.RequireRole("Admin"));
        });

        services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

        // Add response compression for better performance
        // Only enable in production to avoid conflicts with BrowserLink/BrowserRefresh dev tools
        if (!builder.Environment.IsDevelopment())
        {
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
                options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
            });
        }

        // Add response caching
        services.AddResponseCaching();

        // Add memory cache for server-side caching (reduces database queries)
        services.AddMemoryCache();

        // --------- .NET 10 Blazor Metrics & Diagnostics ---------
        // Enable Blazor Server metrics for monitoring circuit health and navigation performance
        // These metrics are production-ready and integrate with standard .NET monitoring tools
        services.AddMetrics();

        // Add controller support for API endpoints
        services.AddControllers();

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
                    if (pendingMigrations.Any())
                    {
                        await db.Database.MigrateAsync();
                    }

                    await SeedData.InitializeAsync(sp, builder.Configuration);

                    // Sync default group conversations after seeding
                    var groupSyncService = sp.GetRequiredService<IGroupConversationSyncService>();
                    await groupSyncService.SyncDefaultGroupsAsync();
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

        // LOGIN (HTTP POST) — sets cookie, then redirects
        app.MapPost("/auth/login", async (HttpContext http,
                                          SignInManager<ApplicationUser> signInManager,
                                          UserManager<ApplicationUser> userManager,
                                          ILogger<Program> logger,
                                          AuditContext auditContext,
                                          IMemoryCache cache) =>
        {
            var form = await http.Request.ReadFormAsync();
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
                if (!loginMade)
                {
                    logger.LogInformation("User {UserName} successfully logged in at {LoginTime}",
                        user.UserName,
                        DateTime.UtcNow);
                }

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
        // If you want antiforgery enforced here, replace the next line with: .RequireAntiforgery();
        .DisableAntiforgery();

        // LOGOUT (HTTP POST)
        app.MapPost("/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/");
        }).DisableAntiforgery();

        app.MapRazorComponents<RTUB.App>()
           .AddInteractiveServerRenderMode();

        // Map SignalR hubs
        app.MapHub<RTUB.Web.Hubs.MessagesHub>("/hubs/messages");

        // Map API controllers
        app.MapControllers();

        app.Run();
    }
}