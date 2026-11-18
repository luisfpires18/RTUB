// Messaging: conversation list, unread count, open, find or start a conversation.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

public sealed partial class MessagingService
{
    // ---------- reads ----------

    public async Task<EventResult<IReadOnlyList<ConversationListItem>>> GetConversationsAsync(ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
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
        if (user.UserId() is not { } me)
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
        if (user.UserId() is not { } me)
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
        if (user.UserId() is not { } me)
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

    public async Task<EventResult<DirectConversation>> FindDirectAsync(string userId, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<DirectConversation>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        var conversation = await DirectWithAsync(db, me, userId.Trim());
        // An archived chat stays hidden until a message brings it back (StartConversationAsync).
        return EventResult<DirectConversation>.Ok(new DirectConversation(conversation is { IsArchived: false } ? conversation.Id : null));
    }

    /// <summary>
    /// Nothing is stored until the first message: the conversation and that message are written together. One other
    /// member = the one-to-one chat (an existing one, even archived, is reused); two or more = a new group with a title.
    /// </summary>
    public async Task<EventResult<ConversationDetail>> StartConversationAsync(StartConversationRequest input, ClaimsPrincipal user)
    {
        if (user.UserId() is not { } me)
        {
            return EventResult<ConversationDetail>.Fail(EventResultStatus.SignInRequired);
        }

        var others = (input.UserIds ?? []).Select(id => id?.Trim()).OfType<string>()
            .Where(id => id.Length > 0 && id != me).Distinct(StringComparer.Ordinal).ToList();
        if (others.Count == 0 || others.Count > MaxGroupMembers)
        {
            return EventResult<ConversationDetail>.Invalid("userIds", "Escolhe pelo menos um membro.");
        }

        var title = input.Title?.Trim() ?? "";
        if (others.Count > 1 && title.Length == 0)
        {
            return EventResult<ConversationDetail>.Invalid("title", "Dá um nome ao grupo.");
        }

        if (title.Length > MaxTitleLength)
        {
            return EventResult<ConversationDetail>.Invalid("title", $"O nome tem no máximo {MaxTitleLength} caracteres.");
        }

        if (BodyProblem(input.Body) is { } bodyProblem)
        {
            return EventResult<ConversationDetail>.Invalid("body", bodyProblem);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await db.Users.CountAsync(u => others.Contains(u.Id) && !u.IsExpelled) != others.Count)
        {
            return EventResult<ConversationDetail>.Invalid("userIds", "Escolhe pelo menos um membro.");
        }

        Conversation? conversation = null;
        if (others.Count == 1)
        {
            conversation = await DirectWithAsync(db, me, others[0]);
            if (conversation is { IsArchived: true })
            {
                // v1 "deleted" a chat by archiving it for both; writing to the person again brings it back.
                conversation.IsArchived = false;
                await db.SaveChangesAsync();
            }
        }

        if (conversation is null)
        {
            var participants = string.Join(';', others.Append(me).OrderBy(x => x, StringComparer.Ordinal));
            conversation = others.Count == 1
                ? new Conversation { Participants = participants, LastMessageAt = DateTime.UtcNow, IsGroup = false }
                : new Conversation { Participants = participants, LastMessageAt = DateTime.UtcNow, IsGroup = true, Title = title, CreatedByUserId = me };
            db.Conversations.Add(conversation);
            await db.SaveChangesAsync();
        }

        var sent = await SendAsync(conversation.Id, new SendMessageRequest(input.Body, null), user);
        if (sent.Status != EventResultStatus.Ok)
        {
            return EventResult<ConversationDetail>.Fail(sent.Status);
        }

        await db.Entry(conversation).ReloadAsync();
        return EventResult<ConversationDetail>.Ok(await DetailAsync(db, conversation, null, user, me));
    }

    // v1 stored the pair sorted; both orders are looked up so either sort order finds the existing chat. Archived included.
    private static Task<Conversation?> DirectWithAsync(ApplicationDbContext db, string me, string other)
    {
        var forward = $"{me};{other}";
        var backward = $"{other};{me}";
        return db.Conversations
            .FirstOrDefaultAsync(c => !c.IsGroup && !c.IsSystemConversation && (c.Participants == forward || c.Participants == backward));
    }
}
