namespace RTUB.Application.DTOs;

// Messaging v2 (task 041, docs/react-messages.md): the React /messages over the preserved v1 tables (Conversations,
// Messages, MessageReactions, ConversationUserSettings). Text only - no attachments, no media. Dates are UTC.

/// <summary>A person as the chat shows them: nickname first (else the full name), never an address or phone.</summary>
public sealed record MessagingPerson(string Id, string DisplayName, string? FullName, string AvatarUrl);

/// <summary>One row of the conversation list. <c>LastMessagePreview</c> is only ever sent to a participant.</summary>
public sealed record ConversationListItem(
    int Id,
    string Kind,
    string Title,
    string AvatarUrl,
    string? LastMessagePreview,
    bool LastMessageIsMine,
    DateTime LastMessageAt,
    int UnreadCount,
    bool Muted,
    bool Pinned,
    bool CanSend);

/// <summary>An emoji and who used it, from the caller's point of view.</summary>
public sealed record ReactionSummary(string Emoji, int Count, bool Mine);

/// <summary>The message a reply points at, as stored with the reply (snippet ≤200, sender name ≤100).</summary>
public sealed record MessageReplyPreview(int? MessageId, string? SenderName, string Body);

public sealed record MessageItem(
    int Id,
    int ConversationId,
    MessagingPerson? Sender,
    bool IsMine,
    bool IsSystem,
    string Body,
    string? Link,
    DateTime CreatedAt,
    MessageReplyPreview? ReplyTo,
    IReadOnlyList<ReactionSummary> Reactions,
    bool Seen);

/// <summary>
/// One conversation with a page of messages, oldest first. <c>HasMore</c>: older messages exist (ask again with
/// <c>before</c> = the first message's id). <c>Participants</c> is the other members (all of them in a group).
/// </summary>
public sealed record ConversationDetail(
    ConversationListItem Conversation,
    IReadOnlyList<MessagingPerson> Participants,
    IReadOnlyList<MessageItem> Messages,
    bool HasMore,
    IReadOnlyList<string> ReactionChoices);

/// <summary>Text only (1-2000 after trimming); a reply names a message of the same conversation.</summary>
public sealed record SendMessageRequest(string? Body, int? ReplyToMessageId);

/// <summary>One of <see cref="ConversationDetail.ReactionChoices"/>.</summary>
public sealed record ReactionRequest(string? Emoji);

/// <summary>Opens (or reopens) the one-to-one conversation with another member.</summary>
public sealed record StartConversationRequest(string? UserId);

public sealed record ConversationSettings(bool Muted);
