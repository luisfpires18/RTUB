using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;

namespace RTUB.Web.Extensions;

/// <summary>
/// Extension methods for registering application services
/// Organizes DI registrations by domain responsibility
/// </summary>
public static partial class ServiceCollectionExtensions
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
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IMemberDebtRepository, MemberDebtRepository>();
        services.AddScoped<IDiscussionRepository, DiscussionRepository>();
        services.AddScoped<ITransportationRepository, TransportationRepository>();
        services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
        services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IEventRepertoireRepository, EventRepertoireRepository>();
        services.AddScoped<IRehearsalAttendanceRepository, RehearsalAttendanceRepository>();
        services.AddScoped<IFiscalYearRepository, FiscalYearRepository>();
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<IMeetingRequestRepository, MeetingRequestRepository>();
        services.AddScoped<IMeetingParticipationRepository, MeetingParticipationRepository>();
        services.AddScoped<IProductReservationRepository, ProductReservationRepository>();
        services.AddScoped<IMemberInstrumentRepository, MemberInstrumentRepository>();
        services.AddScoped<ITrophyRepository, TrophyRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<ILeaderboardCommentRepository, LeaderboardCommentRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<ISongVideoRepository, SongVideoRepository>();
        services.AddScoped<IEventVideoRepository, EventVideoRepository>();
        services.AddScoped<IGalleryMediaRepository, GalleryMediaRepository>();
        services.AddScoped<INaipeContentRepository, NaipeContentRepository>();
        services.AddScoped<INaipeCommentRepository, NaipeCommentRepository>();
        services.AddScoped<INaipeTypeConfigRepository, NaipeTypeConfigRepository>();

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
        services.AddScoped<ITransportationService, TransportationService>();
        services.AddScoped<IEventContactService, EventContactService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ISongService, SongService>();
        services.AddScoped<IMusicService, MusicService>();
        services.AddScoped<IGovernanceService, GovernanceService>();
        services.AddScoped<IGovernanceManagementService, GovernanceManagementService>();
        services.AddScoped<IMemberDirectoryService, MemberDirectoryService>();
        services.AddScoped<IMemberAdminService, MemberAdminService>();
        services.AddScoped<IMyProfileService, MyProfileService>();
        services.AddScoped<IMemberMapService, MemberMapService>();
        services.AddScoped<IHallOfFameService, HallOfFameService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        services.AddScoped<ITrintunaService, TrintunaService>();
        services.AddScoped<IInstrumentInventoryService, InstrumentInventoryService>();
        services.AddScoped<IProductShopService, ProductShopService>();
        services.AddScoped<IShopPromotionService, ShopPromotionService>();
        services.AddScoped<INewsService, NewsService>();
        services.AddScoped<IDocumentationService, DocumentationService>();
        services.AddSingleton<DocumentTreeCache>();
        services.AddScoped<ILogisticsKanbanService, LogisticsKanbanService>();
        services.AddScoped<LogisticsReminderDelivery>();
        services.AddScoped<LogisticsNotifier>();
        services.AddScoped<ITreasuryService, TreasuryService>();
        services.AddScoped<ITreasuryRecordsService, TreasuryRecordsService>();
        services.AddScoped<IGalleryTimelineService, GalleryTimelineService>();
        services.AddScoped<IEventAgendaService, EventAgendaService>();
        services.AddScoped<IEventAdminService, EventAdminService>();
        services.AddScoped<IEventRepertoireAdminService, EventRepertoireAdminService>();
        services.AddScoped<IEventParticipantsAdminService, EventParticipantsAdminService>();
        services.AddScoped<IEventDiscussionBoardService, EventDiscussionBoardService>();
        services.AddScoped<IEventContactsAdminService, EventContactsAdminService>();
        services.AddScoped<IRehearsalAgendaService, RehearsalAgendaService>();
        services.AddScoped<IRehearsalAdminService, RehearsalAdminService>();
        // As minhas ações pendentes. One source per panel, in page order (docs/react-pending-actions.md).
        services.AddScoped<IPendingActionSource, EventResponsePendingActionSource>();
        services.AddScoped<IPendingActionSource, RehearsalResponsePendingActionSource>();
        services.AddScoped<IPendingActionSource, LogisticsPendingActionSource>();
        services.AddScoped<IPendingActionSource, QuestionsPendingActionSource>();
        services.AddScoped<IPendingActionSource, TreasuryPendingActionSource>();
        services.AddScoped<IPendingActionSource, SecretaryPendingActionSource>();
        services.AddScoped<IPendingActionSource, MeetingsPendingActionSource>();
        services.AddScoped<IPendingActionSource, RehearsalApprovalsPendingActionSource>();
        services.AddScoped<IPendingActionSource, RepertoirePendingActionSource>();
        services.AddScoped<IPendingActionSource, MembersInfoPendingActionSource>();
        services.AddScoped<IPendingActionSource, GalleryPendingActionSource>();
        services.AddScoped<IPendingActionSource, NewsPendingActionSource>();
        services.AddScoped<IPendingActionsService, PendingActionsService>();
        services.AddScoped<PushReminderService>();
        services.AddScoped<IGalleryManagementService, GalleryManagementService>();
        services.AddScoped<ISongContentService, SongContentService>();
        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<IRequestAdminService, RequestAdminService>();
        services.AddScoped<IPublicRequestService, PublicRequestService>();
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
        services.AddScoped<IGalleryMediaService, GalleryMediaService>();
        services.AddScoped<INaipeService, NaipeService>();
        services.AddScoped<INaipeContentFilterService, NaipeContentFilterService>();
        services.AddScoped<INaipeBoardService, NaipeBoardService>();

        return services;
    }
}
