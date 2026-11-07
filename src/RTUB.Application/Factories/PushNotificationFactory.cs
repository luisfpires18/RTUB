using System.Globalization;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Application.Factories;

/// <summary>
/// Factory for creating push notification DTOs.
/// Centralizes all notification construction logic including titles, bodies, icons, tags, and URLs.
/// </summary>
public class PushNotificationFactory : IPushNotificationFactory
{
    private static readonly CultureInfo PortugueseCulture = new("pt-PT");

    private readonly TimeProvider _time;

    /// <summary>"hoje" / "amanhã" / "N dias" count Portugal's calendar days (#226 F02; was the server's UTC date).</summary>
    public PushNotificationFactory(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    private DateTime Today => PortugalTime.Today(_time);

    /// <summary>
    /// Creates a push notification for an event (new event or reminder).
    /// </summary>
    /// <param name="event">The event to notify about</param>
    /// <param name="isReminder">True if this is a reminder notification, false for new event notification</param>
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    public SendPushNotificationDto CreateEventNotification(Event @event, bool isReminder)
    {
        ArgumentNullException.ThrowIfNull(@event);

        // Build the event URL
        var eventUrl = EventUrl(@event.Id);

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
                Tag = $"event-reminder-{@event.Id}-{DateTime.UtcNow:yyyyMMdd}"
            };
        }
        else
        {
            // Create new event notification
            var eventTypeDisplay = StatusHelper.GetEventTypeDisplay(@event.Type);
            return new SendPushNotificationDto
            {
                Title = @event.Name,
                Body = $"Novo evento do tipo \"{eventTypeDisplay}\" - {@event.Name}. Confirma a tua presença.",
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
    /// <returns>A SendPushNotificationDto ready to be sent</returns>
    public SendPushNotificationDto CreateRehearsalNotification(Rehearsal rehearsal, string customBody)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(customBody);

        // Build the rehearsal URL
        var rehearsalUrl = RehearsalUrl(rehearsal.Id);

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
    /// Creates a reminder push notification for a rehearsal.
    /// Title format: "Ensaio - DD/MMM DayOfWeek", body indicates upcoming rehearsal.
    /// Used by background schedulers (day before and same day).
    /// </summary>
    public SendPushNotificationDto CreateRehearsalReminderNotification(Rehearsal rehearsal)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);

        var rehearsalUrl = RehearsalUrl(rehearsal.Id);
        var title = FormatRehearsalTitle(rehearsal.Date);

        var whenText = rehearsal.Date.Date == Today
            ? "é hoje"
            : "é amanhã";

        var locationText = string.IsNullOrWhiteSpace(rehearsal.Location)
            ? string.Empty
            : $" no {rehearsal.Location}";

        return new SendPushNotificationDto
        {
            Title = title,
            Body = $"O ensaio {whenText}{locationText}. Não te esqueças de confirmar a tua presença!",
            Icon = "/icons/rtub-logo-192.png",
            Url = rehearsalUrl,
            Tag = $"rehearsal-reminder-{rehearsal.Id}"
        };
    }

    /// <summary>
    /// Creates a custom push notification for an event.
    /// Mirrors the rehearsal custom notification pattern but uses the event context.
    /// </summary>
    public SendPushNotificationDto CreateEventCustomNotification(Event @event, string customBody)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(customBody);

        var eventUrl = EventUrl(@event.Id);

        // Use the event name as the title to keep things familiar
        var title = @event.Name;

        return new SendPushNotificationDto
        {
            Title = title,
            Body = customBody,
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-custom-{@event.Id}"
        };
    }

    // Push links are relative paths to the item itself (task 058): the service worker opens them on its own origin, so
    // they never depend on the request's Host header, and a tap lands on the event / rehearsal, not on a list.
    public SendPushNotificationDto CreateLogisticsNotification(LogisticsNotice kind, int boardId, int cardId, string cardTitle, string boardName,
        string? actorName = null, DateTime? dueDate = null)
    {
        var card = $"«{Cut(cardTitle, 80)}» ({Cut(boardName, 60)})";
        var (title, body) = kind switch
        {
            LogisticsNotice.Owner => ("Logística: tarefa atribuída", $"És o responsável por {card}."),
            LogisticsNotice.Collaborator => ("Logística: tarefa atribuída", $"Foste adicionado como colaborador em {card}."),
            LogisticsNotice.Mention => ("Logística: mencionaram-te", $"{actorName ?? "Alguém"} mencionou-te em {card}."),
            _ => ("Lembrete de Logística", dueDate is { } due ? $"{card} · prazo {due.ToString("dd/MM", PortugueseCulture)}" : card),
        };

        return new SendPushNotificationDto
        {
            Title = title,
            Body = body,
            Icon = "/icons/rtub-logo-192.png",
            Url = $"/logistics/{boardId}?card={cardId}",
            Tag = $"logistics-card-{cardId}",
        };
    }

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    private static string EventUrl(int eventId) => $"/events/{eventId}";

    private static string DiscussionUrl(int eventId) => $"/events/{eventId}/discussion";

    private static string RehearsalUrl(int rehearsalId) => $"/rehearsals/{rehearsalId}";

    private const string QuestionsUrl = "/questions";

    // /meetings has no page per meeting: ?meeting= opens that meeting's details there. A meeting not saved yet (a
    // request's preview, Id 0) opens the list.
    private static string MeetingUrl(int meetingId) => meetingId > 0 ? $"/meetings?meeting={meetingId}" : "/meetings";

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
    private string GetDaysUntilEventText(DateTime eventDate)
    {
        var daysUntil = (int)Math.Ceiling((eventDate.Date - Today).TotalDays);

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
    /// Creates a push notification for new discussion posts.
    /// </summary>
    public SendPushNotificationDto CreateDiscussionPostNotification(Event @event, string authorNickname, string postTitle)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorNickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(postTitle);

        var eventUrl = DiscussionUrl(@event.Id);

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
    /// Creates a push notification for new discussion comments.
    /// </summary>
    public SendPushNotificationDto CreateDiscussionCommentNotification(Event @event, string authorNickname, string postTitle)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorNickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(postTitle);

        var eventUrl = DiscussionUrl(@event.Id);

        return new SendPushNotificationDto
        {
            Title = $"Novo comentário de {authorNickname}",
            Body = $"{postTitle} - {@event.Name}",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-discussion-{@event.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification when someone enrolls in an event.
    /// </summary>
    public SendPushNotificationDto CreateEventEnrollmentNotification(Event @event, string userDisplayName)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);

        var eventUrl = EventUrl(@event.Id);
        var eventTypeDisplay = StatusHelper.GetEventTypeDisplay(@event.Type);

        return new SendPushNotificationDto
        {
            Title = "Nova inscrição",
            Body = $"{userDisplayName} vai a {eventTypeDisplay} - {@event.Name}",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-enrollment-{@event.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification for when a user cancels their event enrollment.
    /// </summary>
    public SendPushNotificationDto CreateEventCancellationNotification(Event @event, string userDisplayName)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);

        var eventUrl = EventUrl(@event.Id);
        var eventTypeDisplay = StatusHelper.GetEventTypeDisplay(@event.Type);

        return new SendPushNotificationDto
        {
            Title = "Inscrição cancelada",
            Body = $"{userDisplayName} já não vai a {eventTypeDisplay} - {@event.Name}",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-cancellation-{@event.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification when someone marks that they won't attend an event.
    /// </summary>
    public SendPushNotificationDto CreateEventNonEnrollmentNotification(Event @event, string userDisplayName)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);

        var eventUrl = EventUrl(@event.Id);
        var eventTypeDisplay = StatusHelper.GetEventTypeDisplay(@event.Type);

        return new SendPushNotificationDto
        {
            Title = "Não vai ao evento",
            Body = $"{userDisplayName} não vai a {eventTypeDisplay} - {@event.Name}",
            Icon = "/icons/rtub-logo-192.png",
            Url = eventUrl,
            Tag = $"event-non-enrollment-{@event.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification for new 1st place in leaderboard.
    /// </summary>
    public SendPushNotificationDto CreateLeaderboardFirstPlaceNotification(string userNickname, int level)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userNickname);

        var leaderboardUrl = "/leaderboard";

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
                Tag = $"meeting-reminder-{meeting.Id}"
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
                Tag = $"meeting-{meeting.Id}"
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
            Tag = $"meeting-custom-{meeting.Id}"
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
            Tag = $"meeting-request-rejected-{meetingRequest.Id}"
        };
    }

    /// <summary>
    /// Creates a push notification when someone marks attendance for a rehearsal.
    /// </summary>
    public SendPushNotificationDto CreateRehearsalAttendanceNotification(Rehearsal rehearsal, string userDisplayName)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);

        var rehearsalUrl = RehearsalUrl(rehearsal.Id);
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
    public SendPushNotificationDto CreateRehearsalCancellationNotification(Rehearsal rehearsal, string userDisplayName)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);

        var rehearsalUrl = RehearsalUrl(rehearsal.Id);
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
    public SendPushNotificationDto CreateRehearsalNonAttendanceNotification(Rehearsal rehearsal, string userDisplayName)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDisplayName);

        var rehearsalUrl = RehearsalUrl(rehearsal.Id);
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
    public SendPushNotificationDto CreateRehearsalAttendanceApprovalNotification(Rehearsal rehearsal, string approverName)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(approverName);

        var rehearsalUrl = RehearsalUrl(rehearsal.Id);
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
    public SendPushNotificationDto CreateRehearsalAttendanceRejectionNotification(Rehearsal rehearsal, string rejectorName)
    {
        ArgumentNullException.ThrowIfNull(rehearsal);
        ArgumentException.ThrowIfNullOrWhiteSpace(rejectorName);

        var rehearsalUrl = RehearsalUrl(rehearsal.Id);
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
    public SendPushNotificationDto CreateLeaderboardCommentNotification(string authorName, string targetUserName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetUserName);

        var leaderboardUrl = "/leaderboard";

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
    public SendPushNotificationDto CreateLeaderboardCommentLikeNotification(string likerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(likerName);

        var leaderboardUrl = "/leaderboard";

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
    public SendPushNotificationDto CreateEventVideoUploadNotification(Event @event, string uploaderName)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploaderName);

        var eventUrl = EventUrl(@event.Id);

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
    public SendPushNotificationDto CreateSongVideoUploadNotification(Song song, string uploaderName)
    {
        ArgumentNullException.ThrowIfNull(song);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploaderName);

        var musicUrl = $"/music/albums/{song.AlbumId}";

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
            Tag = "pending-requests-reminder"
        };
    }

    public SendPushNotificationDto CreatePendingActionsReminderNotification() => new()
    {
        Title = "As minhas ações pendentes",
        Body = "Tens ações pendentes para tratar. Abre a página para as consultar.",
        Icon = "/icons/rtub-logo-192.png",
        Url = "/pending-actions",
        Tag = "pending-actions-reminder"
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
            Tag = $"pending-meeting-request-{meetingRequest.Id}"
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
            Tag = "pending-rehearsal-approvals-reminder"
        };
    }

    /// <summary>
    /// Creates a push notification for a new question.
    /// </summary>
    public SendPushNotificationDto CreateNewQuestionNotification(string questionTitle, string authorName, int questionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);

        var questionsUrl = QuestionsUrl;

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
    public SendPushNotificationDto CreateQuestionReplyNotification(string replyPreview, int replyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyPreview);

        var questionsUrl = QuestionsUrl;

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
    public SendPushNotificationDto CreateQuestionAnsweredNotification(string replyPreview, int replyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(replyPreview);

        var questionsUrl = QuestionsUrl;

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
    public SendPushNotificationDto CreateQuestionReminderNotification(string questionTitle, string authorName, int questionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(questionTitle);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);

        var questionsUrl = QuestionsUrl;

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
    public SendPushNotificationDto CreatePendingQuestionsNotification(int questionCount, string? firstAuthorNickname)
    {

        var questionsUrl = QuestionsUrl;

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

    /// <summary>
    /// Creates a push notification when new naipe content (video or image) is published.
    /// </summary>
    public SendPushNotificationDto CreateNaipeContentNotification(string contentTitle, InstrumentType instrumentType, bool isVideo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentTitle);
        var instrumentTypeName = StatusHelper.GetInstrumentDisplay(instrumentType);

        var naipesUrl = $"/naipes?instrument={instrumentType}";
        var contentType = isVideo ? "vídeo" : "imagem";

        return new SendPushNotificationDto
        {
            Title = $"Novo {contentType} em {instrumentTypeName}",
            Body = $"\"{TruncateContent(contentTitle, 60)}\" foi adicionado aos Naipes",
            Icon = "/icons/rtub-logo-192.png",
            Url = naipesUrl,
            Tag = $"naipe-content-{instrumentTypeName.ToLower().Replace(" ", "-")}"
        };
    }

    /// <summary>
    /// Creates a push notification for weekly summary of events, rehearsals, and meetings.
    /// </summary>
    public SendPushNotificationDto CreateWeeklySummaryNotification(int eventCount, int rehearsalCount, int meetingCount)
    {

        var totalCount = eventCount + rehearsalCount + meetingCount;

        // Build activity summary
        var activities = new List<string>();
        if (eventCount > 0)
        {
            activities.Add(eventCount == 1 ? "1 atuação" : $"{eventCount} atuações");
        }
        if (rehearsalCount > 0)
        {
            activities.Add(rehearsalCount == 1 ? "1 ensaio" : $"{rehearsalCount} ensaios");
        }
        if (meetingCount > 0)
        {
            activities.Add(meetingCount == 1 ? "1 reunião" : $"{meetingCount} reuniões");
        }

        var activitySummary = string.Join(", ", activities);

        // Determine the URL to navigate to based on what's scheduled
        string url;
        if (eventCount > 0)
        {
            url = "/events";
        }
        else if (rehearsalCount > 0)
        {
            url = "/rehearsals";
        }
        else if (meetingCount > 0)
        {
            url = "/meetings";
        }
        else
        {
            url = "/";
        }

        return new SendPushNotificationDto
        {
            Title = "Resumo Semanal",
            Body = totalCount == 0
                ? "Não há atividades agendadas para esta semana."
                : $"Esta semana tens: {activitySummary}",
            Icon = "/icons/rtub-logo-192.png",
            Url = url,
            Tag = "weekly-summary"
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
            Tag = "calotes-reminder"
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
            Tag = $"member-reminder-{userId}-{DateTime.UtcNow.Ticks}"
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
            Tag = $"member-reactivated-{userNickname}"
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
            Tag = $"member-retirement-{userNickname}"
        };
    }

    /// <summary>
    /// Messaging v2 (task 041): a new chat message. Never carries the message text: the title is generic (it is the
    /// only part push logging records) and the body names the sender and, for a group, the group. The link is
    /// relative, so it never depends on the request's Host header.
    /// </summary>
    public SendPushNotificationDto CreateConversationMessageNotification(string senderName, string? groupTitle, int conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);

        return new SendPushNotificationDto
        {
            Title = "Nova mensagem",
            Body = string.IsNullOrWhiteSpace(groupTitle)
                ? $"{senderName} enviou-te uma mensagem."
                : $"{senderName} escreveu em {groupTitle}.",
            Icon = "/icons/rtub-logo-192.png",
            Url = $"/messages?c={conversationId}",
            Tag = $"message-{conversationId}",
            // The message itself is in that conversation; no second copy in Notificações RTUB.
            KeepInInbox = false
        };
    }

    /// <summary>
    /// Creates a push notification when a user is tagged in a gallery media item.
    /// </summary>
    public SendPushNotificationDto CreateGalleryTagNotification(int mediaId)
    {

        var galleryUrl = $"/gallery?item={mediaId}";

        return new SendPushNotificationDto
        {
            Title = "Nova media na Galeria",
            Body = "Foste marcado numa nova foto ou vídeo.",
            Icon = "/icons/rtub-logo-192.png",
            Url = galleryUrl,
            Tag = "gallery-tag"
        };
    }

    /// <summary>
    /// Creates a push notification when a user is @mentioned in a discussion post or comment.
    /// </summary>
    public SendPushNotificationDto CreateMentionNotification(Event @event, string mentionerNickname, string postTitle, bool isComment)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentException.ThrowIfNullOrWhiteSpace(mentionerNickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(postTitle);

        var discussionUrl = DiscussionUrl(@event.Id);
        var context = isComment ? $"num comentário sobre: {postTitle}" : $"num post: {postTitle}";

        return new SendPushNotificationDto
        {
            Title = $"{mentionerNickname} mencionou-te",
            Body = $"{@event.Name} - {context}",
            Icon = "/icons/rtub-logo-192.png",
            Url = discussionUrl,
            Tag = $"mention-{@event.Id}"
        };
    }

    /// <summary>The /profile test push (#230). No member input reaches it.</summary>
    public SendPushNotificationDto CreatePushTestNotification() => new()
    {
        Title = "RTUB: notificação de teste",
        Body = "Se estás a ver isto, as notificações push funcionam neste dispositivo.",
        Icon = "/icons/rtub-logo-192.png",
        Url = "/profile#push",
        Tag = "push-test"
    };
}
