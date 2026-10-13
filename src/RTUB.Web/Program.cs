using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Configuration;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Services.Geocoding;
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

                    if (pendingMigrations.Any())
                    {
                        // An existing database is about to change shape: take a restore point
                        // first. Throws on failure, and the catch below rethrows, so a database
                        // is never migrated without one. A fresh database has nothing to protect.
                        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
                        if (appliedMigrations.Any())
                        {
                            var snapshot = PreMigrationSnapshot.Take(
                                connectionString, pendingMigrations.Last(), DateTime.UtcNow);
                            logger.LogInformation(
                                "Pre-migration snapshot {Snapshot} taken before applying {Count} migration(s) after {LastApplied}",
                                snapshot, pendingMigrations.Count(), appliedMigrations.Last());
                        }

                        await db.Database.MigrateAsync();
                        logger.LogInformation("Database migrations applied successfully");
                    }

                    await SeedData.InitializeAsync(sp, builder.Configuration);

                    // Initialize MemberStatus table if the migration just ran
                    // This ensures "Gestao de membros ativos" has data immediately after deployment
                    // instead of waiting for the scheduled daily update
                    if (hadMemberStatusMigration)
                    {
                        try
                        {
                            logger.LogInformation("MemberStatus migration detected. Starting initial population...");
                            var memberStatusService = sp.GetRequiredService<IMemberStatusService>();
                            var run = await memberStatusService.ReconcileAllMemberStatusesAsync();
                            logger.LogInformation("Initial MemberStatus population completed: {Run}", run);
                        }
                        catch (Exception ex)
                        {
                            // Log error but don't fail startup - scheduled update will populate later
                            logger.LogError(ex, "Failed to populate MemberStatus table on startup. Data will be populated during next scheduled update.");
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
        // One central place. Runs before static files and the endpoints, so every response carries these. It also runs again when
        // UseExceptionHandler (registered above) re-executes the pipeline, which is why the
        // headers are assigned by indexer rather than appended - reassignment is idempotent.
        //
        // Strict-Transport-Security is NOT set here: UseHsts above already emits it in every
        // non-Development environment, and production returns max-age=2592000 today.
        //
        // Content-Security-Policy is ENFORCED (unit 025), with no 'unsafe-inline' and no
        // 'unsafe-eval': unit 022 removed all 15 JSRuntime.InvokeAsync("eval", ...) calls,
        // unit 023 removed every inline <script> block and inline on* handler attribute, and
        // unit 024 removed every inline <style> block and style="..." attribute from
        // browser-served markup. Every source in the policy is evidence-based - see
        // ContentSecurityPolicyBuilder and STATE.md for the per-directive justification.
        //
        // It is emitted on HTML DOCUMENT responses only, deliberately. A CSP header served with
        // a worker script governs that worker's own execution context, and RTUB's service
        // worker re-fetches the cross-origin subresources it caches (R2 media, the script/style
        // CDNs, the Leaflet tiles) - none of which connect-src lists, because the page itself
        // never fetches them. A blanket policy would therefore break offline caching. On other
        // subresource responses the header buys nothing: the directives that matter are already
        // enforced by the embedding document's own policy at fetch time, and frame-ancestors
        // applies only to documents (X-Frame-Options: DENY above covers every response anyway).
        //
        // Set from OnStarting because Content-Type is not known when this middleware runs.
        var contentSecurityPolicy = new ContentSecurityPolicyBuilder(app.Configuration);

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
            // safe and strictly better than SAMEORIGIN.
            headers["X-Frame-Options"] = "DENY";

            // Only features verified unused across wwwroot/js, Pages and Shared. `fullscreen`
            // (PDF viewer iframes use allow="fullscreen") and `clipboard-write`
            // (clipboardCopy.js) are in use and are deliberately left alone, as is
            // the long tail of exotic features, where a blanket deny buys nothing measurable.
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            context.Response.OnStarting(static state =>
            {
                var (ctx, policy) = ((HttpContext, ContentSecurityPolicyBuilder))state;
                var contentType = ctx.Response.ContentType;

                if (contentType is not null &&
                    contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    // Assigned, never appended: UseExceptionHandler re-executes the pipeline and
                    // registers this callback a second time on the same response.
                    ctx.Response.Headers["Content-Security-Policy"] =
                        policy.Build();
                }

                return Task.CompletedTask;
            }, (context, contentSecurityPolicy));

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

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        // --------- Health Checks ---------
        app.MapHealthChecks("/health");

        // --------- Build identity ---------
        // Deploy and rollback smoke tests poll this until the exact version AND commit they just
        // deployed answer, which is what proves the new build is serving rather than an old
        // instance that is still up. Under /api/, so the service worker never caches it.
        app.MapGet("/api/version", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Json(RTUB.Web.Services.BuildInfo.Current);
        }).AllowAnonymous();

        // --------- System announcement (task 037) ---------
        // The Owner's latest announcement while it is fresh (AnnouncementService), for every open React page, signed in or
        // not, as the Blazor banner was. 204 when there is none. Polled, so never stored by anything.
        app.MapGet("/api/announcement", (HttpContext context, RTUB.Web.Services.AnnouncementService announcements) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return announcements.Current is { } current ? Results.Json(current) : Results.NoContent();
        }).AllowAnonymous();

        // LOGIN (HTTP POST) — sets cookie, then redirects
        // RequireRateLimiting below caps attempts per client IP; it complements, and does not
        // replace, Identity's per-account lockout. See AddLoginRateLimiting.
        // The IFormCollection parameter makes this endpoint an antiforgery-protected form
        // endpoint: the framework requires a valid token and returns 400 before the handler
        // runs. Do not replace it with HttpContext.Request.ReadFormAsync() — that silently
        // removes CSRF protection. The React /login (React track 007) takes the token from
        // GET /api/public/antiforgery-token and posts with Accept: application/json, so every
        // outcome below answers as JSON ({ error } with 401, or { redirect }) instead of a
        // redirect. Same checks, same order, either way.
        app.MapPost("/auth/login", async (HttpContext context,
                                          IFormCollection form,
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

            var wantsJson = context.Request.GetTypedHeaders().Accept
                .Any(a => a.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase));
            IResult Fail(string error) => wantsJson
                ? Results.Json(new { error }, statusCode: StatusCodes.Status401Unauthorized)
                : Results.Redirect($"/login?error={error}");

            var user = await userManager.FindByNameAsync(username);

            // If not found by username, try to find by email (for users who might enter their email)
            if (user is null && username.Contains("@"))
            {
                user = await userManager.FindByEmailAsync(username);
            }

            if (user is null || !await userManager.IsEmailConfirmedAsync(user))
            {
                return Fail("Invalid");
            }

            // Check if account is locked out before any other checks
            if (await userManager.IsLockedOutAsync(user))
            {
                return Fail("Locked");
            }

            // Check if member has been expelled
            if (user.IsExpelled)
            {
                return Fail("Expelled");
            }

            // Check password is valid BEFORE updating last login date to avoid race condition
            // We need to verify credentials first, then update the timestamp BEFORE signing in
            // to ensure the LastLoginDate is persisted before the user can make any requests
            var passwordValid = await userManager.CheckPasswordAsync(user, password);
            if (!passwordValid)
            {
                // Track failed login attempt for lockout purposes
                await userManager.AccessFailedAsync(user);
                return Fail("Invalid");
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

            // A local return URL is honoured; anything else (missing, external, malformed) lands on
            // the members' landing page.
            var target = MemberLanding(returnUrl);
            return wantsJson ? Results.Json(new { redirect = target }) : Results.Redirect(target);
        })
        .RequireRateLimiting(RTUB.Web.Extensions.ServiceCollectionExtensions.LoginRateLimitPolicy);

        // LOGOUT (HTTP POST)
        // The unused IFormCollection parameter is what enables antiforgery validation — see the
        // note on /auth/login. The React member shell posts it (api.ts signOut, token from /api/public/antiforgery-token).
        app.MapPost("/auth/logout", async (IFormCollection form,
                                           SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/");
        });

        // --------- Public request API for the React portal (React track 003) ---------
        app.MapPublicRequestEndpoints();

        // --------- Music API for the React Music area (React track 006) ---------
        app.MapMusicEndpoints();

        // --------- Public Órgãos Sociais for the React /roles (React track 008) ---------
        app.MapGovernanceEndpoints();

        // --------- Gallery timeline for the React /gallery (React track 009) ---------
        app.MapGalleryEndpoints();

        // --------- Events API for the React /events and the home preview (React tracks 010, 011) ---------
        app.MapEventEndpoints();

        // --------- Rehearsals API for the React /rehearsals (React track 014) ---------
        app.MapRehearsalEndpoints();

        // --------- Members area for the React /members (React track 017) ---------
        app.MapMemberEndpoints();
        app.MapLeaderboardEndpoints();
        app.MapInventoryEndpoints();
        app.MapShopEndpoints();
        app.MapDocumentationEndpoints();
        app.MapLogisticsEndpoints();
        app.MapTreasuryEndpoints();

        // --------- Public "Novidades" feed for the React /news (React track 025) ---------
        app.MapNewsEndpoints();

        // --------- Requests admin and Perguntas for the React /requests and /questions (task 031) ---------
        app.MapRequestAdminEndpoints();
        app.MapQuestionEndpoints();

        // --------- Messaging v2 for the React /messages over the preserved v1 tables (task 041) ---------
        app.MapMessagingEndpoints();

        // --------- Profile, members map and Hall of Fame for the React /profile, /members/map and /hall-of-fame (task 032) ---------
        app.MapMemberAreaEndpoints();

        // --------- Naipes for the React /naipes and /naipes/config (task 033) ---------
        app.MapNaipeEndpoints();

        // --------- Meetings for the React /meetings (task 034) ---------
        app.MapMeetingEndpoints();

        // --------- Owner tools for the React /emails, /notifications, /users, /owner/tracing and /owner/db (task 035) ---------
        app.MapOwnerEndpoints();

        // --------- Password recovery and email confirmation for the React identity pages (task 036) ---------
        app.MapAccountRecoveryEndpoints();

        // --------- The member's own push subscriptions for the React /profile (task 038) ---------
        app.MapPushEndpoints();

        // --------- React public shell (React track, tasks 001-004) ---------
        // Route ownership: React owns exactly the paths mapped from here down to /login (public ones first, then the
        // members-only ones that 302 visitors to sign in). /Error is a static server page; nothing else renders HTML
        // (task 037 retired the Blazor host). Retired Blazor URLs 302 to their React page further down. Current list: docs/RTUB_REACT_MIGRATION_GUIDE.md.
        // Its hashed /portal/assets/* are ordinary static files (cached above); the shell itself
        // is no-cache so a deploy is picked up at once. See docs/react-portal-pilot.md.
        var portalShell = new StaticFileOptions
        {
            OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
        };
        foreach (var route in new[] { "/", "/privacy", "/profile", "/request" })
        {
            app.MapFallbackToFile(route, "portal/index.html", portalShell)
               .WithMetadata(new HttpMethodMetadata(["GET", "HEAD"]));

            // The pilot's /portal... URLs (tasks 001-003) land on the clean route. Temporary (302)
            // while DEV is hybrid; GET/HEAD only, so nothing is ever submitted to them.
            app.MapMethods("/portal" + route.TrimEnd('/'), ["GET", "HEAD"], (HttpContext context) =>
                Results.Redirect(route + context.Request.QueryString));
        }

        // React Music (track 006): the album list and one album page. The retired Blazor album page
        // lived at /music/songs/{id}; old links land on the React one (302 while DEV is hybrid).
        // React Órgãos Sociais (track 008): /roles, public. React Gallery (track 009): /gallery.
        // React Events (track 011): the agenda and one event; a member answers in a modal on the event
        // page; Admin/Owner manage events there and on the agenda (011.5-012E). A member's own answers
        // are the "Onde estiveste" modal on /events. The discussion and contact tracking are
        // React too since 013: no Blazor event page is left. React Novidades (track 025): /news, public.
        foreach (var route in new[] { "/music", "/music/albums/{id:int}", "/roles", "/gallery",
                     "/events", "/events/{id:int}", "/news" })
        {
            app.MapFallbackToFile(route, "portal/index.html", portalShell)
               .WithMetadata(new HttpMethodMetadata(["GET", "HEAD"]));
        }

        // The event discussion and contact tracking (React since 013) are for signed-in members, as the Blazor
        // pages were: a visitor goes to sign in and comes back. Contacts is Mod and above; the API enforces it
        // and the page tells anyone else. GET/HEAD only.
        // React Rehearsals (track 014): the list and one rehearsal, members only (the Blazor page was [Authorize]).
        // React Members (track 017): the directory and the Padrinho → Afilhado tree, members only (both Blazor pages were
        // [Authorize]). React Classificação (track 019): /leaderboard, members only (the Blazor page was [Authorize]).
        // React Instrumentos (track 020): /inventory; React Loja (track 021): /shop; React Documentação (track 022):
        // /documentation; React Logística (track 023): /logistics and /logistics/{id}; members only (the Blazor pages were
        // [Authorize]; Leitões are refused by the API). React Tesouraria (track 024): reports, one report, calotes, MBWay and
        // Nerba; members only (the old calotes, MBWay and Nerba pages were open to visitors; the API decides the rest).
        // Task 031: /requests (Gestão de Pedidos) and /questions (Perguntas), members only (both Blazor pages were [Authorize]).
        // Task 032: /members/map (was /member/map) and /hall-of-fame, members only (both Blazor pages were [Authorize]).
        // Task 033: /naipes and /naipes/config, members only (the API keeps the settings to Admin / Owner).
        // Task 034: /meetings, members only (the Blazor page was [Authorize]; the API refuses Leitões and decides the rest).
        // Task 041: /messages, Messaging v2 (React; the conversation is ?c=<id>), members only. /messages/{id} and the old
        // /hubs/messages stay 404: nothing of the 027-retired inbox comes back.
        foreach (var route in new[] { "/events/{id:int}/discussion", "/events/{id:int}/contacts", "/rehearsals", "/rehearsals/{id:int}",
                     "/members", "/members/hierarchy", "/leaderboard", "/inventory", "/shop", "/documentation", "/logistics", "/logistics/{id:int}",
                     "/treasury", "/treasury/reports/{id:int}", "/treasury/calotes", "/treasury/mbway", "/treasury/nerba", "/treasury/nerba/{id:int}",
                     "/requests", "/questions", "/members/map", "/hall-of-fame", "/naipes", "/naipes/config", "/meetings", "/messages" })
        {
            app.MapMethods(route, ["GET", "HEAD"], (HttpContext context, IWebHostEnvironment env) =>
            {
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    return Results.Redirect("/login?returnUrl=" + Uri.EscapeDataString(context.Request.Path));
                }

                context.Response.Headers.CacheControl = "no-cache";
                return Results.File(env.WebRootFileProvider.GetFileInfo("portal/index.html").PhysicalPath!, "text/html");
            });
        }

        // Task 035: the Owner tools, Owner only (/users, /owner/tracing and /owner/db were Owner pages; /emails and
        // /notifications were Admin pages and are Owner only now). A visitor goes to sign in; any other member gets the
        // shell with 403, and the page shows the refusal. The API refuses them every call (OwnerEndpoints).
        foreach (var route in new[] { "/emails", "/notifications", "/users", "/owner/tracing", "/owner/db" })
        {
            app.MapMethods(route, ["GET", "HEAD"], (HttpContext context, IWebHostEnvironment env) =>
            {
                if (context.User.Identity?.IsAuthenticated != true)
                {
                    return Results.Redirect("/login?returnUrl=" + Uri.EscapeDataString(context.Request.Path));
                }

                context.Response.Headers.CacheControl = "no-cache";
                var shell = env.WebRootFileProvider.GetFileInfo("portal/index.html").PhysicalPath!;
                return context.User.IsInRole("Owner")
                    ? Results.File(shell, "text/html")
                    : Results.Content(File.ReadAllText(shell), "text/html", System.Text.Encoding.UTF8, StatusCodes.Status403Forbidden);
            });
        }

        // Task 036: /forgot-password, /reset-password and /confirm-email are React, public as the Blazor pages were. The
        // links carry Identity tokens in the query, so the shell is never stored (no-store) and the page never sends its
        // address on (no-referrer); the page clears the query from the address bar once read.
        foreach (var route in new[] { "/forgot-password", "/reset-password", "/confirm-email" })
        {
            app.MapMethods(route, ["GET", "HEAD"], (HttpContext context, IWebHostEnvironment env) =>
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                return Results.File(env.WebRootFileProvider.GetFileInfo("portal/index.html").PhysicalPath!, "text/html");
            });
        }

        // Task 036: /Error stays server-owned on purpose (not React, not Blazor). UseExceptionHandler re-executes the
        // failed request here with its own method (GET, POST, PUT, ...), and an error page must not depend on the portal
        // bundle, a session or the database. A fixed static page (wwwroot/error.html: no script, no request details,
        // no stack trace); the handler's status (500) is kept. docs/react-identity.md.
        app.Map("/Error", async (HttpContext context, IWebHostEnvironment env) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var page = await File.ReadAllTextAsync(env.WebRootFileProvider.GetFileInfo("error.html").PhysicalPath!);
            return Results.Content(page, "text/html", System.Text.Encoding.UTF8);
        });

        // Task 036: the Blazor /share (the PWA's share target) is removed with no replacement; the manifest no longer
        // offers it. An explicit 404, so it can never fall to another route.
        app.MapMethods("/share", ["GET", "HEAD", "POST"], () => Results.NotFound());

        app.MapMethods("/music/songs/{id:int}", ["GET", "HEAD"], (int id, HttpContext context) =>
            Results.Redirect($"/music/albums/{id}" + context.Request.QueryString));

        // The first 011 build answered on its own page; the event page now opens the answer modal
        // (?respond=1). 302 while DEV is hybrid; GET/HEAD only.
        app.MapMethods("/events/{id:int}/enrollment", ["GET", "HEAD"], (int id) =>
            Results.Redirect($"/events/{id}?respond=1"));

        // The Blazor list of everyone's answers (with Admin/Owner add / remove) is the React event page's
        // "Quem vai / Quem foi" and its management modal since 012E. 302 while DEV is hybrid; GET/HEAD only.
        app.MapMethods("/events/{id:int}/enrollments", ["GET", "HEAD"], (int id) =>
            Results.Redirect($"/events/{id}#who-title"));

        // The members' Blazor /member/events (011-012E) is retired: everything it did is on the React
        // agenda and event pages (012F). Its query is kept, so Requests' "criar atuação" prefill
        // (?openModal=true&name=...) opens the React create form. 302 while DEV is hybrid; GET/HEAD only.
        app.MapMethods("/member/events", ["GET", "HEAD"], (HttpContext context) =>
            Results.Redirect("/events" + context.Request.QueryString));

        // The members' Blazor /member/gallery (009-014) is retired: upload, edit, delete and tags are on the React
        // /gallery (015). 302 while DEV is hybrid; GET/HEAD only.
        app.MapMethods("/member/gallery", ["GET", "HEAD"], () => Results.Redirect("/gallery"));

        // The members' Blazor /member/roles (008-015) is retired: the RGI, fiscal years and position assignments are on
        // the React /roles (016). ?fy= is kept. 302 while DEV is hybrid; GET/HEAD only.
        app.MapMethods("/member/roles", ["GET", "HEAD"], (HttpContext context) =>
            Results.Redirect("/roles" + context.Request.QueryString));

        // The Blazor /hierarchy (until 016) is the React /members/hierarchy (017). 302 while DEV is hybrid; GET/HEAD only.
        app.MapMethods("/hierarchy", ["GET", "HEAD"], () => Results.Redirect("/members/hierarchy"));

        // The Blazor /member/profile and /member/map (until 031) are the React /profile and /members/map (032): /profile
        // edits the member's own profile, and shows a visitor how to sign in. 302 while DEV is hybrid; GET/HEAD only.
        app.MapMethods("/member/profile", ["GET", "HEAD"], () => Results.Redirect("/profile"));
        app.MapMethods("/member/map", ["GET", "HEAD"], () => Results.Redirect("/members/map"));

        // The Blazor /members/manage admin bridge (017) is retired: its tools are on the React /members (018). 302 while
        // DEV is hybrid; GET/HEAD only.
        app.MapMethods("/members/manage", ["GET", "HEAD"], () => Results.Redirect("/members"));

        // The Blazor Tesouraria pages (until 023) are the React /treasury pages (024). /calotes keeps its ?fy=; the old
        // MBWay and Nerba URLs carried a report id only for their back button. 302 while DEV is hybrid; GET/HEAD only.
        app.MapMethods("/finance", ["GET", "HEAD"], () => Results.Redirect("/treasury"));
        app.MapMethods("/finance/report/{id:int}", ["GET", "HEAD"], (int id) => Results.Redirect($"/treasury/reports/{id}"));
        app.MapMethods("/calotes", ["GET", "HEAD"], (HttpContext context) =>
            Results.Redirect("/treasury/calotes" + (context.Request.Query["fy"].ToString() is { Length: > 0 } fy ? "?fy=" + Uri.EscapeDataString(fy) : "")));
        foreach (var route in new[] { "/mbway", "/mbway/{id:int}" })
        {
            app.MapMethods(route, ["GET", "HEAD"], () => Results.Redirect("/treasury/mbway"));
        }

        foreach (var route in new[] { "/nerba", "/nerba/{id:int}" })
        {
            app.MapMethods(route, ["GET", "HEAD"], () => Results.Redirect("/treasury/nerba"));
        }

        app.MapMethods("/nerba/event/{id:int}", ["GET", "HEAD"], (int id) => Results.Redirect($"/treasury/nerba/{id}"));

        // The Blazor /images admin (homepage slideshows) was retired in 029A with no replacement. Without this the bare
        // URL would fall to ImagesController's catch-all "images/{**imagePath}" (empty path → 400); a literal route is
        // more specific, so /images is a plain 404 like /labels, and /images/<file> is still served by the controller.
        app.MapMethods("/images", ["GET", "HEAD"], () => Results.NotFound());

        // React Login (track 007). Everyone signed out gets the React shell, like the routes above.
        // A signed-in member never sees the form and goes to the members' landing page. The return
        // URL is deliberately ignored here: /login is also the cookie's AccessDeniedPath, so a member
        // bounced off a page their role cannot open would be sent straight back to it, in a loop.
        app.MapMethods("/login", ["GET", "HEAD"], (HttpContext context, IWebHostEnvironment env) =>
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                return Results.Redirect(MemberLanding(null));
            }

            context.Response.Headers.CacheControl = "no-cache";
            return Results.File(env.WebRootFileProvider.GetFileInfo("portal/index.html").PhysicalPath!, "text/html");
        });

        // Map API controllers
        app.MapControllers();

        app.Run();
    }

    /// <summary>Where a signed-in member goes: a local return URL, otherwise the (React) events page.</summary>
    internal static string MemberLanding(string? returnUrl) =>
        RTUB.Application.Helpers.UrlHelper.IsLocalUrl(returnUrl) ? returnUrl! : "/events";
}
