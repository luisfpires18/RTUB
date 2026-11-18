using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Factories;

// Push factory: events, event custom push, logistics, enrollment, event videos.
public partial class PushNotificationFactory
{
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
                Tag = $"event-reminder-{@event.Id}-{DateTime.UtcNow:yyyyMMdd}",
                PreferenceKey = PushPreferenceCatalog.EventsReminder
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
                Tag = $"event-{@event.Id}",
                PreferenceKey = PushPreferenceCatalog.EventsNew
            };
        }
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
            Tag = $"event-custom-{@event.Id}",
            PreferenceKey = PushPreferenceCatalog.EventsManagerNotice
        };
    }

    // Push links are relative paths to the item itself: the service worker opens them on its own origin, so
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
            PreferenceKey = kind switch
            {
                LogisticsNotice.Mention => PushPreferenceCatalog.Mentions,
                LogisticsNotice.Reminder => PushPreferenceCatalog.LogisticsReminder,
                _ => PushPreferenceCatalog.LogisticsAssigned,
            },
        };
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
            Tag = $"event-enrollment-{@event.Id}",
            PreferenceKey = PushPreferenceCatalog.EventsMemberAnswers
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
            Tag = $"event-cancellation-{@event.Id}",
            PreferenceKey = PushPreferenceCatalog.EventsMemberAnswers
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
            Tag = $"event-non-enrollment-{@event.Id}",
            PreferenceKey = PushPreferenceCatalog.EventsMemberAnswers
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
            Tag = $"event-video-{@event.Id}",
            PreferenceKey = PushPreferenceCatalog.EventsVideo
        };
    }
}
