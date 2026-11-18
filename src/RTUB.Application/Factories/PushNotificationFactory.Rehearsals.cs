using RTUB.Application.DTOs;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Factories;

// Push factory: rehearsals, reminders and attendance.
public partial class PushNotificationFactory
{
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
            Tag = $"rehearsal-{rehearsal.Id}",
            PreferenceKey = PushPreferenceCatalog.RehearsalsManagerNotice
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
            Tag = $"rehearsal-reminder-{rehearsal.Id}",
            PreferenceKey = PushPreferenceCatalog.RehearsalsReminder
        };
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
            Tag = $"rehearsal-attendance-{rehearsal.Id}",
            PreferenceKey = PushPreferenceCatalog.RehearsalsMemberAnswers
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
            Tag = $"rehearsal-cancellation-{rehearsal.Id}",
            PreferenceKey = PushPreferenceCatalog.RehearsalsMemberAnswers
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
            Tag = $"rehearsal-non-attendance-{rehearsal.Id}",
            PreferenceKey = PushPreferenceCatalog.RehearsalsMemberAnswers
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
            Tag = $"rehearsal-attendance-approval-{rehearsal.Id}",
            PreferenceKey = PushPreferenceCatalog.RehearsalsMyAttendance
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
            Tag = $"rehearsal-attendance-rejection-{rehearsal.Id}",
            PreferenceKey = PushPreferenceCatalog.RehearsalsMyAttendance
        };
    }
}
