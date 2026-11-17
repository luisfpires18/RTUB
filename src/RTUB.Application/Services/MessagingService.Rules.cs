// Messaging: send rules, notifications and small helpers.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

public sealed partial class MessagingService
{
    // ---------- helpers ----------

    private static string? UserId(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    /// <summary>The conversation, only if the member takes part in it and it is not archived; null otherwise (a 404).</summary>
    private static async Task<Conversation?> FindMineAsync(ApplicationDbContext db, int id, string me, bool tracked)
    {
        var query = tracked ? db.Conversations : db.Conversations.AsNoTracking();
        var conversation = await query.FirstOrDefaultAsync(c => c.Id == id && !c.IsArchived);
        return conversation is not null && conversation.HasParticipant(me) ? conversation : null;
    }

    /// <summary>Null: no such message for this member (404). Forbidden: a read-only "Sistema RTUB" message. Ok otherwise.</summary>
    private static async Task<EventResultStatus?> ReactableAsync(ApplicationDbContext db, int messageId, string me)
    {
        var target = await db.Messages.AsNoTracking().Where(m => m.Id == messageId)
            .Select(m => new { m.ConversationId }).FirstOrDefaultAsync();
        if (target is null || await FindMineAsync(db, target.ConversationId, me, tracked: false) is not { } conversation)
        {
            return null;
        }

        return conversation.IsSystemConversation ? EventResultStatus.Forbidden : EventResultStatus.Ok;
    }

    private static async Task<bool> CanSendAsync(ApplicationDbContext db, Conversation conversation, ClaimsPrincipal user, string me)
    {
        if (conversation.IsSystemConversation)
        {
            return false;
        }

        if (conversation.IsAnnouncementOnly)
        {
            return await CanAnnounceAsync(db, user, me);
        }

        if (!conversation.IsGroup)
        {
            // A one-to-one chat whose other member was deleted or expelled takes no new messages.
            var other = conversation.GetOtherParticipantId(me);
            return other is not null && await db.Users.AnyAsync(u => u.Id == other && !u.IsExpelled);
        }

        return true;
    }

    private static async Task<bool> CanAnnounceAsync(ApplicationDbContext db, ClaimsPrincipal user, string me)
    {
        if (user.IsInRole("Owner"))
        {
            return true;
        }

        var now = DateTime.UtcNow;
        var start = now.Month >= 9 ? now.Year : now.Year - 1;
        return await db.RoleAssignments.AnyAsync(r =>
            r.UserId == me && r.StartYear == start && r.EndYear == start + 1 && AnnouncementSenderPositions.Contains(r.Position));
    }

    /// <summary>Push to every other participant who has not muted the conversation. Never the message text.</summary>
    private async Task NotifyAsync(ApplicationDbContext db, Conversation conversation, string me, string senderName)
    {
        try
        {
            var recipients = conversation.GetParticipantIds().Where(id => id != me).ToList();
            var muted = await db.ConversationUserSettings.AsNoTracking()
                .Where(s => s.ConversationId == conversation.Id && s.IsMuted)
                .Select(s => s.UserId)
                .ToListAsync();
            recipients = recipients.Except(muted).ToList();
            if (recipients.Count == 0)
            {
                return;
            }

            // ponytail: sent inline and sequentially (SendToSelectedUsersAsync); a big group with many devices slows the
            // send by the push round trips. Move to a background queue if that becomes noticeable.
            var notification = _notifications.CreateConversationMessageNotification(senderName,
                conversation.IsGroup ? (string.IsNullOrWhiteSpace(conversation.Title) ? "Grupo" : conversation.Title) : null, conversation.Id);
            await _push.SendToSelectedUsersAsync(recipients, notification);
        }
        catch (Exception ex)
        {
            // The message is saved; a failed push must not fail the send. No text, no names in the log.
            _logger.LogWarning(ex, "Push for a new message in conversation {ConversationId} failed", conversation.Id);
        }
    }

    internal static MessagingPerson Person(string id, string? nickname, string? first, string? last, string? imageUrl)
    {
        var fullName = $"{first} {last}".Trim();
        var display = !string.IsNullOrWhiteSpace(nickname) ? nickname! : fullName.Length > 0 ? fullName : "Membro";
        return new MessagingPerson(id, display, fullName.Length == 0 ? null : fullName,
            EventAgendaService.IsSafeUrl(imageUrl) ? imageUrl! : EventAgendaService.DefaultAvatar);
    }

    /// <summary>
    /// Old "Sistema RTUB" messages carry links built from the request's host: only the path is kept, so a link can only
    /// open a page of this site. Anything that is not a plain https or site-relative link is dropped.
    /// </summary>
    internal static string? SafeLink(string? link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        var candidate = link.StartsWith('/')
            ? link
            : Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri.PathAndQuery : null;

        // One check on the final value for both branches: a path such as "//evil.org" (from "https://x//evil.org"),
        // "/\\evil.org" or "/<tab>/evil.org" (browsers drop tabs and newlines) would leave the site.
        return candidate is not null
            && candidate.Length > 0 && candidate[0] == '/'
            && (candidate.Length == 1 || (candidate[1] != '/' && candidate[1] != '\\'))
            && !candidate.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c == '\\')
            ? candidate
            : null;
    }

    private static string Truncate(string value, int max, bool ellipsis = false) =>
        value.Length <= max ? value : ellipsis ? value[..(max - 1)].TrimEnd() + "…" : value[..max];

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
