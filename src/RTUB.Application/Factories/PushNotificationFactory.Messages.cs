using RTUB.Application.DTOs;
using RTUB.Application.Services;

namespace RTUB.Application.Factories;

// Push factory: weekly summary, conversation messages and the test push.
public partial class PushNotificationFactory
{
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
            Tag = "weekly-summary",
            PreferenceKey = PushPreferenceCatalog.SummaryWeekly
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
            PreferenceKey = PushPreferenceCatalog.Messages,
            // The message itself is in that conversation; no second copy in Notificações RTUB.
            KeepInInbox = false
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
