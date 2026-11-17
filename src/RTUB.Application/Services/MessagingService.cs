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
/// - "Notificações RTUB" (was "Sistema RTUB"; one per member, every push is kept there since task 058,
///   <see cref="NotificationInbox"/>) conversations are read-only; announcement-only groups take messages from the Owner and the current
///   Magister, Vice-Magister, Presidents of the CF / MAG / CV and the Ensaiador only (v1's list);
/// - text only (1-2000), a reply quotes a message of the same conversation (snippet ≤200 + sender, denormalized as v1
///   did), one reaction per member per message from a fixed emoji set, mute per member (no push while muted);
/// - unread = not sent by me and my id not in <see cref="Message.ReadBy"/>.
/// Message text never reaches a log, the audit log (Message, Conversation, MessageReaction and ConversationUserSettings
/// are excluded and redacted, AuditConfiguration / AuditLogAppender) or a push notification.
/// </summary>
public sealed partial class MessagingService : IMessagingService
{
    public const int MaxBodyLength = 2000;
    public const int ReplySnippetLength = 200;
    public const int ReplySenderNameLength = 100;
    public const int PreviewLength = 80;
    public const int PageSize = 50;
    public const int MaxTitleLength = 200; // the Conversations.Title column
    public const int MaxGroupMembers = 100;
    public const string SystemTitle = "Notificações RTUB";
    // The 352 px WebP (~34 KB) the portal header uses, shown at avatar size; was the 1024 px logo (~146 KB). #226 F09.
    public const string GroupAvatar = "/icons/rtub-logo-352.webp";

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
}
