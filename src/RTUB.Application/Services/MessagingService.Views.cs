// Messaging: builds the list, detail and message items returned to the portal.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class MessagingService
{
    private async Task<List<ConversationListItem>> ListItemsAsync(ApplicationDbContext db, List<Conversation> conversations,
        ClaimsPrincipal user, string me)
    {
        if (conversations.Count == 0)
        {
            return new List<ConversationListItem>();
        }

        var ids = conversations.Select(c => c.Id).ToList();
        var unread = await db.Messages.AsNoTracking()
            .Where(m => ids.Contains(m.ConversationId) && m.SenderId != me && !m.ReadBy.Contains(me))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var lastIds = await db.Messages.AsNoTracking()
            .Where(m => ids.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.Max(m => m.Id))
            .ToListAsync();
        var last = await db.Messages.AsNoTracking()
            .Where(m => lastIds.Contains(m.Id))
            .Select(m => new { m.ConversationId, m.Body, m.SenderId, m.CreatedAt })
            .ToDictionaryAsync(m => m.ConversationId);

        var settings = await db.ConversationUserSettings.AsNoTracking()
            .Where(s => s.UserId == me && ids.Contains(s.ConversationId))
            .ToDictionaryAsync(s => s.ConversationId);

        var people = await PeopleAsync(db, conversations.Where(c => !c.IsGroup && !c.IsSystemConversation)
            .Select(c => c.GetOtherParticipantId(me)).OfType<string>());

        bool? canAnnounce = null;
        var items = new List<ConversationListItem>();
        foreach (var c in conversations)
        {
            var (kind, title, avatar) = Heading(c, me, people);
            bool canSend;
            if (c.IsSystemConversation)
            {
                canSend = false;
            }
            else if (c.IsAnnouncementOnly)
            {
                canSend = canAnnounce ??= await CanAnnounceAsync(db, user, me);
            }
            else if (!c.IsGroup)
            {
                canSend = c.GetOtherParticipantId(me) is { } other && people.TryGetValue(other, out var p) && !p.Expelled;
            }
            else
            {
                canSend = true;
            }

            last.TryGetValue(c.Id, out var lm);
            settings.TryGetValue(c.Id, out var s);
            var lastAt = lm is not null && lm.CreatedAt > c.LastMessageAt ? lm.CreatedAt : c.LastMessageAt;
            items.Add(new ConversationListItem(c.Id, kind, title, avatar,
                lm is null ? null : Truncate(lm.Body, PreviewLength, ellipsis: true),
                lm is not null && lm.SenderId == me,
                Utc(lastAt),
                unread.GetValueOrDefault(c.Id),
                s?.IsMuted ?? false,
                s?.IsPinned ?? false,
                canSend));
        }

        return items;
    }

    private async Task<ConversationDetail> DetailAsync(ApplicationDbContext db, Conversation conversation, int? before,
        ClaimsPrincipal user, string me)
    {
        var item = (await ListItemsAsync(db, new List<Conversation> { conversation }, user, me))[0];

        var page = await db.Messages.AsNoTracking()
            .Include(m => m.Sender)
            .Include(m => m.Reactions)
            .Where(m => m.ConversationId == conversation.Id && (before == null || m.Id < before))
            .OrderByDescending(m => m.Id)
            .Take(PageSize + 1)
            .ToListAsync();
        var hasMore = page.Count > PageSize;
        var messages = page.Take(PageSize).Reverse().Select(m => Item(m, me)).ToList();

        var others = conversation.GetParticipantIds().Where(id => id != me).ToList();
        var people = conversation.IsSystemConversation ? new Dictionary<string, (MessagingPerson Person, bool Expelled)>() : await PeopleAsync(db, others);
        var participants = others.Where(people.ContainsKey).Select(id => people[id].Person)
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();

        return new ConversationDetail(item, participants, messages, hasMore, ReactionChoices);
    }

    private async Task<MessageItem> MessageAsync(int messageId, string me)
    {
        await using var db = await _contexts.CreateDbContextAsync();
        var message = await db.Messages.AsNoTracking().Include(m => m.Sender).Include(m => m.Reactions).FirstAsync(m => m.Id == messageId);
        return Item(message, me);
    }

    private static MessageItem Item(Message m, string me) => new(
        m.Id,
        m.ConversationId,
        m.Sender is { } s ? Person(s.Id, s.Nickname, s.FirstName, s.LastName, s.ImageUrl) : null,
        m.SenderId == me,
        m.IsSystem,
        m.Body,
        SafeLink(m.Link),
        Utc(m.CreatedAt),
        Reply(m),
        m.Reactions.GroupBy(r => r.Emoji)
            .Select(g => new ReactionSummary(g.Key, g.Count(), g.Any(r => r.UserId == me)))
            .OrderByDescending(r => r.Count).ThenBy(r => r.Emoji, StringComparer.Ordinal)
            .ToList(),
        m.SenderId == me && m.GetReadByIds().Any(id => id != me));

    private static MessageReplyPreview? Reply(Message m) =>
        m.ReplyToMessageId is null && string.IsNullOrEmpty(m.ReplyToBody)
            ? null
            : new MessageReplyPreview(m.ReplyToMessageId, m.ReplyToSenderName, m.ReplyToBody ?? "");

    private static (string Kind, string Title, string Avatar) Heading(Conversation c, string me,
        IReadOnlyDictionary<string, (MessagingPerson Person, bool Expelled)> people)
    {
        // Always the current name: older rows still say "Sistema RTUB" (task 058 renamed it).
        if (c.IsSystemConversation)
        {
            return ("system", SystemTitle, GroupAvatar);
        }

        if (c.IsGroup)
        {
            return ("group", string.IsNullOrWhiteSpace(c.Title) ? "Grupo" : c.Title!, GroupAvatar);
        }

        return c.GetOtherParticipantId(me) is { } other && people.TryGetValue(other, out var p)
            ? ("direct", p.Person.DisplayName, p.Person.AvatarUrl)
            : ("direct", "Membro removido", EventAgendaService.DefaultAvatar);
    }

    private static async Task<Dictionary<string, (MessagingPerson Person, bool Expelled)>> PeopleAsync(ApplicationDbContext db, IEnumerable<string> ids)
    {
        var wanted = ids.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<string, (MessagingPerson, bool)>();
        }

        var users = await db.Users.AsNoTracking().Where(u => wanted.Contains(u.Id))
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl, u.IsExpelled })
            .ToListAsync();
        return users.ToDictionary(u => u.Id, u => (Person(u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl), u.IsExpelled));
    }
}
