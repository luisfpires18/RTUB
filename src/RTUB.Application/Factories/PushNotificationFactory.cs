using System.Globalization;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
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

            return new SendPushNotificationDto
            {
                Title = $"Lembrete: {@event.Name}",
                Body = $"A atuação é em {daysText} ({eventDateStr}) no {@event.Location}. Não te esqueças de confirmar a tua presença!",
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
            Title = $"Nova {meetingTypeName}",
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
            _ => "Reunião"
        };
    }
}
