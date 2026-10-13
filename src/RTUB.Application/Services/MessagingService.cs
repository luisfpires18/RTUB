using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Messaging v2 (task 041, docs/react-messages.md): the React /messages over the v1 tables that task 027 preserved, with
/// the v1 rules (git: a121f3f1^ MessagingService) and no schema change:
/// - a member sees only the conversations they take part in (<see cref="Conversation.HasParticipant"/>); archived ones
///   stay hidden; there is no Admin / Owner reading of other people's chats;
/// - "Sistema RTUB" conversations are read-only; announcement-only groups take messages from the Owner and the current
///   Magister, Vice-Magister, Presidents of the CF / MAG / CV and the Ensaiador only (v1's list);
/// - text only (1-2000), a reply quotes a message of the same conversation (snippet ≤200 + sender, denormalized as v1
///   did), one reaction per member per message from a fixed emoji set, mute per member (no push while muted);
/// - unread = not sent by me and my id not in <see cref="Message.ReadBy"/>.
/// Message text never reaches a log, the audit log (Message, Conversation, MessageReaction and ConversationUserSettings
/// are excluded and redacted, AuditConfiguration / AuditLogAppender) or a push notification.
/// </summary>
public sealed class MessagingService : IMessagingService
{
    public const int MaxBodyLength = 2000;
    public const int ReplySnippetLength = 200;
    public const int ReplySenderNameLength = 100;
    public const int PreviewLength = 80;
    public const int PageSize = 50;
    public const string SystemTitle = "Sistema RTUB";
    public const string GroupAvatar = "/images/rtub_logo.webp";

    /// <summary>The emoji a member can react with. Older reactions outside the set still show.</summary>
    public static readonly IReadOnlyList<string> ReactionChoices = ["👍", "❤️", "😂", "😮", "😢", "🙏"];

    /// <summary>v1's announcement senders (besides the Owner), in the current fiscal year.</summary>
    private static readonly Position[] AnnouncementSenderPositions =
    [
        Position.Magister, Position.ViceMagister, Position.PresidenteConselhoFiscal,
        Position.PresidenteMesaAssembleia, Position.PresidenteConselhoVeteranos, Position.Ensaiador,
    ];

    private readonly IDbContextFactory<ApplicationDbContext> _contexts;
    private readonly IPushNotificationService _push;
    private readonly IPushNotificationFactory _notifications;
    private readonly ILogger<MessagingService> _logger;

    public MessagingService(IDbContextFactory<ApplicationDbContext> contexts, IPushNotificationService push,
        IPushNotificationFactory notifications, ILogger<MessagingService> logger)
    {
        _contexts = contexts;
        _push = push;
        _notifications = notifications;
        _logger = logger;
    }

    // ---------- reads ----------

    public async Task<EventResult<IReadOnlyList<ConversationListItem>>> GetConversationsAsync(ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<IReadOnlyList<ConversationListItem>>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var conversations = await MyConversationsAsync(db, me);

        var items = await ListItemsAsync(db, conversations, user, me);
        return EventResult<IReadOnlyList<ConversationListItem>>.Ok(items
            .OrderByDescending(i => i.Pinned)
            .ThenByDescending(i => i.LastMessageAt)
            .ToList());
    }

    /// <summary>Same rows and same rule as the list's per-conversation <c>UnreadCount</c>, summed, without building the list.</summary>
    public async Task<EventResult<UnreadSummary>> GetUnreadAsync(ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<UnreadSummary>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var ids = (await MyConversationsAsync(db, me)).Select(c => c.Id).ToList();
        var unread = await db.Messages.AsNoTracking()
            .CountAsync(m => ids.Contains(m.ConversationId) && m.SenderId != me && !m.ReadBy.Contains(me));
        return EventResult<UnreadSummary>.Ok(new UnreadSummary(unread));
    }

    // The SQL filter is a substring match on the id list; HasParticipant makes it exact. Archived ones stay hidden.
    private static async Task<List<Conversation>> MyConversationsAsync(ApplicationDbContext db, string me) =>
        (await db.Conversations.AsNoTracking()
            .Where(c => !c.IsArchived && c.Participants.Contains(me))
            .ToListAsync())
        .Where(c => c.HasParticipant(me))
        .ToList();

    public async Task<EventResult<IReadOnlyList<MessagingPerson>>> GetRecipientsAsync(ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<IReadOnlyList<MessagingPerson>>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var people = await db.Users.AsNoTracking()
            .Where(u => u.Id != me && !u.IsExpelled)
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl })
            .ToListAsync();

        return EventResult<IReadOnlyList<MessagingPerson>>.Ok(people
            .Select(u => Person(u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl))
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList());
    }

    public async Task<EventResult<ConversationDetail>> GetConversationAsync(int id, int? beforeMessageId, ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<ConversationDetail>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await FindMineAsync(db, id, me, tracked: false) is not { } conversation)
        {
            return EventResult<ConversationDetail>.Fail(EventResultStatus.NotFound);
        }

        return EventResult<ConversationDetail>.Ok(await DetailAsync(db, conversation, beforeMessageId, user, me));
    }

    // ---------- writes ----------

    public async Task<EventResult<ConversationDetail>> StartConversationAsync(StartConversationRequest input, ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<ConversationDetail>.Fail(EventResultStatus.SignInRequired);
        }

        var other = input.UserId?.Trim();
        await using var db = await _contexts.CreateDbContextAsync();
        if (string.IsNullOrEmpty(other) || other == me || !await db.Users.AnyAsync(u => u.Id == other && !u.IsExpelled))
        {
            return EventResult<ConversationDetail>.Invalid("userId", "Escolhe um membro.");
        }

        // v1 stored the pair sorted; both orders are looked up so either sort order finds the existing chat.
        var pair = new[] { me, other }.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var forward = $"{pair[0]};{pair[1]}";
        var backward = $"{pair[1]};{pair[0]}";
        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => !c.IsGroup && !c.IsSystemConversation && (c.Participants == forward || c.Participants == backward));

        if (conversation is null)
        {
            conversation = new Conversation { Participants = forward, LastMessageAt = DateTime.UtcNow, IsGroup = false };
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }
        else if (conversation.IsArchived)
        {
            // v1 "deleted" a chat by archiving it for both; writing to the person again brings it back.
            conversation.IsArchived = false;
            await db.SaveChangesAsync();
        }

        return EventResult<ConversationDetail>.Ok(await DetailAsync(db, conversation, null, user, me));
    }

    public async Task<EventResult<MessageItem>> SendAsync(int conversationId, SendMessageRequest input, ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<MessageItem>.Fail(EventResultStatus.SignInRequired);
        }

        var body = input.Body?.Trim() ?? "";
        if (body.Length == 0)
        {
            return EventResult<MessageItem>.Invalid("body", "Escreve uma mensagem.");
        }

        if (body.Length > MaxBodyLength)
        {
            return EventResult<MessageItem>.Invalid("body", $"A mensagem tem no máximo {MaxBodyLength} caracteres.");
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await FindMineAsync(db, conversationId, me, tracked: true) is not { } conversation)
        {
            return EventResult<MessageItem>.Fail(EventResultStatus.NotFound);
        }

        if (!await CanSendAsync(db, conversation, user, me))
        {
            return EventResult<MessageItem>.Fail(EventResultStatus.Forbidden);
        }

        var message = new Message { ConversationId = conversation.Id, SenderId = me, Body = body, IsSystem = false };
        if (input.ReplyToMessageId is { } replyTo)
        {
            var original = await db.Messages.AsNoTracking().Include(m => m.Sender)
                .FirstOrDefaultAsync(m => m.Id == replyTo && m.ConversationId == conversation.Id);
            if (original is null)
            {
                return EventResult<MessageItem>.Invalid("replyToMessageId", "A mensagem a que respondes já não existe.");
            }

            message.ReplyToMessageId = original.Id;
            message.ReplyToBody = Truncate(original.Body, ReplySnippetLength);
            message.ReplyToSenderName = original.Sender is { } s
                ? Truncate(Person(s.Id, s.Nickname, s.FirstName, s.LastName, null).DisplayName, ReplySenderNameLength)
                : original.IsSystem ? SystemTitle : null;
        }

        db.Messages.Add(message);
        await db.SaveChangesAsync();
        conversation.LastMessageAt = message.CreatedAt;
        conversation.LastMessageId = message.Id;
        await db.SaveChangesAsync();

        var sender = await db.Users.AsNoTracking().Where(u => u.Id == me)
            .Select(u => new { u.Id, u.Nickname, u.FirstName, u.LastName, u.ImageUrl }).FirstAsync();
        var senderPerson = Person(sender.Id, sender.Nickname, sender.FirstName, sender.LastName, sender.ImageUrl);
        await NotifyAsync(db, conversation, me, senderPerson.DisplayName);

        return EventResult<MessageItem>.Ok(new MessageItem(message.Id, conversation.Id, senderPerson, true, false, message.Body, null,
            Utc(message.CreatedAt), Reply(message), Array.Empty<ReactionSummary>(), false));
    }

    public async Task<EventResult<bool>> MarkReadAsync(int conversationId, ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await FindMineAsync(db, conversationId, me, tracked: false) is null)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        var unread = (await db.Messages
                .Where(m => m.ConversationId == conversationId && m.SenderId != me && !m.ReadBy.Contains(me))
                .ToListAsync())
            .Where(m => !m.IsReadBy(me))
            .ToList();
        foreach (var message in unread)
        {
            message.MarkAsReadBy(me);
        }

        await db.SaveChangesAsync();
        return EventResult<bool>.Ok(true);
    }

    public async Task<EventResult<ConversationSettings>> SetMutedAsync(int conversationId, bool muted, ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<ConversationSettings>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await FindMineAsync(db, conversationId, me, tracked: false) is null)
        {
            return EventResult<ConversationSettings>.Fail(EventResultStatus.NotFound);
        }

        var settings = await db.ConversationUserSettings.FirstOrDefaultAsync(s => s.ConversationId == conversationId && s.UserId == me);
        if (settings is null)
        {
            db.ConversationUserSettings.Add(new ConversationUserSettings { ConversationId = conversationId, UserId = me, IsMuted = muted });
        }
        else
        {
            settings.IsMuted = muted;
        }

        await db.SaveChangesAsync();
        return EventResult<ConversationSettings>.Ok(new ConversationSettings(muted));
    }

    public async Task<EventResult<MessageItem>> ReactAsync(int messageId, ReactionRequest input, ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<MessageItem>.Fail(EventResultStatus.SignInRequired);
        }

        var emoji = input.Emoji?.Trim() ?? "";
        if (!ReactionChoices.Contains(emoji))
        {
            return EventResult<MessageItem>.Invalid("emoji", "Escolhe uma das reações.");
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await ReactableAsync(db, messageId, me) is not { } status)
        {
            return EventResult<MessageItem>.Fail(EventResultStatus.NotFound);
        }

        if (status == EventResultStatus.Forbidden)
        {
            return EventResult<MessageItem>.Fail(EventResultStatus.Forbidden);
        }

        var existing = await db.MessageReactions.FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == me);
        if (existing is null)
        {
            db.MessageReactions.Add(new MessageReaction { MessageId = messageId, UserId = me, Emoji = emoji });
        }
        else
        {
            existing.Emoji = emoji;
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Two taps at once: the unique (MessageId, UserId) index kept one reaction; show what is stored.
        }

        return EventResult<MessageItem>.Ok(await MessageAsync(messageId, me));
    }

    public async Task<EventResult<MessageItem>> RemoveReactionAsync(int messageId, ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<MessageItem>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await ReactableAsync(db, messageId, me) is not { } status)
        {
            return EventResult<MessageItem>.Fail(EventResultStatus.NotFound);
        }

        if (status == EventResultStatus.Forbidden)
        {
            return EventResult<MessageItem>.Fail(EventResultStatus.Forbidden);
        }

        await db.MessageReactions.Where(r => r.MessageId == messageId && r.UserId == me).ExecuteDeleteAsync();
        return EventResult<MessageItem>.Ok(await MessageAsync(messageId, me));
    }

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
        if (c.IsSystemConversation)
        {
            return ("system", string.IsNullOrWhiteSpace(c.Title) ? SystemTitle : c.Title!, GroupAvatar);
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
