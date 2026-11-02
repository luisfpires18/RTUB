using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// "Notificações RTUB" (task 058): every push is also kept, for later reference, in each recipient's read-only system
/// conversation in /messages - whether or not the member has push turned on. The copy is title + body with the push's
/// link, saved as already read (it never moves the unread badge). Chat-message pushes are not copied
/// (<see cref="SendPushNotificationDto.KeepInInbox"/>); the /profile test push never comes through here. Expelled accounts
/// get nothing. A failure is logged and never fails the push.
/// </summary>
public sealed class NotificationInbox
{
    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly ILogger<NotificationInbox> _logger;
    private readonly TimeProvider _time;

    public NotificationInbox(IDbContextFactory<ApplicationDbContext> contexts, ILogger<NotificationInbox> logger, TimeProvider? time = null)
    {
        _contexts = contexts;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    /// <summary><paramref name="userIds"/> null = everyone (a broadcast): every account that is not expelled.</summary>
    public async Task KeepAsync(IEnumerable<string>? userIds, SendPushNotificationDto notification)
    {
        if (!notification.KeepInInbox)
        {
            return;
        }

        try
        {
            await using var db = await _contexts.CreateDbContextAsync();
            var wanted = userIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            var recipients = await db.Users.AsNoTracking()
                .Where(u => !u.IsExpelled && (wanted == null || wanted.Contains(u.Id)))
                .Select(u => u.Id)
                .ToListAsync();
            if (recipients.Count == 0)
            {
                return;
            }

            // One system conversation per member (Participants = their id); the oldest wins if v1 left more than one.
            var inboxes = (await db.Conversations
                    .Where(c => c.IsSystemConversation && recipients.Contains(c.Participants))
                    .OrderBy(c => c.Id)
                    .ToListAsync())
                .GroupBy(c => c.Participants)
                .ToDictionary(g => g.Key, g => g.First());

            var now = _time.GetUtcNow().UtcDateTime;
            var body = string.IsNullOrWhiteSpace(notification.Body) ? notification.Title : $"{notification.Title}\n\n{notification.Body}";
            if (body.Length > MessagingService.MaxBodyLength)
            {
                body = body[..MessagingService.MaxBodyLength];
            }

            foreach (var userId in recipients)
            {
                if (!inboxes.TryGetValue(userId, out var inbox))
                {
                    inbox = new Conversation
                    {
                        Participants = userId, IsSystemConversation = true, Title = MessagingService.SystemTitle,
                        CreatedByUserId = MessagingGroups.SystemCreator, CreatedAt = now,
                    };
                    db.Conversations.Add(inbox);
                }

                inbox.IsArchived = false;
                inbox.LastMessageAt = now;
                db.Messages.Add(new Message
                {
                    Conversation = inbox, IsSystem = true, Body = body, Link = notification.Url, ReadBy = userId, CreatedAt = now,
                });
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never the text: only the push's tag.
            _logger.LogWarning(ex, "Could not keep push {Tag} in Notificações RTUB", notification.Tag ?? "none");
        }
    }
}
