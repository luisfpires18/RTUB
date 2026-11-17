using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Factories;

// Push factory: pending-work reminders and member activity.
public partial class PushNotificationFactory
{
    /// <summary>
    /// Creates a push notification for pending public request reminders (sent to admins daily).
    /// </summary>
    public SendPushNotificationDto CreatePendingPublicRequestsReminderNotification(int pendingCount)
    {

        var requestUrl = "/requests";
        var bodyText = pendingCount == 1
            ? "Existe 1 pedido de atuação pendente a aguardar aprovação."
            : $"Existem {pendingCount} pedidos de atuação pendentes a aguardar aprovação.";

        return new SendPushNotificationDto
        {
            Title = "Lembrete: Pedidos Pendentes",
            Body = bodyText,
            Icon = "/icons/rtub-logo-192.png",
            Url = requestUrl,
            Tag = "pending-requests-reminder",
            PreferenceKey = PushPreferenceCatalog.RequestsPublic
        };
    }

    public SendPushNotificationDto CreatePendingActionsReminderNotification() => new()
    {
        Title = "As minhas ações pendentes",
        Body = "Tens ações pendentes para tratar. Abre a página para as consultar.",
        Icon = "/icons/rtub-logo-192.png",
        Url = "/pending-actions",
        Tag = "pending-actions-reminder",
        PreferenceKey = PushPreferenceCatalog.PendingActions
    };

    /// <summary>
    /// Creates a push notification for pending meeting request reminders.
    /// </summary>
    public SendPushNotificationDto CreatePendingMeetingRequestReminderNotification(MeetingRequest meetingRequest)
    {
        ArgumentNullException.ThrowIfNull(meetingRequest);

        var meetingsUrl = "/meetings";
        var meetingTypeName = FormatMeetingType(meetingRequest.RequestedMeetingType);

        return new SendPushNotificationDto
        {
            Title = $"Lembrete: Pedido de {meetingTypeName}",
            Body = $"O pedido \"{meetingRequest.Title}\" está pendente de aprovação.",
            Icon = "/icons/rtub-logo-192.png",
            Url = meetingsUrl,
            Tag = $"pending-meeting-request-{meetingRequest.Id}",
            PreferenceKey = PushPreferenceCatalog.MeetingsRequests
        };
    }

    /// <summary>
    /// Creates a push notification for pending rehearsal approvals.
    /// </summary>
    public SendPushNotificationDto CreatePendingRehearsalApprovalsReminderNotification(int pendingRehearsalCount)
    {

        var rehearsalsUrl = "/rehearsals";
        var bodyText = pendingRehearsalCount == 1
            ? "Existe 1 ensaio passado com presenças pendentes para aprovar."
            : $"Existem {pendingRehearsalCount} ensaios passados com presenças pendentes para aprovar.";

        return new SendPushNotificationDto
        {
            Title = "Lembrete: Ensaios Pendentes",
            Body = bodyText,
            Icon = "/icons/rtub-logo-192.png",
            Url = rehearsalsUrl,
            Tag = "pending-rehearsal-approvals-reminder",
            PreferenceKey = PushPreferenceCatalog.RehearsalsApprovalsPending
        };
    }

    /// <summary>
    /// Creates a push notification for calotes (debt) reminder.
    /// </summary>
    public SendPushNotificationDto CreateCalotesReminderNotification(decimal amountOwed)
    {

        var calotesUrl = "/treasury/calotes";

        return new SendPushNotificationDto
        {
            Title = "Calote Pendente",
            Body = $"Deves {amountOwed:N2}€ à tuna",
            Icon = "/icons/rtub-logo-192.png",
            Url = calotesUrl,
            Tag = "calotes-reminder",
            PreferenceKey = PushPreferenceCatalog.TreasuryCalotes
        };
    }

    /// <summary>
    /// Creates a reminder push notification for a member to participate in activities.
    /// </summary>
    public SendPushNotificationDto CreateMemberActivityReminderNotification(string? userDisplayName, string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var eventsUrl = "/events";

        return new SendPushNotificationDto
        {
            Title = "Lembrete: Participa numa Atividade!",
            Body = $"Participa numa atividade este mês para voltares ao ativo, {userDisplayName}!",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventsUrl,
            Tag = $"member-reminder-{userId}-{DateTime.UtcNow.Ticks}",
            PreferenceKey = PushPreferenceCatalog.MembersStatus
        };
    }

    /// <summary>
    /// Creates a push notification when a member is reactivated.
    /// </summary>
    public SendPushNotificationDto CreateMemberReactivatedNotification(string userNickname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userNickname);

        var leaderboardUrl = "/leaderboard";

        return new SendPushNotificationDto
        {
            Title = "Membro Reativado",
            Body = $"{userNickname} está de volta!",
            Icon = "/icons/rtub-logo-192.png",
            Url = leaderboardUrl,
            Tag = $"member-reactivated-{userNickname}",
            PreferenceKey = PushPreferenceCatalog.MembersReactivated
        };
    }

    /// <summary>
    /// Creates a push notification warning a member that retirement is approaching (1 month away).
    /// </summary>
    public SendPushNotificationDto CreateMemberRetirementWarningNotification(string userNickname)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userNickname);

        var leaderboardUrl = "/leaderboard";

        return new SendPushNotificationDto
        {
            Title = "1 mês até reforma",
            Body = $"A tua reforma está a chegar, {userNickname}!",
            Icon = "/icons/rtub-logo-192.png",
            Url = leaderboardUrl,
            Tag = $"member-retirement-{userNickname}",
            PreferenceKey = PushPreferenceCatalog.MembersStatus
        };
    }
}
