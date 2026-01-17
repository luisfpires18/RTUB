using Microsoft.Extensions.DependencyInjection;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Services.Email;
using RTUB.Application.Services.Retirement;
using RTUB.Web.Services;

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
        services.AddScoped<IMemberDebtRepository, MemberDebtRepository>();
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
        services.AddScoped<IEventAuthorizationService, EventAuthorizationService>();
        services.AddScoped<IEnrollmentFilterService, EnrollmentFilterService>();
        services.AddScoped<IEnrollmentStatisticsService, EnrollmentStatisticsService>();
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
        services.AddScoped<IPowerService, PowerService>();
        services.AddScoped<ICombatEngine, DeterministicCombatEngine>();
        services.AddScoped<ICombatActionService, CombatActionService>();
        services.AddScoped<IBattleService, BattleService>();
        services.AddScoped<IStageService, StageService>();
        services.AddScoped<IStageBiomeService, StageBiomeService>();
        services.AddScoped<IStageEnemyManagementService, StageEnemyManagementService>();
        services.AddScoped<IBossModeService, BossModeService>();
        services.AddScoped<ISurviveModeService, SurviveModeService>();

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
    /// Registers storage services (images, audio, lyrics, documents)
    /// </summary>
    public static IServiceCollection AddStorageServices(this IServiceCollection services)
    {
        services.AddScoped<IImageStorageService, CloudflareImageStorageService>();
        services.AddScoped<IAudioStorageService, CloudflareAudioStorageService>();
        services.AddScoped<ILyricStorageService, CloudflareLyricStorageService>();
        services.AddScoped<IDocumentStorageService, CloudflareDocumentStorageService>();
        services.AddScoped<DriveDocumentStorageService>(); // For /roles page RGI document from IDrive
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
}
