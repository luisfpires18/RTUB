using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Services.Email;
using RTUB.Application.Services.Retirement;

namespace RTUB.Web.Extensions;

/// <summary>
/// Extension methods for registering application services
/// Organizes DI registrations by domain responsibility
/// </summary>
public static class ServiceCollectionExtensions
{
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
        services.AddScoped<IDiscussionRepository, DiscussionRepository>();
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
        services.AddScoped<IProductReservationRepository, ProductReservationRepository>();
        services.AddScoped<IMemberInstrumentRepository, MemberInstrumentRepository>();
        services.AddScoped<ITrophyRepository, TrophyRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<ISlideshowRepository, SlideshowRepository>();
        services.AddScoped<ILeaderboardCommentRepository, LeaderboardCommentRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IConversationUserSettingsRepository, ConversationUserSettingsRepository>();
        services.AddScoped<ISongVideoRepository, SongVideoRepository>();
        services.AddScoped<IEventVideoRepository, EventVideoRepository>();
        services.AddScoped<IGalleryMediaRepository, GalleryMediaRepository>();

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
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISongService, SongService>();
        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<ISlideshowService, SlideshowService>();
        services.AddScoped<ILabelService, LabelService>();
        services.AddScoped<IRoleAssignmentService, RoleAssignmentService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IEnrollmentService, EnrollmentService>();
        services.AddScoped<IMemberInstrumentService, MemberInstrumentService>();
        services.AddScoped<IUserProfileService, UserProfileService>();
        services.AddScoped<IFiscalYearService, FiscalYearService>();
        services.AddScoped<IEventRepertoireService, EventRepertoireService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IRetirementStatusService, RetirementStatusService>();
        services.AddScoped<IGalleryMediaService, GalleryMediaService>();
        services.AddScoped<ILoginCountService, LoginCountService>();

        return services;
    }

    /// <summary>
    /// Registers rehearsal-related services
    /// </summary>
    public static IServiceCollection AddRehearsalServices(this IServiceCollection services)
    {
        services.AddScoped<IRehearsalService, RehearsalService>();
        services.AddScoped<IRehearsalAttendanceService, RehearsalAttendanceService>();

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
    /// Registers storage services (images, audio, lyrics, documents)
    /// </summary>
    public static IServiceCollection AddStorageServices(this IServiceCollection services)
    {
        services.AddScoped<IImageStorageService, CloudflareImageStorageService>();
        services.AddSingleton<IAudioStorageService, DriveAudioStorageService>();
        services.AddSingleton<ILyricStorageService, DriveLyricStorageService>();
        services.AddScoped<IDocumentStorageService, CloudflareDocumentStorageService>();
        services.AddScoped<DriveDocumentStorageService>(); // For /roles page RGI document from IDrive
        services.AddScoped<IEventMediaStorageService, CloudflareEventMediaStorageService>();
        services.AddScoped<ISongVideoStorageService, CloudflareSongVideoStorageService>();
        services.AddScoped<IEventVideoStorageService, CloudflareEventVideoStorageService>();
        services.AddScoped<IGalleryMediaStorageService, CloudflareGalleryMediaStorageService>();

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

        return services;
    }
}
