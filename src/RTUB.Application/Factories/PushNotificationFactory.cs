using System.Globalization;
using RTUB.Application.Interfaces;
using RTUB.Core.Helpers;

namespace RTUB.Application.Factories;

/// <summary>
/// Factory for creating push notification DTOs.
/// Centralizes all notification construction logic including titles, bodies, icons, tags, and URLs.
/// </summary>
public partial class PushNotificationFactory : IPushNotificationFactory
{
    private static readonly CultureInfo PortugueseCulture = new("pt-PT");

    private readonly TimeProvider _time;

    /// <summary>"hoje" / "amanhã" / "N dias" count Portugal's calendar days (#226 F02; was the server's UTC date).</summary>
    public PushNotificationFactory(TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
    }

    private DateTime Today => PortugalTime.Today(_time);

    private static string Cut(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    private static string EventUrl(int eventId) => $"/events/{eventId}";

    private static string DiscussionUrl(int eventId) => $"/events/{eventId}/discussion";

    private static string RehearsalUrl(int rehearsalId) => $"/rehearsals/{rehearsalId}";

    private const string QuestionsUrl = "/questions";

    // /meetings has no page per meeting: ?meeting= opens that meeting's details there. A meeting not saved yet (a
    // request's preview, Id 0) opens the list.
    private static string MeetingUrl(int meetingId) => meetingId > 0 ? $"/meetings?meeting={meetingId}" : "/meetings";
}
