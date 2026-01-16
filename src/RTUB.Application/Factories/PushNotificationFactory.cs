using System.Globalization;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Factories;

/// <summary>
/// Factory for creating push notification DTOs.
/// Centralizes all notification construction logic including titles, bodies, icons, tags, and URLs.
/// </summary>
public class PushNotificationFactory : IPushNotificationFactory
{
    private static readonly CultureInfo PortugueseCulture = new("pt-PT");

    /// <summary>
    /// Creates a push notification for an event (new event or reminder).
    /// </summary>
    /// <param name="event">The event to notify about</param>
    /// <param name="isReminder">True if this is a reminder notification, false for new event notification</param>
    /// <param name="baseUrl">The base URL of the application (e.g., "https://rtub.example.com")</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    public SendPushNotificationDto CreateEventNotification(Event @event, bool isReminder, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        // Build the event URL
        var eventUrl = BuildEventUrl(baseUrl);

        // Format the date in Portuguese
        var eventDateStr = FormatEventDate(@event.Date);

        if (isReminder)
        {
            // Create reminder notification
            var daysText = GetDaysUntilEventText(@event.Date);
            var reminderPhrase = daysText == "hoje" ? "A atuação é hoje" : $"A atuação é em {daysText}";

            return new SendPushNotificationDto
            {
                Title = $"Lembrete: {@event.Name}",
                Body = $"{reminderPhrase} ({eventDateStr}) no {@event.Location}. Não te esqueças de confirmar a tua presença!",
                Icon = "/icons/rtub-logo-192.png",
                Url = eventUrl,
                Tag = $"event-reminder-{@event.Id}"
            };
        }
        else
        {
            // Create new event notification
            return new SendPushNotificationDto
            {
                Title = @event.Name,
                Body = $"Nova atuação: {@event.Name} em {eventDateStr} no {@event.Location}",
                Icon = "/icons/rtub-logo-192.png",
                Url = eventUrl,
                Tag = $"event-{@event.Id}"
            };
        }
    }

    /// <summary>
    /// Creates a custom push notification for a rehearsal.
    /// Title format: "Ensaio - DD/MMM DayOfWeek"
    /// </summary>
    /// <param name="rehearsal">The rehearsal to notify about</param>
    /// <param name="customBody">Custom message body provided by the user</param>
    /// <param name="baseUrl">The base URL of the application (e.g., "https://rtub.example.com")</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    public SendPushNotificationDto CreateRehearsalNotification(Rehearsal rehearsal, string customBody, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(customBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        // Build the rehearsal URL
        var rehearsalUrl = BuildRehearsalUrl(baseUrl);

        // Format the title: "Ensaio - 24/Nov Terça Feira"
        var title = FormatRehearsalTitle(rehearsal.Date);

        return new SendPushNotificationDto
        {
            Title = title,
            Body = customBody,
            Icon = "/icons/rtub-logo-192.png",
            Url = rehearsalUrl,
            Tag = $"rehearsal-{rehearsal.Id}"
        };
    }

    /// <summary>
    /// Builds the event URL from the base URL.
    /// </summary>
    private static string BuildEventUrl(string baseUrl)
    {
        var trimmedBaseUrl = baseUrl.TrimEnd('/');
        return $"{trimmedBaseUrl}/events";
    }

    /// <summary>
    /// Builds the rehearsal URL from the base URL.
    /// </summary>
    private static string BuildRehearsalUrl(string baseUrl)
    {
        var trimmedBaseUrl = baseUrl.TrimEnd('/');
        return $"{trimmedBaseUrl}/rehearsals";
    }

    /// <summary>
    /// Formats the rehearsal title in the format "Ensaio - DD/MMM DayOfWeek"
    /// Example: "Ensaio - 24/Nov Terça Feira"
    /// </summary>
    private static string FormatRehearsalTitle(DateTime date)
    {
        // Format: "Ensaio - 24/Nov Terça Feira"
        var day = date.ToString("dd", PortugueseCulture);
        var month = date.ToString("MMM", PortugueseCulture);
        var dayOfWeek = date.ToString("dddd", PortugueseCulture);

        // Capitalize first letter of day of week
        dayOfWeek = char.ToUpper(dayOfWeek[0]) + dayOfWeek.Substring(1);

        return $"Ensaio - {day}/{month} {dayOfWeek}";
    }

    /// <summary>
    /// Formats the event date in Portuguese format (dd 'de' MMMM 'de' yyyy).
    /// </summary>
    private static string FormatEventDate(DateTime eventDate)
    {
        return eventDate.ToString("dd 'de' MMMM 'de' yyyy", PortugueseCulture);
    }

    /// <summary>
    /// Gets the days until event formatted as text in Portuguese.
    /// Returns appropriate text like "1 dia" or "3 dias".
    /// Throws ArgumentException if the event is in the past.
    /// </summary>
    private static string GetDaysUntilEventText(DateTime eventDate)
    {
        var daysUntil = (int)Math.Ceiling((eventDate.Date - DateTime.UtcNow.Date).TotalDays);

        if (daysUntil < 0)
        {
            throw new ArgumentException("Cannot create reminder for past events", nameof(eventDate));
        }

        if (daysUntil == 0)
        {
            return "hoje";
        }

        return daysUntil == 1 ? "1 dia" : $"{daysUntil} dias";
    }

    /// <summary>
    /// Creates a push notification for event repertoire changes.
    /// </summary>
    public SendPushNotificationDto CreateEventRepertoireNotification(Event @event, string songTitle, bool isAdded, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(songTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var eventUrl = BuildEventUrl(baseUrl);
        var action = isAdded ? "adicionada" : "alterada";

        return new SendPushNotificationDto
        {
            Title = $"Repertório {action} - {@event.Name}",
            Body = $"A música \"{songTitle}\" foi {action} no repertório de {@event.Name}.",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-repertoire-{@event.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification for new discussion posts.
    /// </summary>
    public SendPushNotificationDto CreateDiscussionPostNotification(Event @event, string authorNickname, string postTitle, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorNickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(postTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var eventUrl = BuildEventUrl(baseUrl);

        return new SendPushNotificationDto
        {
            Title = $"Novo post de {authorNickname}",
            Body = $"{postTitle} - {@event.Name}",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-discussion-{@event.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification when someone enrolls in an event.
    /// </summary>
    public SendPushNotificationDto CreateEventEnrollmentNotification(Event @event, string userDisplayName, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var eventUrl = BuildEventUrl(baseUrl);
        var eventTypeDisplay = StatusHelper.GetEventTypeDisplay(@event.Type);

        return new SendPushNotificationDto
        {
            Title = "Nova inscrição",
            Body = $"{userDisplayName} vai a {eventTypeDisplay} {@event.Name}",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-enrollment-{@event.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification for when a user cancels their event enrollment.
    /// </summary>
    public SendPushNotificationDto CreateEventCancellationNotification(Event @event, string userDisplayName, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var eventUrl = BuildEventUrl(baseUrl);
        var eventTypeDisplay = StatusHelper.GetEventTypeDisplay(@event.Type);

        return new SendPushNotificationDto
        {
            Title = "Inscrição cancelada",
            Body = $"{userDisplayName} já não vai a {eventTypeDisplay} {@event.Name}",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-cancellation-{@event.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification when someone marks that they won't attend an event.
    /// </summary>
    public SendPushNotificationDto CreateEventNonEnrollmentNotification(Event @event, string userDisplayName, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var eventUrl = BuildEventUrl(baseUrl);
        var eventTypeDisplay = StatusHelper.GetEventTypeDisplay(@event.Type);

        return new SendPushNotificationDto
        {
            Title = "Não vai ao evento",
            Body = $"{userDisplayName} não vai a {eventTypeDisplay} {@event.Name}",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-non-enrollment-{@event.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification for new 1st place in leaderboard.
    /// </summary>
    public SendPushNotificationDto CreateLeaderboardFirstPlaceNotification(string userNickname, int level, string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userNickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var leaderboardUrl = $"{baseUrl.TrimEnd('/')}/leaderboard";

        return new SendPushNotificationDto
        {
            Title = "Novo 1º Lugar no Ranking!",
            Body = $"{userNickname} - Nível {level}",
            Icon = "/icons/rtub-logo-192.png",
            Url = leaderboardUrl,
            Tag = "leaderboard-first-place"
        };
    }

    /// <summary>
    /// Creates a push notification for new meetings.
    /// </summary>
    public SendPushNotificationDto CreateMeetingNotification(Meeting meeting, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(meeting);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var meetingUrl = $"{baseUrl.TrimEnd('/')}/meetings";
        var meetingTypeName = FormatMeetingType(meeting.Type);
        var dateStr = meeting.Date.ToString("dd 'de' MMMM 'de' yyyy", PortugueseCulture);

        return new SendPushNotificationDto
        {
            Title = $"Nova Reunião Convocada: {meetingTypeName}",
            Body = $"{meeting.Title} - {dateStr}",
            Icon = "/icons/rtub-logo-192.png",
            Url = meetingUrl,
            Tag = $"meeting-{meeting.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification for new performance requests.
    /// </summary>
    public SendPushNotificationDto CreateRequestNotification(Request request, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var requestUrl = $"{baseUrl.TrimEnd('/')}/requests";
        var dateStr = request.PreferredDate.ToString("dd/MM/yyyy");

        return new SendPushNotificationDto
        {
            Title = "Novo Pedido de Atuação",
            Body = $"{request.Name} - {request.EventType} em {dateStr}",
            Icon = "/icons/rtub-logo-192.png",
            Url = requestUrl,
            Tag = $"request-{request.Id}"
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
    /// Creates a push notification when someone marks attendance for a rehearsal.
    /// </summary>
    public SendPushNotificationDto CreateRehearsalAttendanceNotification(Rehearsal rehearsal, string userDisplayName, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var rehearsalUrl = BuildRehearsalUrl(baseUrl);
        var title = FormatRehearsalTitle(rehearsal.Date);

        return new SendPushNotificationDto
        {
            Title = "Nova presença",
            Body = $"{userDisplayName} vai ao {title}",
            Icon = "/icons/rtub-logo-192.png",
            Url = rehearsalUrl,
            Tag = $"rehearsal-attendance-{rehearsal.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification when a user cancels their rehearsal attendance.
    /// </summary>
    public SendPushNotificationDto CreateRehearsalCancellationNotification(Rehearsal rehearsal, string userDisplayName, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var rehearsalUrl = BuildRehearsalUrl(baseUrl);
        var title = FormatRehearsalTitle(rehearsal.Date);

        return new SendPushNotificationDto
        {
            Title = "Presença cancelada",
            Body = $"{userDisplayName} já não vai ao {title}",
            Icon = "/icons/rtub-logo-192.png",
            Url = rehearsalUrl,
            Tag = $"rehearsal-cancellation-{rehearsal.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification when someone marks that they won't attend a rehearsal.
    /// </summary>
    public SendPushNotificationDto CreateRehearsalNonAttendanceNotification(Rehearsal rehearsal, string userDisplayName, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var rehearsalUrl = BuildRehearsalUrl(baseUrl);
        var title = FormatRehearsalTitle(rehearsal.Date);

        return new SendPushNotificationDto
        {
            Title = "Não vai ao ensaio",
            Body = $"{userDisplayName} não vai ao {title}",
            Icon = "/icons/rtub-logo-192.png",
            Url = rehearsalUrl,
            Tag = $"rehearsal-non-attendance-{rehearsal.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification when admin approves a user's rehearsal attendance.
    /// </summary>
    public SendPushNotificationDto CreateRehearsalAttendanceApprovalNotification(Rehearsal rehearsal, string approverName, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(approverName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var rehearsalUrl = BuildRehearsalUrl(baseUrl);
        var title = FormatRehearsalTitle(rehearsal.Date);

        return new SendPushNotificationDto
        {
            Title = title,
            Body = $"{approverName} aprovou a tua presença",
            Icon = "/icons/rtub-logo-192.png",
            Url = rehearsalUrl,
            Tag = $"rehearsal-attendance-approval-{rehearsal.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification when admin rejects a user's rehearsal attendance.
    /// </summary>
    public SendPushNotificationDto CreateRehearsalAttendanceRejectionNotification(Rehearsal rehearsal, string rejectorName, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var rehearsalUrl = BuildRehearsalUrl(baseUrl);
        var title = FormatRehearsalTitle(rehearsal.Date);

        return new SendPushNotificationDto
        {
            Title = title,
            Body = $"{rejectorName} recusou a tua presença",
            Icon = "/icons/rtub-logo-192.png",
            Url = rehearsalUrl,
            Tag = $"rehearsal-attendance-rejection-{rehearsal.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification when someone comments on a user's leaderboard profile.
    /// </summary>
    public SendPushNotificationDto CreateLeaderboardCommentNotification(string authorName, string targetUserName, string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var leaderboardUrl = $"{baseUrl.TrimEnd('/')}/leaderboard";

        return new SendPushNotificationDto
        {
            Title = "Novo comentário no teu perfil",
            Body = $"{authorName} comentou no teu perfil",
            Icon = "/icons/rtub-logo-192.png",
            Url = leaderboardUrl,
            Tag = "leaderboard-comment"
        };
    }

    /// <summary>
    /// Creates a push notification when someone likes a user's comment.
    /// </summary>
    public SendPushNotificationDto CreateLeaderboardCommentLikeNotification(string likerName, string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(likerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var leaderboardUrl = $"{baseUrl.TrimEnd('/')}/leaderboard";

        return new SendPushNotificationDto
        {
            Title = "Novo like no teu comentário",
            Body = $"{likerName} gostou do teu comentário",
            Icon = "/icons/rtub-logo-192.png",
            Url = leaderboardUrl,
            Tag = "leaderboard-comment-like"
        };
    }

    /// <summary>
    /// Creates a push notification when someone uploads a video to an event.
    /// </summary>
    public SendPushNotificationDto CreateEventVideoUploadNotification(Event @event, string uploaderName, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploaderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var eventUrl = BuildEventUrl(baseUrl);

        return new SendPushNotificationDto
        {
            Title = "Novo vídeo",
            Body = $"{uploaderName} adicionou um vídeo a {@event.Name}",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-video-{@event.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification when someone uploads a video to a song.
    /// </summary>
    public SendPushNotificationDto CreateSongVideoUploadNotification(Song song, string uploaderName, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploaderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var musicUrl = $"{baseUrl.TrimEnd('/')}/music";

        return new SendPushNotificationDto
        {
            Title = "Novo vídeo",
            Body = $"{uploaderName} adicionou um vídeo a \"{song.Title}\"",
            Icon = "/icons/rtub-logo-192.png",
            Url = musicUrl,
            Tag = $"song-video-{song.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification for pending public request reminders (sent to admins daily).
    /// </summary>
    public SendPushNotificationDto CreatePendingPublicRequestsReminderNotification(int pendingCount, string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var requestUrl = $"{baseUrl.TrimEnd('/')}/requests";
        var bodyText = pendingCount == 1
            ? "Existe 1 pedido de atuação pendente a aguardar aprovação."
            : $"Existem {pendingCount} pedidos de atuação pendentes a aguardar aprovação.";

        return new SendPushNotificationDto
        {
            Title = "Lembrete: Pedidos Pendentes",
            Body = bodyText,
            Icon = "/icons/rtub-logo-192.png",
            Url = requestUrl,
            Tag = "pending-requests-reminder"
        };
    }

    /// <summary>
    /// Creates a push notification for pending meeting request reminders.
    /// </summary>
    public SendPushNotificationDto CreatePendingMeetingRequestReminderNotification(MeetingRequest meetingRequest, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(meetingRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var meetingsUrl = $"{baseUrl.TrimEnd('/')}/meetings";
        var meetingTypeName = FormatMeetingType(meetingRequest.RequestedMeetingType);

        return new SendPushNotificationDto
        {
            Title = $"Lembrete: Pedido de {meetingTypeName}",
            Body = $"O pedido \"{meetingRequest.Title}\" está pendente de aprovação.",
            Icon = "/icons/rtub-logo-192.png",
            Url = meetingsUrl,
            Tag = $"pending-meeting-request-{meetingRequest.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification for a new question.
    /// </summary>
    public SendPushNotificationDto CreateNewQuestionNotification(string questionTitle, string authorName, int questionId, string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var questionsUrl = BuildQuestionsUrl(baseUrl);

        return new SendPushNotificationDto
        {
            Title = "Nova Pergunta",
            Body = $"{authorName} fez uma pergunta para si: {TruncateContent(questionTitle, 100)}",
            Icon = "/icons/rtub-logo-192.png",
            Url = questionsUrl,
            Tag = $"question-{questionId}"
        };
    }

    /// <summary>
    /// Creates a push notification for a question reply from the author.
    /// </summary>
    public SendPushNotificationDto CreateQuestionReplyNotification(string replyPreview, int replyId, string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyPreview);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var questionsUrl = BuildQuestionsUrl(baseUrl);

        return new SendPushNotificationDto
        {
            Title = "Nova Resposta à Pergunta",
            Body = TruncateContent(replyPreview, 150),
            Icon = "/icons/rtub-logo-192.png",
            Url = questionsUrl,
            Tag = $"question-reply-{replyId}"
        };
    }

    /// <summary>
    /// Creates a push notification when the assigned member answers a question.
    /// </summary>
    public SendPushNotificationDto CreateQuestionAnsweredNotification(string replyPreview, int replyId, string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyPreview);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var questionsUrl = BuildQuestionsUrl(baseUrl);

        return new SendPushNotificationDto
        {
            Title = "Pergunta Respondida",
            Body = $"A sua pergunta foi respondida: {TruncateContent(replyPreview, 100)}",
            Icon = "/icons/rtub-logo-192.png",
            Url = questionsUrl,
            Tag = $"question-reply-{replyId}"
        };
    }

    /// <summary>
    /// Creates a push notification for a question reminder.
    /// </summary>
    public SendPushNotificationDto CreateQuestionReminderNotification(string questionTitle, string authorName, int questionId, string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var questionsUrl = BuildQuestionsUrl(baseUrl);

        return new SendPushNotificationDto
        {
            Title = "Lembrete: Pergunta Pendente",
            Body = $"{authorName} enviou um lembrete para a sua pergunta: {TruncateContent(questionTitle, 100)}",
            Icon = "/icons/rtub-logo-192.png",
            Url = questionsUrl,
            Tag = $"question-reminder-{questionId}"
        };
    }

    /// <summary>
    /// Creates a push notification for pending questions (background service).
    /// </summary>
    public SendPushNotificationDto CreatePendingQuestionsNotification(int questionCount, string? firstAuthorNickname, string baseUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

        var questionsUrl = BuildQuestionsUrl(baseUrl);

        return new SendPushNotificationDto
        {
            Title = questionCount == 1 ? "Pergunta Pendente" : $"{questionCount} Perguntas Pendentes",
            Body = questionCount == 1
                ? $"Tem uma pergunta à espera da sua resposta de {firstAuthorNickname ?? "um membro"}"
                : $"Tem {questionCount} perguntas à espera da sua resposta",
            Icon = "/icons/rtub-logo-192.png",
            Url = questionsUrl,
            Tag = "question-reminder"
        };
    }

    /// <summary>
    /// Builds the questions URL from the base URL.
    /// </summary>
    private static string BuildQuestionsUrl(string baseUrl)
    {
        var trimmedBaseUrl = baseUrl.TrimEnd('/');
        return $"{trimmedBaseUrl}/questions";
    }

    /// <summary>
    /// Truncates content to a maximum length with ellipsis.
    /// </summary>
    private static string TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxLength)
        {
            return content;
        }
        return content[..(maxLength - 3)] + "...";
    }
}
