// Owner tools: email and push recipients, subscriptions, sending email.
using System.Data.Common;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;

namespace RTUB.Application.Services;

public partial class OwnerOperationsService
{
    // ---------- /emails ----------

    public async Task<OwnerEmailRecipients> GetEmailRecipientsAsync()
    {
        var people = await EmailAccountsAsync();
        return new OwnerEmailRecipients(
            people.Where(u => u.Subscribed).Select(Person).ToList(),
            people.Where(u => !u.Subscribed).Select(Person).ToList());
    }

    public async Task<EventResult<OwnerSubscriptionResult>> SetSubscriptionAsync(OwnerSubscriptionInput input)
    {
        var ids = (input.UserIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0)
        {
            return EventResult<OwnerSubscriptionResult>.Invalid("userIds", "Escolha pelo menos um utilizador.");
        }

        var changed = 0;
        var errors = new List<string>();
        foreach (var id in ids)
        {
            var user = await _users.FindByIdAsync(id);
            if (user is null)
            {
                continue;
            }

            user.Subscribed = input.Subscribed;
            var result = await _users.UpdateAsync(user);
            if (result.Succeeded)
            {
                changed++;
            }
            else
            {
                errors.Add($"{DisplayName(user)}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }

        return EventResult<OwnerSubscriptionResult>.Ok(new OwnerSubscriptionResult(changed, errors, await GetEmailRecipientsAsync()));
    }

    public async Task<EventResult<OwnerEmailResult>> SendEmailAsync(OwnerEmailInput input)
    {
        var errors = new Dictionary<string, string[]>();
        Require(errors, "title", input.Title, EmailTitleMax, "O título é obrigatório.");
        Require(errors, "content", input.Content, EmailContentMax, "O conteúdo é obrigatório.");
        if (errors.Count > 0)
        {
            return new EventResult<OwnerEmailResult>(EventResultStatus.Invalid, Errors: errors);
        }

        // Who is subscribed now, not when the page was opened; one email per address.
        var recipients = (await EmailAccountsAsync())
            .Where(u => u.Subscribed)
            .DistinctBy(u => u.Email!, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (recipients.Count == 0)
        {
            return EventResult<OwnerEmailResult>.Invalid("recipients", "Não há utilizadores subscritos para enviar o email.");
        }

        var (success, count, message) = await _email.SendAnnouncementEmailAsync(
            input.Title!,
            input.Content!,
            recipients.Select(u => u.Email!).ToList(),
            recipients.ToDictionary(u => u.Email!, u => (u.Nickname ?? u.FirstName ?? "", $"{u.FirstName} {u.LastName}"), StringComparer.OrdinalIgnoreCase));

        return EventResult<OwnerEmailResult>.Ok(new OwnerEmailResult(success, count, recipients.Count, success ? null : message ?? "Erro ao enviar emails."));
    }

    /// <summary>Accounts with a confirmed address, as the old page listed them.</summary>
    private async Task<List<ApplicationUser>> EmailAccountsAsync()
    {
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking()
            .Where(u => u.EmailConfirmed && u.Email != null && u.Email != "")
            .OrderBy(u => u.Nickname ?? u.FirstName)
            .ToListAsync();
    }

    // ---------- /notifications ----------

    public async Task<OwnerPushRecipients> GetPushRecipientsAsync() =>
        new((await PushAccountsAsync()).Select(Person).ToList());

    public async Task<EventResult<OwnerPushResult>> SendPushAsync(OwnerPushInput input, ClaimsPrincipal actor)
    {
        var errors = new Dictionary<string, string[]>();
        Require(errors, "title", input.Title, PushTitleMax, "O título é obrigatório.");
        Require(errors, "body", input.Body, PushBodyMax, "A mensagem é obrigatória.");
        if (errors.Count > 0)
        {
            return new EventResult<OwnerPushResult>(EventResultStatus.Invalid, Errors: errors);
        }

        // Only accounts the page could list (with a push subscription).
        var subscribed = (await _push.GetSubscribedUserIdsAsync()).ToHashSet();
        var userIds = (input.UserIds ?? []).Where(subscribed.Contains).Distinct().ToList();
        if (userIds.Count == 0)
        {
            return EventResult<OwnerPushResult>.Invalid("userIds", "Selecione pelo menos um utilizador para receber a notificação push.");
        }

        var notification = new SendPushNotificationDto
        {
            Title = input.Title!,
            Body = input.Body!,
            Icon = "/icons/rtub-logo-192.png",
            Url = "/",
            Tag = $"announcement-{DateTime.UtcNow.Ticks}",
            PreferenceKey = PushPreferenceCatalog.OwnerAnnouncement,
        };
        var (sent, failed) = await _push.SendToSelectedUsersAsync(userIds, notification);

        // Audited as before; a failure here never fails the send.
        try
        {
            var actorId = actor.FindFirstValue(ClaimTypes.NameIdentifier);
            var actorName = actor.Identity?.Name;
            if (!string.IsNullOrEmpty(actorId) && !string.IsNullOrEmpty(actorName))
            {
                await _audit.AddAsync(new AuditLog
                {
                    EntityType = "Notification",
                    EntityId = null,
                    Action = "PushNotificationSent",
                    UserId = actorId,
                    UserName = actorName,
                    Timestamp = DateTime.UtcNow,
                    Changes = JsonSerializer.Serialize(new { RecipientCount = userIds.Count, NotificationBody = notification.Body }),
                    EntityDisplayName = notification.Title,
                    IsCriticalAction = false,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create audit log for push notification sent");
        }

        return EventResult<OwnerPushResult>.Ok(new OwnerPushResult(userIds.Count, sent, failed));
    }

    private async Task<List<ApplicationUser>> PushAccountsAsync()
    {
        var ids = (await _push.GetSubscribedUserIdsAsync()).ToHashSet();
        await using var db = await _contexts.CreateDbContextAsync();
        return await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .OrderBy(u => u.Nickname ?? u.FirstName)
            .ToListAsync();
    }
}
