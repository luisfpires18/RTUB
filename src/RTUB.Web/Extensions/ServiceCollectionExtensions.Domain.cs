using Microsoft.Extensions.DependencyInjection.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;

namespace RTUB.Web.Extensions;

// Domain service registrations: rehearsals, meetings, inventory, discussions, members, roles, questions, ranking, finance, push.

public static partial class ServiceCollectionExtensions
{
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

        // The React /meetings, over the services above.
        services.AddScoped<IMeetingBoardService, MeetingBoardService>();
        services.AddScoped<IMeetingPollBoardService, MeetingPollBoardService>();

        // The React Owner tools: what /emails, /notifications, /users, /owner/tracing and /owner/db did.
        services.AddScoped<IOwnerOperationsService, OwnerOperationsService>();

        // The React /forgot-password, /reset-password and /confirm-email.
        services.AddScoped<AccountRecoveryService>();

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
    /// Registers member-related services
    /// </summary>
    public static IServiceCollection AddMemberServices(this IServiceCollection services)
    {
        services.AddScoped<IMemberFilterService, MemberFilterService>();
        services.AddScoped<IActiveMemberFilterService, ActiveMemberFilterService>();
        services.AddScoped<IMemberMentorService, MemberMentorService>();
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
        services.AddScoped<IQuestionBoardService, QuestionBoardService>();
        services.AddScoped<IMessagingService, MessagingService>(); // Messaging v2, React /messages
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
    /// Registers finance and transaction services
    /// </summary>
    public static IServiceCollection AddFinanceServices(this IServiceCollection services)
    {
        services.AddScoped<IFinanceManagementService, FinanceManagementService>();
        services.AddScoped<IFiscalYearHelper, FiscalYearHelperService>();
        services.AddScoped<IRequestToEventService, RequestToEventService>();
        services.AddScoped<IRequestValidationService, RequestValidationService>();

        return services;
    }

    /// <summary>
    /// Registers member query and statistics services
    /// Provides optimized queries for member pages (Leaderboard, Members)
    /// </summary>
    public static IServiceCollection AddMemberQueryServices(this IServiceCollection services)
    {
        services.AddScoped<IMemberStatisticsService, MemberStatisticsService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IMemberStatusService, MemberStatusService>();

        return services;
    }

    /// <summary>
    /// Registers Web Push notification services
    /// </summary>
    public static IServiceCollection AddPushNotificationServices(this IServiceCollection services)
    {
        services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
        // One pooled HttpClient for every push send: WebPushClient otherwise news up its own client (its own handler
        // and connection pool) per request scope and nothing ever disposes it.
        services.AddHttpClient(PushNotificationService.HttpClientName);
        services.AddScoped<NotificationInbox>();
        services.AddScoped<PushPreferenceService>();
        services.AddScoped<IPushPreferenceStore>(sp => sp.GetRequiredService<PushPreferenceService>());
        services.AddScoped<IPushNotificationService, PushNotificationService>();
        services.AddScoped<IPushNotificationFactory, RTUB.Application.Factories.PushNotificationFactory>();

        return services;
    }
}
