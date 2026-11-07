using Microsoft.Extensions.Logging;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Task 069: the Logística event pushes - a new owner, a new collaborator, an @mention. Sent once, after the change is
/// saved (never for a refused or rolled-back change), never to the member who made it. The recipients were checked
/// against the board in the same transaction (they see it); expelled accounts get nothing (the push service and the
/// inbox skip them). Push plus the "Notificações RTUB" inbox copy, as every other RTUB push; no other channel, no
/// per-category preference yet (docs/push-notifications.md). A failure is logged and never fails the change.
/// </summary>
public sealed class LogisticsNotifier
{
    private readonly IPushNotificationService _push;
    private readonly IPushNotificationFactory _factory;
    private readonly ILogger<LogisticsNotifier> _logger;

    public LogisticsNotifier(IPushNotificationService push, IPushNotificationFactory factory, ILogger<LogisticsNotifier> logger)
    {
        _push = push;
        _factory = factory;
        _logger = logger;
    }

    public sealed record Card(int BoardId, int CardId, string Title, string BoardName);

    public async Task SendAsync(LogisticsNotice kind, Card card, IEnumerable<string> userIds, string? actorId, string? actorName = null)
    {
        var to = userIds.Where(u => !string.IsNullOrEmpty(u) && u != actorId).Distinct().ToList();
        if (to.Count == 0)
        {
            return;
        }

        try
        {
            await _push.SendToSelectedUsersAsync(to, _factory.CreateLogisticsNotification(kind, card.BoardId, card.CardId, card.Title, card.BoardName, actorName));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Logistics {Kind} notification for card {CardId} failed", kind, card.CardId);
        }
    }
}
