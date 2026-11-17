// Messaging: send, mark read, remove, mute, react.
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
    private static string? BodyProblem(string? body) => (body?.Trim() ?? "").Length switch
    {
        0 => "Escreve uma mensagem.",
        > MaxBodyLength => $"A mensagem tem no máximo {MaxBodyLength} caracteres.",
        _ => null,
    };

    public async Task<EventResult<MessageItem>> SendAsync(int conversationId, SendMessageRequest input, ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<MessageItem>.Fail(EventResultStatus.SignInRequired);
        }

        if (BodyProblem(input.Body) is { } bodyProblem)
        {
            return EventResult<MessageItem>.Invalid("body", bodyProblem);
        }

        var body = input.Body!.Trim();

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

    /// <summary>
    /// "Remover conversa": one-to-one chats only (groups and Notificações RTUB stay). As v1, the chat is archived for both
    /// members, its messages kept; a new message to the person brings it back (<see cref="StartConversationAsync"/>).
    /// </summary>
    public async Task<EventResult<bool>> RemoveDirectAsync(int conversationId, ClaimsPrincipal user)
    {
        if (UserId(user) is not { } me)
        {
            return EventResult<bool>.Fail(EventResultStatus.SignInRequired);
        }

        await using var db = await _contexts.CreateDbContextAsync();
        if (await FindMineAsync(db, conversationId, me, tracked: true) is not { } conversation)
        {
            return EventResult<bool>.Fail(EventResultStatus.NotFound);
        }

        if (conversation.IsGroup || conversation.IsSystemConversation)
        {
            return EventResult<bool>.Fail(EventResultStatus.Forbidden);
        }

        conversation.IsArchived = true;
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
}
