using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Services.Email;
using RTUB.Application.Services.Geocoding;
using RTUB.Application.Services.Retirement;
using RTUB.Application.Services.Storage;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;
using RTUB.Web.Services;

namespace RTUB.Web.Extensions;

/// <summary>
/// Extension methods for registering application services
/// Organizes DI registrations by domain responsibility
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Name of the rate limiting policy applied to <c>POST /auth/login</c>.</summary>
    public const string LoginRateLimitPolicy = "login";

    private const string LoginRateLimitSection = "LoginRateLimit";

    /// <summary>
    /// Login attempts allowed per client IP per window. Identity locks an account after 5 failures,
    /// so this sits just above that: a single fumbling user is never throttled, while one client
    /// probing a list of accounts is capped well below a useful credential-stuffing rate.
    /// </summary>
    private const int DefaultLoginPermitLimit = 10;

    /// <summary>Matched to <c>Lockout.DefaultLockoutTimeSpan</c> so both limits tell one story.</summary>
    private const double DefaultLoginWindowMinutes = 5;

    /// <summary>
    /// Cache-key prefix for the <c>LastLoginDate</c> write throttle, keyed by <b>user id</b>.
    /// Deliberately separate from the authentication-log throttle in the same handler: one limits
    /// how often a line is logged, this one limits how often a row is written.
    /// </summary>
    public const string ActivityWriteCachePrefix = "activity-lastlogin:";

    /// <summary>
    /// How long a successful <c>LastLoginDate</c> write suppresses the next one for the same user.
    /// Well under the one-hour bucket the presence UI renders, so the throttle is invisible there.
    /// </summary>
    public static readonly TimeSpan ActivityWriteThrottle = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Registers repositories for data access
    /// Implements Repository pattern following DIP
    /// </summary>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        // Register generic repository
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        // Register specific repositories with domain logic
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<ISongRepository, SongRepository>();
        services.AddScoped<IRehearsalRepository, RehearsalRepository>();
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddScoped<IAlbumRepository, AlbumRepository>();
        services.AddScoped<IPostRepository, PostRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IPostMediaRepository, PostMediaRepository>();
        services.AddScoped<ICommentImageRepository, CommentImageRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IMemberDebtRepository, MemberDebtRepository>();
        services.AddScoped<IDiscussionRepository, DiscussionRepository>();
        services.AddScoped<ITransportationRepository, TransportationRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ILogisticsBoardRepository, LogisticsBoardRepository>();
        services.AddScoped<ILogisticsListRepository, LogisticsListRepository>();
        services.AddScoped<ILogisticsCardRepository, LogisticsCardRepository>();
        services.AddScoped<IEventRepertoireRepository, EventRepertoireRepository>();
        services.AddScoped<IRehearsalAttendanceRepository, RehearsalAttendanceRepository>();
        services.AddScoped<IFiscalYearRepository, FiscalYearRepository>();
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<IMeetingRequestRepository, MeetingRequestRepository>();
        services.AddScoped<IMeetingParticipationRepository, MeetingParticipationRepository>();
        services.AddScoped<IProductReservationRepository, ProductReservationRepository>();
        services.AddScoped<IMemberInstrumentRepository, MemberInstrumentRepository>();
        services.AddScoped<ITrophyRepository, TrophyRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<ISlideshowRepository, SlideshowRepository>();
        services.AddScoped<ILeaderboardCommentRepository, LeaderboardCommentRepository>();
        services.AddScoped<IBetCommentRepository, BetCommentRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IConversationUserSettingsRepository, ConversationUserSettingsRepository>();
        services.AddScoped<ISongVideoRepository, SongVideoRepository>();
        services.AddScoped<IEventVideoRepository, EventVideoRepository>();
        services.AddScoped<IGalleryMediaRepository, GalleryMediaRepository>();
        services.AddScoped<INaipeContentRepository, NaipeContentRepository>();
        services.AddScoped<INaipeCommentRepository, NaipeCommentRepository>();
        services.AddScoped<INaipeTypeConfigRepository, NaipeTypeConfigRepository>();
        services.AddScoped<IGameScoreRepository, GameScoreRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IBetRepository, BetRepository>();
        services.AddScoped<IBetOptionRepository, BetOptionRepository>();
        services.AddScoped<IUserBetRepository, UserBetRepository>();
        services.AddScoped<ICharacterRepository, CharacterRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();

        // Stage Mode repositories
        services.AddScoped<IStageProgressRepository, StageProgressRepository>();
        services.AddScoped<IStageEnemyRepository, StageEnemyRepository>();

        // Boss Mode repositories
        services.AddScoped<IBossModeProgressRepository, BossModeProgressRepository>();

        // Survive Mode repositories
        services.AddScoped<ISurviveModeProgressRepository, SurviveModeProgressRepository>();
        services.AddScoped<IItemTypeConfigRepository, ItemTypeConfigRepository>();
        services.AddScoped<IForgeComboConfigRepository, ForgeComboConfigRepository>();

        return services;
    }

    /// <summary>
    /// Registers core application services (events, albums, songs, reports, etc.)
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Core domain services
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IAlbumService, AlbumService>();
        services.AddScoped<IAlbumStatisticsService, AlbumStatisticsService>();
        services.AddScoped<IAlbumFilterService, AlbumFilterService>();
        services.AddScoped<IAlbumImageService, AlbumImageService>();
        services.AddScoped<IEventFilterService, EventFilterService>();
        services.AddScoped<IEventUrlService, EventUrlService>();
        services.AddScoped<IEventStatisticsService, EventStatisticsService>();
        services.AddScoped<IEventDiscussionService, EventDiscussionService>();
        services.AddScoped<ITransportationService, TransportationService>();
        services.AddScoped<IEventAuthorizationService, EventAuthorizationService>();
        services.AddScoped<IEnrollmentFilterService, EnrollmentFilterService>();
        services.AddScoped<IEnrollmentStatisticsService, EnrollmentStatisticsService>();
        services.AddScoped<IEventContactService, EventContactService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISongService, SongService>();
        services.AddScoped<ISongContentService, SongContentService>();
        services.AddScoped<ISongUrlCacheService, SongUrlCacheService>();
        services.AddScoped<ISongPlayService, SongPlayService>();
        services.AddScoped<ISongValidationService, SongValidationService>();
        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<ISlideshowService, SlideshowService>();
        services.AddScoped<ILabelService, LabelService>();
        services.AddScoped<IRoleAssignmentService, RoleAssignmentService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IMemberDebtService, MemberDebtService>();
        services.AddScoped<IMbwayTransferService, MbwayTransferService>();
        services.AddScoped<INerbaOrderService, NerbaOrderService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IEnrollmentService, EnrollmentService>();
        services.AddScoped<IMemberInstrumentService, MemberInstrumentService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IFiscalYearService, FiscalYearService>();
        services.AddScoped<IEventRepertoireService, EventRepertoireService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IRetirementStatusService, RetirementStatusService>();
        services.AddScoped<IGalleryMediaService, GalleryMediaService>();
        services.AddScoped<INaipeService, NaipeService>();
        services.AddScoped<INaipeContentFilterService, NaipeContentFilterService>();
        services.AddScoped<INaipeAuthorizationService, NaipeAuthorizationService>();
        services.AddScoped<INaipeConfigService, NaipeConfigService>();
        services.AddScoped<IItemTypeConfigService, ItemTypeConfigService>();
        services.AddSingleton<ItemTypeConfigInitializer>();
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IUpgradeService, UpgradeService>();
        services.AddScoped<IImprovementService, ImprovementService>();
        services.AddScoped<IConsumableUpgradeService, ConsumableUpgradeService>();
        services.AddSingleton<ICombatEngine, DeterministicCombatEngine>();
        services.AddScoped<ICombatActionService, CombatActionService>();
        services.AddScoped<IBattleService, BattleService>();
        services.AddScoped<IStageService, StageService>();
        services.AddScoped<IStageBiomeService, StageBiomeService>();
        services.AddScoped<IStageEnemyManagementService, StageEnemyManagementService>();
        services.AddScoped<IBossModeService, BossModeService>();
        services.AddScoped<ISurviveModeService, SurviveModeService>();
        services.AddScoped<IShopService, ShopService>();

        return services;
    }

    /// <summary>
    /// Registers rehearsal-related services
    /// </summary>
    public static IServiceCollection AddRehearsalServices(this IServiceCollection services)
    {
        services.AddScoped<IRehearsalService, RehearsalService>();
        services.AddScoped<IRehearsalAttendanceService, RehearsalAttendanceService>();
        services.AddScoped<IRehearsalAttendanceFilterService, RehearsalAttendanceFilterService>();
        services.AddScoped<IRehearsalFilterService, RehearsalFilterService>();
        services.AddScoped<IRehearsalStatisticsService, RehearsalStatisticsService>();
        services.AddScoped<IRehearsalUrlService, RehearsalUrlService>();

        return services;
    }

    /// <summary>
    /// Registers logistics board services
    /// </summary>
    public static IServiceCollection AddLogisticsServices(this IServiceCollection services)
    {
        services.AddScoped<ILogisticsBoardService, LogisticsBoardService>();
        services.AddScoped<ILogisticsListService, LogisticsListService>();
        services.AddScoped<ILogisticsCardService, LogisticsCardService>();

        return services;
    }

    /// <summary>
    /// Registers meeting-related services
    /// </summary>
    public static IServiceCollection AddMeetingServices(this IServiceCollection services)
    {
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IMeetingRequestService, MeetingRequestService>();
        services.AddScoped<IMeetingParticipationService, MeetingParticipationService>();
        services.AddScoped<IMeetingAtaService, MeetingAtaService>();
        services.AddScoped<IMeetingAtaConfirmationService, MeetingAtaConfirmationService>();
        services.AddScoped<IAtaPdfService, AtaPdfService>();

        return services;
    }

    /// <summary>
    /// Registers inventory and shop services
    /// </summary>
    public static IServiceCollection AddInventoryServices(this IServiceCollection services)
    {
        services.AddScoped<IInstrumentService, InstrumentService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IProductReservationService, ProductReservationService>();
        services.AddScoped<ITrophyService, TrophyService>();
        services.AddScoped<IInventoryService, InventoryService>();

        return services;
    }

    /// <summary>
    /// Registers discussion and social features
    /// </summary>
    public static IServiceCollection AddDiscussionServices(this IServiceCollection services)
    {
        services.AddScoped<IDiscussionService, DiscussionService>();
        services.AddScoped<IPostService, PostService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<ILeaderboardCommentService, LeaderboardCommentService>();
        services.AddScoped<IBetCommentService, BetCommentService>();

        return services;
    }

    /// <summary>
    /// Registers member-related services
    /// </summary>
    public static IServiceCollection AddMemberServices(this IServiceCollection services)
    {
        services.AddScoped<IMemberFilterService, MemberFilterService>();
        services.AddScoped<IActiveMemberFilterService, ActiveMemberFilterService>();
        services.AddScoped<IMemberMentorService, MemberMentorService>();
        services.AddScoped<IMemberPositionService, MemberPositionService>();
        services.AddScoped<IMemberAnniversaryService, MemberAnniversaryService>();
        services.AddScoped<IMemberHierarchyService, MemberHierarchyService>();

        return services;
    }

    /// <summary>
    /// Registers role assignment and management services
    /// </summary>
    public static IServiceCollection AddRoleServices(this IServiceCollection services)
    {
        services.AddScoped<IRoleAssignmentValidationService, RoleAssignmentValidationService>();
        services.AddScoped<IRoleManagementService, RoleManagementService>();

        return services;
    }

    /// <summary>
    /// Registers question services for Orgãos Sociais Q&A
    /// </summary>
    public static IServiceCollection AddQuestionServices(this IServiceCollection services)
    {
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IQuestionReplyRepository, QuestionReplyRepository>();
        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IMeetingAtaRepository, MeetingAtaRepository>();

        return services;
    }

    /// <summary>
    /// Registers ranking and gamification services
    /// </summary>
    public static IServiceCollection AddRankingServices(this IServiceCollection services)
    {
        services.AddScoped<IRankingService, RankingService>();

        return services;
    }

    /// <summary>
    /// Registers game services
    /// </summary>
    public static IServiceCollection AddGameServices(this IServiceCollection services)
    {
        services.AddScoped<IGameScoreService, GameScoreService>();
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IGameFilterService, GameFilterService>();
        services.AddScoped<ITimeFormatter, TimeFormatter>();

        return services;
    }

    /// <summary>
    /// Registers finance and transaction services
    /// </summary>
    public static IServiceCollection AddFinanceServices(this IServiceCollection services)
    {
        services.AddScoped<ITransactionFilterService, TransactionFilterService>();
        services.AddScoped<IDebtService, DebtService>();
        services.AddScoped<IFinanceManagementService, FinanceManagementService>();
        services.AddScoped<IFiscalYearHelper, FiscalYearHelperService>();
        services.AddScoped<IRequestToEventService, RequestToEventService>();
        services.AddScoped<IRequestValidationService, RequestValidationService>();

        return services;
    }

    /// <summary>
    /// Registers betting services (Fidelis wagering system)
    /// </summary>
    public static IServiceCollection AddBettingServices(this IServiceCollection services)
    {
        services.AddScoped<IBetService, BetService>();

        return services;
    }

    /// <summary>
    /// Registers email notification services
    /// </summary>
    public static IServiceCollection AddEmailServices(this IServiceCollection services)
    {
        // Email notification dependencies (following SRP)
        services.AddScoped<EmailConfigurationProvider>();
        services.AddScoped<SmtpClientFactory>();
        services.AddScoped<EmailRateLimiter>();
        services.AddScoped<IEmailNotificationService, EmailNotificationService>();

        return services;
    }

    /// <summary>
    /// Guards against a non-production environment being pointed at the production bucket.
    /// </summary>
    /// <remarks>
    /// <c>appsettings.json</c> commits <c>Cloudflare:R2:Bucket</c> as the production bucket name,
    /// so an environment that forgets to override it inherits production's bucket and every
    /// upload and delete lands there. No per-object ownership check can catch that - the bucket
    /// really is the one configured - so it is refused at startup instead, before any service can
    /// be resolved. Production itself is never checked and is unaffected.
    /// </remarks>
    private static void GuardAgainstProductionBucket(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsProduction())
        {
            return;
        }

        var bucket = configuration["Cloudflare:R2:Bucket"];
        var productionBucket = configuration["Cloudflare:R2:ProductionBucket"];

        if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(productionBucket))
        {
            return;
        }

        // Only an environment that can actually reach R2 is worth refusing. With no credential
        // the S3 client cannot touch any bucket, production's included, so there is nothing to
        // guard - which is what lets the integration-test host run on the committed settings.
        //
        // Deliberately a capability check, not an allow-list of environment names: exempting
        // "Test" by name would exempt anything that called itself Test, credentials and all.
        if (string.IsNullOrWhiteSpace(configuration["Cloudflare:R2:AccessKeyId"]) ||
            string.IsNullOrWhiteSpace(configuration["Cloudflare:R2:SecretAccessKey"]))
        {
            return;
        }

        if (string.Equals(bucket.Trim(), productionBucket.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Environment '{environment.EnvironmentName}' is configured with Cloudflare:R2:Bucket = "
                + "the production bucket. A non-production environment must have its own bucket: every "
                + "upload and every delete it performs would otherwise be applied to production data. "
                + "Set Cloudflare__R2__Bucket to this environment's own bucket.");
        }
    }

    /// <summary>
    /// Registers storage services (images, audio, lyrics, documents)
    /// </summary>
    public static IServiceCollection AddStorageServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        GuardAgainstProductionBucket(configuration, environment);

        // Read-only view of the production bucket, for a non-production app running on a
        // sanitized production snapshot. Always registered, but inert unless the environment is
        // non-production AND a dedicated Cloudflare:R2:Reference:* credential is configured - so
        // in production it holds no client and answers "not found" to everything.
        services.AddSingleton<IReferenceStorageService, ReferenceStorageService>();

        services.AddScoped<IImageStorageService, CloudflareImageStorageService>();
        services.AddScoped<IAudioStorageService, CloudflareAudioStorageService>();
        services.AddScoped<ILyricStorageService, CloudflareLyricStorageService>();
        services.AddScoped<IDocumentStorageService, CloudflareDocumentStorageService>();
        services.AddScoped<IEventMediaStorageService, CloudflareEventMediaStorageService>();
        services.AddScoped<ISongVideoStorageService, CloudflareSongVideoStorageService>();
        services.AddScoped<IEventVideoStorageService, CloudflareEventVideoStorageService>();
        services.AddScoped<IGalleryMediaStorageService, CloudflareGalleryMediaStorageService>();
        services.AddScoped<IReceiptStorageService, CloudflareReceiptStorageService>();
        services.AddScoped<INaipeMediaStorageService, CloudflareNaipeMediaStorageService>();
        services.AddScoped<IItemTypeMediaStorageService, CloudflareItemTypeMediaStorageService>();

        return services;
    }

    /// <summary>
    /// Key for the S3 client that talks to the private database-backup bucket.
    /// Kept separate from the media client so the backup can use a bucket-scoped token.
    /// </summary>
    private const string BackupS3ClientKey = "R2Backup";

    /// <summary>
    /// Registers the daily SQLite backup: its own R2 client, storage service and scheduler.
    /// Registers nothing when DatabaseBackup:Enabled is false, so non-production
    /// environments never touch the backup bucket.
    /// </summary>
    public static IServiceCollection AddDatabaseBackupServices(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration
            .GetSection(RTUB.Application.Configuration.DatabaseBackupOptions.SectionName)
            .Get<RTUB.Application.Configuration.DatabaseBackupOptions>();

        if (options?.Enabled != true)
        {
            return services;
        }

        // Credentials fall back to the media R2 settings when a dedicated backup token is
        // not configured. Resolved lazily so a misconfiguration surfaces as a clear error
        // in the logs rather than at container build time.
        services.AddKeyedSingleton<Amazon.S3.IAmazonS3>(BackupS3ClientKey, (serviceProvider, _) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            var accessKey = configuration["DatabaseBackup:AccessKeyId"] ?? configuration["Cloudflare:R2:AccessKeyId"];
            var secretKey = configuration["DatabaseBackup:SecretAccessKey"] ?? configuration["Cloudflare:R2:SecretAccessKey"];
            var accountId = configuration["DatabaseBackup:AccountId"] ?? configuration["Cloudflare:R2:AccountId"];

            if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            {
                var errorMsg = "Database backup credentials not configured. Set DatabaseBackup:AccessKeyId and DatabaseBackup:SecretAccessKey (or fall back to the Cloudflare:R2 ones).";
                logger.LogError(errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            if (string.IsNullOrEmpty(accountId))
            {
                var errorMsg = "Database backup account ID not configured. Set DatabaseBackup:AccountId or Cloudflare:R2:AccountId.";
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

        services.AddScoped<IDatabaseBackupStorageService>(serviceProvider =>
            new DatabaseBackupStorageService(
                serviceProvider.GetRequiredKeyedService<Amazon.S3.IAmazonS3>(BackupS3ClientKey),
                serviceProvider.GetRequiredService<IOptions<RTUB.Application.Configuration.DatabaseBackupOptions>>(),
                serviceProvider.GetRequiredService<ILogger<DatabaseBackupStorageService>>()));

        services.AddHostedService<DatabaseBackupBackgroundService>();

        return services;
    }

    /// <summary>
    /// Registers member query and statistics services
    /// Provides optimized queries for member pages (Leaderboard, Members)
    /// </summary>
    public static IServiceCollection AddMemberQueryServices(this IServiceCollection services)
    {
        services.AddScoped<IMemberStatisticsService, MemberStatisticsService>();
        services.AddScoped<IUserRoleQueryService, UserRoleQueryService>();
        services.AddScoped<IMemberStatusService, MemberStatusService>();

        return services;
    }

    /// <summary>
    /// Registers Web Push notification services
    /// </summary>
    public static IServiceCollection AddPushNotificationServices(this IServiceCollection services)
    {
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        services.AddScoped<IPushNotificationService, PushNotificationService>();
        services.AddScoped<IPushNotificationFactory, RTUB.Application.Factories.PushNotificationFactory>();

        return services;
    }

    /// <summary>
    /// Registers internal messaging services
    /// </summary>
    public static IServiceCollection AddMessagingServices(this IServiceCollection services)
    {
        services.AddScoped<IMessagingService, MessagingService>();
        services.AddScoped<IGroupConversationSyncService, GroupConversationSyncService>();
        services.AddScoped<IMessagesHubService, RTUB.Web.Services.MessagesHubService>();
        services.AddScoped<IMessagingDisplayService, MessagingDisplayService>();
        services.AddScoped<IMessagingSortService, MessagingSortService>();

        return services;
    }

    /// <summary>
    /// Registers the per-client rate limiter for <c>POST /auth/login</c>.
    /// </summary>
    /// <remarks>
    /// This is a named policy only - there is deliberately no <c>GlobalLimiter</c>, so nothing is
    /// throttled except the endpoints that opt in with <c>RequireRateLimiting</c>.
    ///
    /// It complements, and does not replace, Identity's per-account lockout
    /// (<see cref="AddIdentityServices"/>): lockout stops repeated guesses against one account,
    /// this stops one client walking many accounts (credential stuffing), which lockout never sees.
    ///
    /// Partitioning is on <c>Connection.RemoteIpAddress</c> only. The request's own headers are
    /// never read: <c>X-Forwarded-For</c> is caller-controlled, so trusting it here would let an
    /// attacker mint a fresh budget per request and allocate a limiter per forged value. Behind a
    /// reverse proxy, <c>RemoteIpAddress</c> must be corrected by the host (on Azure App Service,
    /// the <c>ASPNETCORE_FORWARDEDHEADERS_ENABLED</c> app setting), not by parsing headers here.
    /// </remarks>
    public static IServiceCollection AddLoginRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(LoginRateLimitSection);
        var permitLimit = section.GetValue("PermitLimit", DefaultLoginPermitLimit);
        var window = TimeSpan.FromMinutes(section.GetValue("WindowMinutes", DefaultLoginWindowMinutes));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(LoginRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    // A null RemoteIpAddress collapses into this one shared bucket rather than
                    // creating a partition, so the partition count stays bounded by the number of
                    // real peers and can never be grown by anything a caller supplies.
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        // No queue: a rejected attempt is answered immediately instead of holding
                        // the request open, which is what a flood would otherwise exploit.
                        QueueLimit = 0
                    }));

            // Only the login policy exists, so this callback is reached only by a login rejection.
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
                }

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsync(
                    "Demasiadas tentativas de login. Tente novamente mais tarde.", cancellationToken);
            };
        });

        return services;
    }

    /// <summary>
    /// Registers all IOptions&lt;T&gt; configuration bindings from appsettings / scaling.config.json
    /// </summary>
    public static IServiceCollection AddConfigurationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RTUB.Application.Configuration.RankingConfiguration>(
            configuration.GetSection(RTUB.Application.Configuration.RankingConfiguration.SectionName));
        services.Configure<RTUB.Application.Configuration.XpSettings>(
            configuration.GetSection(RTUB.Application.Configuration.XpSettings.SectionName));
        services.Configure<RTUB.Application.Configuration.Toggles>(
            configuration.GetSection(RTUB.Application.Configuration.Toggles.SectionName));
        services.Configure<RTUB.Application.Configuration.WebPushOptions>(
            configuration.GetSection(RTUB.Application.Configuration.WebPushOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.BirthdayEmailSchedulerOptions>(
            configuration.GetSection(RTUB.Application.Configuration.BirthdayEmailSchedulerOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.MemberStatusUpdateOptions>(
            configuration.GetSection(RTUB.Application.Configuration.MemberStatusUpdateOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.LoginPopupOptions>(
            configuration.GetSection(RTUB.Application.Configuration.LoginPopupOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.PendingRequestReminderOptions>(
            configuration.GetSection(RTUB.Application.Configuration.PendingRequestReminderOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.RehearsalApprovalReminderOptions>(
            configuration.GetSection(RTUB.Application.Configuration.RehearsalApprovalReminderOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.QuestionNotificationOptions>(
            configuration.GetSection(RTUB.Application.Configuration.QuestionNotificationOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.WeeklyNotificationOptions>(
            configuration.GetSection(RTUB.Application.Configuration.WeeklyNotificationOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.CalotesNotificationOptions>(
            configuration.GetSection(RTUB.Application.Configuration.CalotesNotificationOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.ActivityReminderOptions>(
            configuration.GetSection(RTUB.Application.Configuration.ActivityReminderOptions.SectionName));
        services.Configure<RTUB.Application.Configuration.AvoidQuestionsConfiguration>(
            configuration.GetSection(RTUB.Application.Configuration.AvoidQuestionsConfiguration.SectionName));
        services.Configure<RTUB.Application.Configuration.BmrBebeMaisRuiConfiguration>(
            configuration.GetSection(RTUB.Application.Configuration.BmrBebeMaisRuiConfiguration.SectionName));
        services.Configure<RTUB.Application.Configuration.FidelisRewardsConfiguration>(
            configuration.GetSection(RTUB.Application.Configuration.FidelisRewardsConfiguration.SectionName));
        services.Configure<RTUB.Application.Configuration.MyTunoScalingConfiguration>(
            configuration.GetSection(RTUB.Application.Configuration.MyTunoScalingConfiguration.SectionName));
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
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        return services;
    }

    /// <summary>
    /// Configures cookie authentication events: security-stamp validation, role-change logout,
    /// expulsion check, and session logging, followed by Identity's own
    /// <see cref="SecurityStampValidator"/> so the framework's principal refresh and cookie renewal
    /// still happen. RTUB's checks run on every request; the framework's refresh stays gated by
    /// <see cref="SecurityStampValidatorOptions.ValidationInterval"/>.
    /// </summary>
    public static IServiceCollection AddCookieAuthenticationServices(this IServiceCollection services)
    {
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
                        return;

                    var userName = context.Principal?.Identity?.Name;
                    if (string.IsNullOrWhiteSpace(userName))
                        return;

                    var validatedUser = await signInManager.ValidateSecurityStampAsync(context.Principal);
                    if (validatedUser == null)
                    {
                        await signInManager.SignOutAsync();
                        context.RejectPrincipal();
                        return;
                    }

                    if (validatedUser.IsExpelled)
                    {
                        await signInManager.SignOutAsync();
                        context.RejectPrincipal();
                        logger.LogInformation("User {UserName} forced to logout due to expulsion.", userName);
                        return;
                    }

                    var hasAdminClaim = context.Principal?.IsInRole("Admin") ?? false;
                    var isAdminInDatabase = await userManager.IsInRoleAsync(validatedUser, "Admin");
                    if (hasAdminClaim && !isAdminInDatabase)
                    {
                        await signInManager.SignOutAsync();
                        context.RejectPrincipal();
                        logger.LogInformation("User {UserName} forced to logout due to role change.", userName);
                        return;
                    }

                    // Identity's own cookie validation, composed with the checks above rather than
                    // replaced by them. AddIdentity installs SecurityStampValidator.ValidatePrincipalAsync
                    // as OnValidatePrincipal; assigning this whole CookieAuthenticationEvents object
                    // used to drop it, and with it the principal refresh Identity performs after a
                    // successful stamp check — so role and profile claim changes never reached a live
                    // session and the cookie was never renewed. This call runs the configured
                    // ISecurityStampValidator, which rebuilds the principal from the database
                    // (SignInManager.CreateUserPrincipalAsync -> ReplacePrincipal + ShouldRenew) once
                    // SecurityStampValidatorOptions.ValidationInterval has elapsed — 30 minutes by
                    // default, deliberately left at the framework default here.
                    //
                    // The order matters and the RTUB checks must stay in front of it:
                    //  - they run on EVERY request, whereas the framework gates its own stamp read
                    //    behind ValidationInterval, so no RTUB check is diluted to that cadence;
                    //  - they read the principal as it arrived in the cookie. A refresh rebuilds it
                    //    from the database, scrubbing exactly the stale "Admin" claim the role probe
                    //    above exists to catch: running the framework first would silently downgrade
                    //    such a session instead of rejecting it, and only on the requests where a
                    //    refresh happened to fall due.
                    //
                    // The framework repeats the stamp check above, but costs nothing for it:
                    // UserManager.GetUserAsync resolves off the request-scoped DbContext's change
                    // tracker, which the check above has already populated. A refresh request pays
                    // only for the rebuild itself — two SELECTs, once per user per ValidationInterval.
                    await SecurityStampValidator.ValidatePrincipalAsync(context);
                    if (context.Principal is null)
                        return;

                    var issuedUtc = context.Properties?.IssuedUtc?.UtcDateTime ?? DateTime.MinValue;
                    var cookieUserAgent = context.HttpContext?.Request?.Headers["User-Agent"].ToString();
                    var logCacheKey = $"login-log:{userName}:{issuedUtc.Ticks}";
                    if (!cache.TryGetValue(logCacheKey, out _))
                    {
                        logger.LogInformation(
                            "User {UserName} authenticated via cookie validation at {LoginTime} ({Device})",
                            userName, DateTime.UtcNow,
                            UserAgentHelper.GetShortUserAgent(cookieUserAgent));
                        cache.Set(logCacheKey, true, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                        });
                    }

                    // LastLoginDate is, despite its name, a last-authenticated-activity stamp: it
                    // backs the online / "Recente" presence UI, which buckets at one hour. This
                    // handler runs on every authenticated request, so the write is throttled to one
                    // per ActivityWriteThrottle per user — far finer than the UI can render, and it
                    // keeps routine requests off the write path entirely.
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (string.IsNullOrWhiteSpace(userId))
                        return;

                    var activityCacheKey = $"{ActivityWriteCachePrefix}{userId}";
                    if (cache.TryGetValue(activityCacheKey, out _))
                        return;

                    try
                    {
                        var db = context.HttpContext?.RequestServices?.GetService<ApplicationDbContext>();
                        if (db is null)
                        {
                            logger.LogWarning("ApplicationDbContext not available in OnValidatePrincipal");
                            return;
                        }

                        var now = DateTime.UtcNow;
                        for (int attempt = 1; ; attempt++)
                        {
                            try
                            {
                                await db.Database.ExecuteSqlInterpolatedAsync($@"
                                    UPDATE AspNetUsers
                                    SET LastLoginDate = {now}
                                    WHERE Id = {userId};");
                                break;
                            }
                            // 6 is SQLITE_LOCKED, not SQLITE_BUSY (5). SQLITE_BUSY is already
                            // absorbed by "PRAGMA busy_timeout = 30000" in SqliteConnectionInterceptor;
                            // busy_timeout does not cover SQLITE_LOCKED, which is why it is retried here.
                            catch (Microsoft.Data.Sqlite.SqliteException ex) when (attempt < 3 && ex.SqliteErrorCode == 6)
                            {
                                await Task.Delay(50 * (int)Math.Pow(2, attempt - 1));
                            }
                        }

                        // Only once the write has actually succeeded — a failed update must not
                        // suppress the next request's attempt.
                        cache.Set(activityCacheKey, true, new MemoryCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = ActivityWriteThrottle
                        });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error while updating LastLoginDate for {UserName}", userName);
                    }
                }
            };
        });

        return services;
    }

    /// <summary>
    /// Registers infrastructure services: email, PDF, MVC, SQL validation, database viewer, S3/R2
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ReportPdfService>();
        services.AddScoped<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, EmailSender>();
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
        services.AddHttpClient("CdnProxy")
            .ConfigureHttpClient(client => { client.Timeout = TimeSpan.FromSeconds(15); });

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
        services.AddHostedService<RehearsalApprovalReminderBackgroundService>();
        services.AddHostedService<QuestionNotificationBackgroundService>();
        services.AddHostedService<WeeklyNotificationBackgroundService>();
        services.AddHostedService<CalotesNotificationBackgroundService>();
        services.AddHostedService<ActivityReminderBackgroundService>();

        return services;
    }

    /// <summary>
    /// Registers UI state services, interop services, and Blazor-specific singletons
    /// </summary>
    public static IServiceCollection AddWebUiServices(this IServiceCollection services)
    {
        services.AddScoped<ProfilePictureUpdateService>();
        services.AddSingleton<MessagesNotificationService>();
        services.AddSingleton<AdminRefreshService>();
        services.AddSingleton<AnnouncementService>();
        services.AddScoped<RTUB.Web.Interop.MediaSessionInterop>();
        services.AddScoped<RTUB.Web.Interop.AudioPlayerInterop>();
        services.AddScoped<RTUB.Web.Interop.PwaHelperInterop>();
        services.AddScoped<MediaQueueService>();

        return services;
    }

    /// <summary>
    /// Configures Blazor Interactive Server, SignalR hub options, authorization, antiforgery,
    /// response compression and caching
    /// </summary>
    public static IServiceCollection AddBlazorAndWebServices(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                options.DetailedErrors = environment.IsDevelopment();
                options.MaxBufferedUnacknowledgedRenderBatches = 20;
                options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
            });

        services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
        {
            options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(300);
        });

        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, ServerAuthenticationStateProvider>();
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
