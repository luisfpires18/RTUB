using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Application.Services.Geocoding;
using RTUB.Core.Entities;
using RTUB.Web.Services;

namespace RTUB.Web.Extensions;

// Options, database, identity, infrastructure, geocoding, background workers and web services.

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all IOptions&lt;T&gt; configuration bindings from appsettings
    /// </summary>
    public static IServiceCollection AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RTUB.Application.Configuration.RankingConfiguration>(
            configuration.GetSection(RTUB.Application.Configuration.RankingConfiguration.SectionName));
        services.Configure<RTUB.Application.Configuration.XpSettings>(
            configuration.GetSection(RTUB.Application.Configuration.XpSettings.SectionName));
        services.Configure<RTUB.Application.Configuration.WebPushOptions>(
            configuration.GetSection(RTUB.Application.Configuration.WebPushOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.BirthdayEmailSchedulerOptions>(
            configuration.GetSection(RTUB.Application.Configuration.BirthdayEmailSchedulerOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.MemberStatusUpdateOptions>(
            configuration.GetSection(RTUB.Application.Configuration.MemberStatusUpdateOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.PendingRequestReminderOptions>(
            configuration.GetSection(RTUB.Application.Configuration.PendingRequestReminderOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.PendingActionsReminderOptions>(
            configuration.GetSection(RTUB.Application.Configuration.PendingActionsReminderOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.RehearsalApprovalReminderOptions>(
            configuration.GetSection(RTUB.Application.Configuration.RehearsalApprovalReminderOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.QuestionNotificationOptions>(
            configuration.GetSection(RTUB.Application.Configuration.QuestionNotificationOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.WeeklyNotificationOptions>(
            configuration.GetSection(RTUB.Application.Configuration.WeeklyNotificationOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.CalotesNotificationOptions>(
            configuration.GetSection(RTUB.Application.Configuration.CalotesNotificationOptions.SectionName));
        services.Configure<LogisticsReminderOptions>(configuration.GetSection(LogisticsReminderOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.ActivityReminderOptions>(
            configuration.GetSection(RTUB.Application.Configuration.ActivityReminderOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.DatabaseBackupOptions>(
            configuration.GetSection(RTUB.Application.Configuration.DatabaseBackupOptions.SectionName));

        return services;
    }

    /// <summary>
    /// Configures SQLite DbContextFactory and registers ApplicationDbContext for Identity
    /// </summary>
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<IAuditLogAppender, AuditLogAppender>();

        services.AddDbContextFactory<ApplicationDbContext>(o =>
        {
            o.UseSqlite(connectionString, b =>
            {
                b.MigrationsAssembly("RTUB");
                b.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .AddInterceptors(new SqliteConnectionInterceptor());
        }, ServiceLifetime.Scoped);

        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

        return services;
    }

    /// <summary>
    /// Registers ASP.NET Core Identity with simplified password requirements
    /// </summary>
    public static IServiceCollection AddIdentityServices(this IServiceCollection services)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.SignIn.RequireConfirmedAccount = true;
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 4;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireDigit = false;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;
            // Version3 adds AspNetUserPasskeys: Face ID / fingerprint sign-in (task 057).
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        return services;
    }

    /// <summary>
    /// Registers infrastructure services: email, PDF, MVC, SQL validation, database viewer, S3/R2
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ReportPdfService>();
        services.AddScoped<IEmailSender, EmailSender>();
        services.AddControllersWithViews();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();
        services.AddScoped<IEmailTemplateRenderer, RazorEmailTemplateRenderer>();
        services.AddScoped<ISqlValidationService, SqlValidationService>();
        services.AddScoped<IDatabaseViewerService, DatabaseViewerService>();

        // Cloudflare R2 S3 client (singleton - shared across all requests)
        services.AddSingleton<Amazon.S3.IAmazonS3>(serviceProvider =>
        {
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
                AuthenticationRegion = "auto"
            };
            return new Amazon.S3.AmazonS3Client(credentials, config);
        });

        return services;
    }

    /// <summary>
    /// Registers geocoding services and HTTP clients
    /// </summary>
    public static IServiceCollection AddGeocodingServices(this IServiceCollection services)
    {
        services.AddHttpClient("Nominatim")
            .ConfigureHttpClient(client => { client.Timeout = TimeSpan.FromSeconds(10); });

        services.AddSingleton<IGeocodingQueue, InMemoryGeocodingQueue>();
        services.AddScoped<NominatimGeocodingService>();
        services.AddScoped<IGeocodingService, CachedGeocodingService>();

        return services;
    }

    /// <summary>
    /// Registers all background hosted services (schedulers, workers)
    /// </summary>
    public static IServiceCollection AddBackgroundServices(this IServiceCollection services)
    {
        services.AddHostedService<BackgroundGeocodingWorker>();
        services.AddHostedService<BirthdayEmailSchedulerService>();
        services.AddHostedService<MemberStatusUpdateBackgroundService>();
        services.AddHostedService<PendingRequestReminderService>();
        services.AddHostedService<PendingActionsReminderService>();
        // Page views (task 089): counted in memory on /api/account/me, stored once a minute.
        services.AddSingleton<PageViewCounter>();
        services.AddHostedService<PageViewFlushService>();
        services.AddHostedService<RehearsalApprovalReminderBackgroundService>();
        services.AddHostedService<QuestionNotificationBackgroundService>();
        services.AddHostedService<WeeklyNotificationBackgroundService>();
        services.AddHostedService<CalotesNotificationBackgroundService>();
        services.AddHostedService<ActivityReminderBackgroundService>();
        services.AddHostedService<LogisticsReminderBackgroundService>();

        return services;
    }

    /// <summary>
    /// Configures the system announcement, authorization, antiforgery, response compression and caching
    /// </summary>
    public static IServiceCollection AddWebServices(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddSingleton<AnnouncementService>();
        // Release notes (task 083): scoped for the context factory; the bundled releases/*.json are cached per process.
        services.AddOptions<RTUB.Web.Services.ReleaseNotesOptions>().BindConfiguration("ReleaseNotes");
        services.AddScoped<RTUB.Web.Services.ReleaseNotesService>();
        services.AddAuthorization(o =>
        {
            o.AddPolicy("RequireAdministratorRole", p => p.RequireRole("Admin"));
        });
        services.AddAntiforgery(o => o.HeaderName = "X-CSRF-TOKEN");

        if (!environment.IsDevelopment())
        {
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
                options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
            });

            // Fastest (the providers' default) left ~30 KB per cold load on the table: the portal's critical path is
            // 161 KB at gzip-1 and 131 KB at gzip-9. Optimal is brotli quality 4 / zlib level 6, a few ms of CPU on a
            // cold load and nothing on a warm one (task 054). Build-time precompression is the next step if this
            // ever shows on the B1's CPU.
            services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o =>
                o.Level = System.IO.Compression.CompressionLevel.Optimal);
            services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o =>
                o.Level = System.IO.Compression.CompressionLevel.Optimal);
        }

        services.AddResponseCaching();
        services.AddMemoryCache();
        services.AddMetrics();
        services.AddControllers();
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("database");

        return services;
    }
}
