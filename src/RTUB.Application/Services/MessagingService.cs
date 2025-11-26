using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Constants;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing conversations and messages
/// </summary>
public class MessagingService : IMessagingService
{
    private const int MessagePreviewMaxLength = DefaultValues.Messaging.MessagePreviewMaxLength;

    /// <summary>
    /// Positions that are allowed to send messages in announcement-only channels
    /// </summary>
    private static readonly Position[] AnnouncementSenderPositions =
    {
        Position.Magister,
        Position.ViceMagister,
        Position.PresidenteConselhoFiscal,
        Position.PresidenteMesaAssembleia,
        Position.PresidenteConselhoVeteranos,
        Position.Ensaiador
    };

    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IConversationUserSettingsRepository _settingsRepository;
    private readonly IRoleAssignmentRepository _roleAssignmentRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly IMessagesHubService? _messagesHubService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<MessagingService> _logger;

    public MessagingService(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IConversationUserSettingsRepository settingsRepository,
        IRoleAssignmentRepository roleAssignmentRepository,
        IPushNotificationService pushNotificationService,
        UserManager<ApplicationUser> userManager,
        ILogger<MessagingService> logger,
        IMessagesHubService? messagesHubService = null)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _settingsRepository = settingsRepository;
        _roleAssignmentRepository = roleAssignmentRepository;
        _pushNotificationService = pushNotificationService;
        _userManager = userManager;
        _logger = logger;
        _messagesHubService = messagesHubService;
    }

    public async Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(string userId)
    {
        var conversations = await _conversationRepository.GetUserConversationsAsync(userId);
        var userSettings = (await _settingsRepository.GetByUserAsync(userId)).ToDictionary(s => s.ConversationId);
        var conversationDtos = new List<ConversationDto>();

        foreach (var conversation in conversations)
        {
            var dto = await MapConversationToDtoAsync(conversation, userId, userSettings);
            conversationDtos.Add(dto);
        }

        // Sort: pinned first (by LastMessageAt), then non-pinned (by LastMessageAt)
        return conversationDtos
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastMessageAt);
    }

    public async Task<ConversationDto?> GetConversationAsync(int conversationId, string currentUserId)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);

        if (conversation == null || !conversation.HasParticipant(currentUserId))
        {
            return null;
        }

        var settings = await _settingsRepository.GetByUserAndConversationAsync(currentUserId, conversationId);
        var settingsDict = settings != null
            ? new Dictionary<int, ConversationUserSettings> { { conversationId, settings } }
            : new Dictionary<int, ConversationUserSettings>();

        return await MapConversationToDtoAsync(conversation, currentUserId, settingsDict);
    }

    public async Task<IEnumerable<MessageDto>> GetConversationMessagesAsync(int conversationId, string currentUserId, int? limit = null)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);

        if (conversation == null || !conversation.HasParticipant(currentUserId))
        {
            return Enumerable.Empty<MessageDto>();
        }

        var messages = await _messageRepository.GetConversationMessagesAsync(conversationId, limit);

        return messages.Select(m => MapMessageToDto(m, currentUserId)).Reverse();
    }

    public async Task<MessageDto> SendDirectMessageAsync(string senderId, SendMessageDto messageDto)
    {
        // Get or create conversation
        var conversation = await _conversationRepository.GetOrCreateOneToOneAsync(senderId, messageDto.ReceiverId);

        // Create message
        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = senderId,
            Body = messageDto.Body,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow
        };

        await _messageRepository.AddAsync(message);

        // Update conversation
        conversation.LastMessageAt = message.CreatedAt;
        conversation.LastMessageId = message.Id;
        await _conversationRepository.UpdateAsync(conversation);

        // Load sender for DTO mapping
        message.Sender = await _userManager.FindByIdAsync(senderId);

        // Create DTO for broadcasting
        var messageDto2 = MapMessageToDto(message, senderId);

        // Broadcast message via SignalR to all participants in the conversation
        if (_messagesHubService != null)
        {
            await _messagesHubService.BroadcastMessageAsync(conversation.Id, messageDto2);
        }

        // Send push notification (without creating inbox message - the direct message itself is already there)
        // Only send if the receiver has not muted this conversation
        var sender = await _userManager.FindByIdAsync(senderId);
        if (sender != null && !string.IsNullOrEmpty(messageDto.Body))
        {
            var isReceiverMuted = await _settingsRepository.IsConversationMutedAsync(messageDto.ReceiverId, conversation.Id);

            if (!isReceiverMuted)
            {
                var senderName = !string.IsNullOrEmpty(sender.Nickname) ? sender.Nickname : $"{sender.FirstName} {sender.LastName}";
                var messagePreview = messageDto.Body.Length > MessagePreviewMaxLength ? $"{messageDto.Body[..MessagePreviewMaxLength]}..." : messageDto.Body;

                await _pushNotificationService.SendPushOnlyAsync(messageDto.ReceiverId, new SendPushNotificationDto
                {
                    Title = $"Nova mensagem de {senderName}",
                    Body = messagePreview,
                    Icon = "/icons/rtub-logo-192.png",
                    Url = "/messages",
                    Tag = $"message-{conversation.Id}"
                });
            }
        }

        return messageDto2;
    }

    public async Task<MessageDto> SendSystemMessageAsync(string receiverId, string body, string? link = null)
    {
        // Create or get system conversation for this user
        var conversation = await _conversationRepository.GetSystemConversationForUserAsync(receiverId);

        if (conversation == null)
        {
            conversation = new Conversation
            {
                Participants = receiverId,
                LastMessageAt = DateTime.UtcNow,
                IsSystemConversation = true,
                Title = "Sistema RTUB",
                CreatedAt = DateTime.UtcNow
            };
            await _conversationRepository.AddAsync(conversation);
        }

        // Create system message
        var message = new Message
        {
            ConversationId = conversation.Id,
            SenderId = null,
            Body = body,
            IsSystem = true,
            Link = link,
            CreatedAt = DateTime.UtcNow
        };

        await _messageRepository.AddAsync(message);

        // Update conversation
        conversation.LastMessageAt = message.CreatedAt;
        conversation.LastMessageId = message.Id;
        await _conversationRepository.UpdateAsync(conversation);

        return MapMessageToDto(message, receiverId);
    }

    public async Task MarkConversationAsReadAsync(int conversationId, string userId)
    {
        await _messageRepository.MarkConversationAsReadAsync(conversationId, userId);

        // Notify other participants that messages were seen
        if (_messagesHubService != null)
        {
            await _messagesHubService.NotifyMessageSeenAsync(conversationId, userId, DateTime.UtcNow);
        }
    }

    public async Task MarkConversationAsUnreadAsync(int conversationId, string userId)
    {
        var latestMessage = await _messageRepository.GetLatestMessageAsync(conversationId);

        if (latestMessage != null && latestMessage.SenderId != userId)
        {
            latestMessage.MarkAsUnreadBy(userId);
            await _messageRepository.UpdateAsync(latestMessage);
        }
    }

    public async Task<bool> DeleteConversationAsync(int conversationId, string userId)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);

        if (conversation == null || !conversation.HasParticipant(userId))
        {
            return false;
        }

        // Check if user can delete this conversation using the shared helper
        if (!CanUserDeleteConversation(conversation, userId))
        {
            return false;
        }

        await _conversationRepository.ArchiveConversationAsync(conversationId);
        return true;
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _messageRepository.GetUnreadCountForUserAsync(userId);
    }

    public async Task<ConversationDto> GetOrCreateConversationAsync(string userId1, string userId2)
    {
        var conversation = await _conversationRepository.GetOrCreateOneToOneAsync(userId1, userId2);
        var settings = await _settingsRepository.GetByUserAndConversationAsync(userId1, conversation.Id);
        var settingsDict = settings != null
            ? new Dictionary<int, ConversationUserSettings> { { conversation.Id, settings } }
            : new Dictionary<int, ConversationUserSettings>();
        return await MapConversationToDtoAsync(conversation, userId1, settingsDict);
    }

    public async Task<ConversationDto> CreateGroupConversationAsync(string creatorUserId, string groupName, List<string> participantIds)
    {
        // Ensure creator is in the participants list
        if (!participantIds.Contains(creatorUserId))
        {
            participantIds.Add(creatorUserId);
        }

        var conversation = new Conversation
        {
            Participants = string.Join(";", participantIds),
            Title = groupName,
            IsGroup = true,
            CreatedByUserId = creatorUserId,
            LastMessageAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _conversationRepository.AddAsync(conversation);

        // Send system message announcing group creation
        var creator = await _userManager.FindByIdAsync(creatorUserId);
        var creatorName = creator?.Nickname ?? $"{creator?.FirstName} {creator?.LastName}";

        var systemMessage = new Message
        {
            ConversationId = conversation.Id,
            SenderId = null,
            Body = $"{creatorName} criou o grupo \"{groupName}\"",
            IsSystem = true,
            CreatedAt = DateTime.UtcNow
        };

        await _messageRepository.AddAsync(systemMessage);

        conversation.LastMessageId = systemMessage.Id;
        await _conversationRepository.UpdateAsync(conversation);

        return await MapConversationToDtoAsync(conversation, creatorUserId, new Dictionary<int, ConversationUserSettings>());
    }

    public async Task<MessageDto> SendGroupMessageAsync(string senderId, int conversationId, string body)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);

        if (conversation == null)
        {
            throw new InvalidOperationException("Conversation not found");
        }

        if (!conversation.IsGroup)
        {
            throw new InvalidOperationException("Conversation is not a group conversation");
        }

        if (!conversation.HasParticipant(senderId))
        {
            throw new InvalidOperationException("User is not a participant in this group");
        }

        // Check if user can send messages in announcement-only channels
        if (conversation.IsAnnouncementOnly)
        {
            var canSend = await CanUserSendMessageInAnnouncementChannelAsync(senderId);
            if (!canSend)
            {
                throw new InvalidOperationException("User does not have permission to send messages in this announcement channel");
            }
        }

        // Create message
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Body = body,
            IsSystem = false,
            CreatedAt = DateTime.UtcNow
        };

        await _messageRepository.AddAsync(message);

        // Update conversation
        conversation.LastMessageAt = message.CreatedAt;
        conversation.LastMessageId = message.Id;
        await _conversationRepository.UpdateAsync(conversation);

        // Load sender for DTO mapping
        message.Sender = await _userManager.FindByIdAsync(senderId);

        // Create DTO for broadcasting
        var messageDto = MapMessageToDto(message, senderId);

        // Broadcast message via SignalR to all participants in the conversation
        if (_messagesHubService != null)
        {
            await _messagesHubService.BroadcastMessageAsync(conversationId, messageDto);
        }

        // Send push notifications to all other participants (unless they have muted)
        var sender = message.Sender;
        if (sender != null && !string.IsNullOrEmpty(body))
        {
            var senderName = !string.IsNullOrEmpty(sender.Nickname) ? sender.Nickname : $"{sender.FirstName} {sender.LastName}";
            var messagePreview = body.Length > MessagePreviewMaxLength ? $"{body[..MessagePreviewMaxLength]}..." : body;
            var groupName = conversation.Title ?? "Grupo";

            var otherParticipants = conversation.GetParticipantIds().Where(id => id != senderId).ToList();

            // Batch fetch muted status for all participants in one query
            var mutedUserIds = await _settingsRepository.GetMutedUserIdsAsync(conversationId, otherParticipants);

            // Send push notifications in parallel to non-muted participants
            var notification = new SendPushNotificationDto
            {
                Title = $"Nova mensagem no grupo {groupName}",
                Body = $"{senderName}: {messagePreview}",
                Icon = "/icons/rtub-logo-192.png",
                Url = "/messages",
                Tag = $"message-{conversationId}"
            };

            var pushTasks = otherParticipants
                .Where(participantId => !mutedUserIds.Contains(participantId))
                .Select(participantId => _pushNotificationService.SendPushOnlyAsync(participantId, notification));

            await Task.WhenAll(pushTasks);
        }

        return messageDto;
    }

    public async Task<ConversationDto> GetOrCreateSystemGroupAsync(string groupTitle, List<string> participantIds)
    {
        var conversation = await _conversationRepository.GetGroupByTitleAsync(groupTitle);

        if (conversation == null)
        {
            conversation = new Conversation
            {
                Participants = string.Join(";", participantIds),
                Title = groupTitle,
                IsGroup = true,
                CreatedByUserId = "system",
                LastMessageAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            await _conversationRepository.AddAsync(conversation);
        }

        return await MapConversationToDtoAsync(conversation, participantIds.FirstOrDefault() ?? string.Empty, new Dictionary<int, ConversationUserSettings>());
    }

    public async Task UpdateGroupParticipantsAsync(int conversationId, List<string> participantIds)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);

        if (conversation == null || !conversation.IsGroup)
        {
            throw new InvalidOperationException("Conversation not found or not a group");
        }

        conversation.SetParticipants(participantIds);
        conversation.UpdatedAt = DateTime.UtcNow;

        await _conversationRepository.UpdateAsync(conversation);
    }

    public async Task<bool> ToggleMuteAsync(int conversationId, string userId)
    {
        var settings = await _settingsRepository.GetOrCreateAsync(userId, conversationId);
        settings.IsMuted = !settings.IsMuted;
        settings.UpdatedAt = DateTime.UtcNow;
        await _settingsRepository.UpdateAsync(settings);
        return settings.IsMuted;
    }

    public async Task<bool> TogglePinAsync(int conversationId, string userId)
    {
        var settings = await _settingsRepository.GetOrCreateAsync(userId, conversationId);
        settings.IsPinned = !settings.IsPinned;
        settings.UpdatedAt = DateTime.UtcNow;
        await _settingsRepository.UpdateAsync(settings);
        return settings.IsPinned;
    }

    public async Task<bool> IsConversationMutedAsync(int conversationId, string userId)
    {
        return await _settingsRepository.IsConversationMutedAsync(userId, conversationId);
    }

    private async Task<ConversationDto> MapConversationToDtoAsync(Conversation conversation, string currentUserId, Dictionary<int, ConversationUserSettings> userSettings)
    {
        // Get settings for this conversation from the provided dictionary
        userSettings.TryGetValue(conversation.Id, out var settings);

        // Determine if user can send messages
        var canSendMessage = true;
        if (conversation.IsSystemConversation)
        {
            canSendMessage = false; // System conversations are read-only
        }
        else if (conversation.IsAnnouncementOnly)
        {
            canSendMessage = await CanUserSendMessageInAnnouncementChannelAsync(currentUserId);
        }

        // Determine if user can delete this conversation
        var canDelete = CanUserDeleteConversation(conversation, currentUserId);

        var dto = new ConversationDto
        {
            Id = conversation.Id,
            ParticipantIds = conversation.GetParticipantIds(),
            LastMessageAt = conversation.LastMessageAt,
            Title = conversation.Title,
            IsSystemConversation = conversation.IsSystemConversation,
            IsGroup = conversation.IsGroup,
            CreatedByUserId = conversation.CreatedByUserId,
            IsAnnouncementOnly = conversation.IsAnnouncementOnly,
            CanSendMessage = canSendMessage,
            CanDelete = canDelete,
            UnreadCount = await _messageRepository.GetUnreadCountForConversationAsync(conversation.Id, currentUserId),
            IsMuted = settings?.IsMuted ?? false,
            IsPinned = settings?.IsPinned ?? false
        };

        // Get last message preview
        if (conversation.LastMessageId.HasValue)
        {
            var lastMessage = conversation.Messages.FirstOrDefault(m => m.Id == conversation.LastMessageId.Value);
            if (lastMessage == null)
            {
                lastMessage = await _messageRepository.GetLatestMessageAsync(conversation.Id);
            }

            if (lastMessage != null)
            {
                dto.LastMessagePreview = lastMessage.Body.Length > MessagePreviewMaxLength
                    ? $"{lastMessage.Body[..MessagePreviewMaxLength]}..."
                    : lastMessage.Body;
                dto.LastMessageSenderId = lastMessage.SenderId;
            }
        }

        // For group conversations, load participant info
        if (conversation.IsGroup)
        {
            var participantIds = conversation.GetParticipantIds();
            foreach (var participantId in participantIds)
            {
                var user = await _userManager.FindByIdAsync(participantId);
                if (user != null)
                {
                    dto.GroupParticipants.Add(new GroupParticipantDto
                    {
                        UserId = participantId,
                        Name = $"{user.FirstName} {user.LastName}",
                        Nickname = user.Nickname,
                        Avatar = user.ImageUrl
                    });
                }
            }
        }
        // For one-to-one conversations, get other participant info
        else if (!conversation.IsSystemConversation)
        {
            var otherParticipantId = conversation.GetOtherParticipantId(currentUserId);
            if (otherParticipantId != null)
            {
                var otherUser = await _userManager.FindByIdAsync(otherParticipantId);
                if (otherUser != null)
                {
                    dto.OtherParticipantId = otherParticipantId;
                    dto.OtherParticipantName = $"{otherUser.FirstName} {otherUser.LastName}";
                    dto.OtherParticipantNickname = otherUser.Nickname;
                    dto.OtherParticipantAvatar = otherUser.ImageUrl;
                }
            }
        }

        return dto;
    }

    private MessageDto MapMessageToDto(Message message, string currentUserId)
    {
        var dto = new MessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            Body = message.Body,
            IsSystem = message.IsSystem,
            CreatedAt = message.CreatedAt,
            IsRead = message.IsReadBy(currentUserId),
            Link = message.Link,
            ReadBy = message.ReadBy
        };

        if (message.Sender != null)
        {
            dto.SenderName = $"{message.Sender.FirstName} {message.Sender.LastName}";
            dto.SenderNickname = message.Sender.Nickname;
            dto.SenderAvatar = message.Sender.ImageUrl;
        }

        return dto;
    }

    public async Task<bool> CanUserSendMessageAsync(int conversationId, string userId)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);

        if (conversation == null)
        {
            return false;
        }

        // System conversations are read-only
        if (conversation.IsSystemConversation)
        {
            return false;
        }

        // User must be a participant
        if (!conversation.HasParticipant(userId))
        {
            return false;
        }

        // Announcement-only channels require specific positions
        if (conversation.IsAnnouncementOnly)
        {
            return await CanUserSendMessageInAnnouncementChannelAsync(userId);
        }

        return true;
    }

    public async Task<ConversationDto> GetOrCreateAnnouncementGroupAsync(string groupTitle, List<string> participantIds)
    {
        var conversation = await _conversationRepository.GetGroupByTitleAsync(groupTitle);

        if (conversation == null)
        {
            conversation = new Conversation
            {
                Participants = string.Join(";", participantIds),
                Title = groupTitle,
                IsGroup = true,
                IsAnnouncementOnly = true,
                CreatedByUserId = "system",
                LastMessageAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            await _conversationRepository.AddAsync(conversation);
        }

        return await MapConversationToDtoAsync(conversation, participantIds.FirstOrDefault() ?? string.Empty, new Dictionary<int, ConversationUserSettings>());
    }

    /// <summary>
    /// Checks if a user has one of the positions required to send messages in announcement channels
    /// </summary>
    private async Task<bool> CanUserSendMessageInAnnouncementChannelAsync(string userId)
    {
        // Get current fiscal year bounds
        var now = DateTime.UtcNow;
        var startYear = now.Month >= 9 ? now.Year : now.Year - 1;
        var endYear = startYear + 1;

        // Get user's current role assignments
        var roleAssignments = await _roleAssignmentRepository.GetByUserIdAsync(userId);

        // Check if user has any of the allowed positions in the current fiscal year
        return roleAssignments.Any(ra =>
            ra.StartYear == startYear &&
            ra.EndYear == endYear &&
            AnnouncementSenderPositions.Contains(ra.Position));
    }

    /// <summary>
    /// Checks if a user can delete a conversation based on the deletion rules
    /// </summary>
    private static bool CanUserDeleteConversation(Conversation conversation, string userId)
    {
        // System conversations cannot be deleted by anyone
        if (conversation.IsSystemConversation)
        {
            return false;
        }

        // System groups (CreatedByUserId = "system") cannot be deleted by anyone
        if (conversation.IsGroup && conversation.CreatedByUserId == "system")
        {
            return false;
        }

        // Group conversations can only be deleted by their creator
        if (conversation.IsGroup && conversation.CreatedByUserId != userId)
        {
            return false;
        }

        // Regular 1:1 conversations can be deleted by any participant
        return true;
    }
}
