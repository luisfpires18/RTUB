using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using ApplicationUser = RTUB.Core.Entities.ApplicationUser;

namespace RTUB
{
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
            
            services.AddDbContext<ApplicationDbContext>(o => o.UseSqlite(connectionString, b => b.MigrationsAssembly("RTUB")));

            // ---------- Identity ----------
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            // Configure cookie authentication to redirect to /login instead of /Account/Login
            services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/login";
                options.AccessDeniedPath = "/login";
            });

            // PDF generation service - moved to Application layer
            services.AddScoped<RTUB.Application.Services.ReportPdfService>();

            // Email sender for Identity (forgot password, etc.)
            services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, RTUB.Application.Services.EmailSender>();

            // --------- Application Services (Direct DbContext access) ---------
            services.AddScoped<IEventService, EventService>();
            services.AddScoped<IAlbumService, AlbumService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddScoped<ISongService, SongService>();
            services.AddScoped<IRequestService, RequestService>();
            services.AddScoped<ISlideshowService, SlideshowService>();
            services.AddScoped<ILabelService, LabelService>();
            services.AddScoped<IRoleAssignmentService, RoleAssignmentService>();
            services.AddScoped<ITransactionService, TransactionService>();
            services.AddScoped<IActivityService, ActivityService>();
            services.AddScoped<IEnrollmentService, EnrollmentService>();
            services.AddScoped<IUserProfileService, UserProfileService>();
            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<IEmailNotificationService, EmailNotificationService>();
            services.AddScoped<IFiscalYearService, FiscalYearService>();
            services.AddScoped<IEventRepertoireService, EventRepertoireService>();
            services.AddScoped<IRehearsalService, RehearsalService>();
            services.AddScoped<IRehearsalAttendanceService, RehearsalAttendanceService>();
            services.AddScoped<IAuditLogService, AuditLogService>();
            services.AddSingleton<IAudioStorageService, DriveAudioStorageService>();
            services.AddSingleton<ILyricStorageService, DriveLyricStorageService>();
            services.AddSingleton<IDocumentStorageService, DriveDocumentStorageService>();
            services.AddSingleton<ISlideshowStorageService, DriveSlideshowStorageService>();
            services.AddSingleton<IEventStorageService, DriveEventStorageService>();
            services.AddSingleton<IProfileStorageService, DriveProfileStorageService>();
            
            // --------- Inventory & Shop Services ---------
            services.AddScoped<IInstrumentService, InstrumentService>();
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ITrophyService, TrophyService>();

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
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
                options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
            });

            // Add response caching
            services.AddResponseCaching();

            // Add memory cache for server-side caching (reduces database queries)
            services.AddMemoryCache();

            // Add controller support for API endpoints
            services.AddControllers();

            var app = builder.Build();

            // ---------- Migrate + seed ----------
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
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while migrating or seeding the database");
                    throw;
                }
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }
            
            if (app.Environment.IsDevelopment())
            {
                // Only use HTTPS redirection in development
                // In production (Azure App Service), HTTPS is handled at the load balancer level
                app.UseHttpsRedirection();

                // Enable response compression (must be before UseStaticFiles)
                app.UseResponseCompression();

            }

            // Enable response caching
            app.UseResponseCaching();

            // Serve static files with caching headers
            app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    // Cache static files for 30 days in production
                    if (!app.Environment.IsDevelopment())
                    {
                        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=2592000");
                    }
                }
            });

            app.UseRouting();

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
                                              UserManager<ApplicationUser> userManager) =>
            {
                var form = await http.Request.ReadFormAsync();
                var username = form["Username"].ToString();
                var password = form["Password"].ToString();
                var rememberRaw = form["RememberMe"].ToString();
                var remember = bool.TryParse(rememberRaw, out var b) ? b : string.Equals(rememberRaw, "on", StringComparison.OrdinalIgnoreCase);
                var returnUrl = form["ReturnUrl"].ToString();

                var user = await userManager.FindByNameAsync(username);
                if (user is null || !await userManager.IsEmailConfirmedAsync(user))
                {
                    return Results.Redirect("/login?error=Invalid");
                }

                var result = await signInManager.PasswordSignInAsync(user, password, remember, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    // Update last login date (best effort - don't fail login if this fails)
                    try
                    {
                        user.LastLoginDate = DateTime.UtcNow;
                        await userManager.UpdateAsync(user);
                    }
                    catch
                    {
                        // Log error but don't fail login
                    }
                    
                    // Validate and redirect to return URL if provided and is a local URL, otherwise redirect to home
                    if (!string.IsNullOrEmpty(returnUrl) && RTUB.Application.Helpers.UrlHelper.IsLocalUrl(returnUrl))
                    {
                        return Results.Redirect(returnUrl);
                    }
                    return Results.Redirect("/");
                }
                if (result.RequiresTwoFactor) return Results.Redirect($"/login-with-2fa?rememberMe={remember}");
                if (result.IsLockedOut) return Results.Redirect("/login?error=Locked");

                return Results.Redirect("/login?error=Invalid");
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

            // Map API controllers
            app.MapControllers();

            app.Run();
        }
    }
}