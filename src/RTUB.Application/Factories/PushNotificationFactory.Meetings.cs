using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Factories;

// Push factory: meetings, polls and meeting requests.
public partial class PushNotificationFactory
{
    /// <summary>
    /// Creates a push notification for meetings (new or reminder).
    /// </summary>
    /// <param name="meeting">The meeting to notify about</param>
    /// <param name="isReminder">True if this is a reminder notification, false for new meeting notification</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    public SendPushNotificationDto CreateMeetingNotification(Meeting meeting, bool isReminder)
    {
        ArgumentNullException.ThrowIfNull(meeting);

        var meetingUrl = MeetingUrl(meeting.Id);
        var meetingTypeName = FormatMeetingType(meeting.Type);
        var dateStr = meeting.Date.ToString("dd 'de' MMMM 'de' yyyy", PortugueseCulture);

        if (isReminder)
        {
            // Create reminder notification
            var daysText = GetDaysUntilMeetingText(meeting.Date);
            var reminderPhrase = daysText == "hoje" ? "A reunião é hoje" : $"A reunião é em {daysText}";

            return new SendPushNotificationDto
            {
                Title = $"Lembrete: {meetingTypeName}",
                Body = $"{reminderPhrase} ({dateStr}). Não te esqueças de confirmar a tua presença!",
                Icon = "/icons/rtub-logo-192.png",
                Url = meetingUrl,
                Tag = $"meeting-reminder-{meeting.Id}",
                PreferenceKey = PushPreferenceCatalog.MeetingsReminder
            };
        }
        else
        {
            // Create new meeting notification
            return new SendPushNotificationDto
            {
                Title = $"Nova Reunião Convocada: {meetingTypeName}",
                Body = $"{meeting.Title} - {dateStr}",
                Icon = "/icons/rtub-logo-192.png",
                Url = meetingUrl,
                Tag = $"meeting-{meeting.Id}",
                // A proposed request has no meeting yet (MeetingRequestService sends a temporary meeting with Id 0).
                PreferenceKey = meeting.Id > 0 ? PushPreferenceCatalog.MeetingsNew : PushPreferenceCatalog.MeetingsRequests
            };
        }
    }

    /// <summary>
    /// Creates a custom push notification for a meeting.
    /// </summary>
    public SendPushNotificationDto CreateMeetingCustomNotification(Meeting meeting, string customBody)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        ArgumentException.ThrowIfNullOrWhiteSpace(customBody);

        var meetingUrl = MeetingUrl(meeting.Id);
        var title = $"Reunião - {FormatEventDate(meeting.Date)}";

        return new SendPushNotificationDto
        {
            Title = title,
            Body = customBody,
            Icon = "/icons/rtub-logo-192.png",
            Url = meetingUrl,
            Tag = $"meeting-custom-{meeting.Id}",
            PreferenceKey = PushPreferenceCatalog.MeetingsManagerNotice
        };
    }

    /// <summary>
    /// Creates a push notification for a new poll (Votação).
    /// </summary>
    public SendPushNotificationDto CreateMeetingPollNotification(MeetingPoll poll, string audienceLabel)
    {
        ArgumentNullException.ThrowIfNull(poll);
        ArgumentException.ThrowIfNullOrWhiteSpace(audienceLabel);

        return new SendPushNotificationDto
        {
            Title = $"Nova votação: {audienceLabel}",
            Body = poll.Question,
            Icon = "/icons/rtub-logo-192.png",
            Url = "/meetings/polls",
            Tag = $"meeting-poll-{poll.Id}",
            PreferenceKey = PushPreferenceCatalog.MeetingsPolls
        };
    }

    /// <summary>
    /// Gets the text for days until meeting (similar to GetDaysUntilEventText).
    /// </summary>
    private string? GetDaysUntilMeetingText(DateTime meetingDate)
    {
        var today = Today;
        var daysUntil = (meetingDate.Date - today).Days;

        return daysUntil switch
        {
            < 0 => null, // Past meeting
            0 => "hoje",
            1 => "amanhã",
            _ => $"{daysUntil} dias"
        };
    }

    /// <summary>
    /// Creates a push notification for new performance requests.
    /// </summary>
    public SendPushNotificationDto CreateRequestNotification(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestUrl = "/requests";
        var dateStr = request.PreferredDate.ToString("dd/MM/yyyy");

        return new SendPushNotificationDto
        {
            Title = "Novo Pedido de Atuação",
            Body = $"{request.Name} - {request.EventType} em {dateStr}",
            Icon = "/icons/rtub-logo-192.png",
            Url = requestUrl,
            Tag = $"request-{request.Id}",
            PreferenceKey = PushPreferenceCatalog.RequestsPublic
        };
    }

    /// <summary>
    /// Formats meeting type for display.
    /// </summary>
    private static string FormatMeetingType(MeetingType type)
    {
        return type switch
        {
            MeetingType.AssembleiaGeralOrdinaria => "Assembleia Geral Ordinária",
            MeetingType.AssembleiaGeralExtraordinaria => "Assembleia Geral Extraordinária",
            MeetingType.ConselhoVeteranos => "Conselho de Veteranos",
            MeetingType.ReuniaoDirecao => "Reunião de Direção",
            _ => "Reunião"
        };
    }

    /// <summary>
    /// Creates a push notification when a meeting request is rejected or expired.
    /// </summary>
    /// <param name="meetingRequest">The meeting request that was rejected</param>
    /// <param name="isExpired">Whether the request expired due to date</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    public SendPushNotificationDto CreateMeetingRequestRejectedNotification(MeetingRequest meetingRequest, bool isExpired)
    {
        ArgumentNullException.ThrowIfNull(meetingRequest);

        var meetingsUrl = "/meetings";
        var title = "Pedido de reunião rejeitado";
        var body = isExpired
            ? $"O teu pedido \"{meetingRequest.Title}\" expirou porque a data proposta já passou."
            : $"O teu pedido \"{meetingRequest.Title}\" foi rejeitado.";

        return new SendPushNotificationDto
        {
            Title = title,
            Body = body,
            Icon = "/icons/rtub-logo-192.png",
            Url = meetingsUrl,
            Tag = $"meeting-request-rejected-{meetingRequest.Id}",
            PreferenceKey = PushPreferenceCatalog.MeetingsRequests
        };
    }
}
