using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing conversations and messages
/// </summary>
public class MessagingService : IMessagingService
{
    /// <summary>
    /// Maximum length of message preview text before truncation
    /// </summary>
    private const int MessagePreviewMaxLength = 100;
    
    private readonly IConversationRepository _conversationRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<MessagingService> _logger;

    public MessagingService(
        IConversationRepository conversationRepository,
        IMessageRepository messageRepository,
        IPushNotificationService pushNotificationService,
        UserManager<ApplicationUser> userManager,
        ILogger<MessagingService> logger)
    {
        _conversationRepository = conversationRepository;
        _messageRepository = messageRepository;
        _pushNotificationService = pushNotificationService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IEnumerable<ConversationDto>> GetUserConversationsAsync(string userId)
    {
        var conversations = await _conversationRepository.GetUserConversationsAsync(userId);
        var conversationDtos = new List<ConversationDto>();

        foreach (var conversation in conversations)
        {
            var dto = await MapConversationToDtoAsync(conversation, userId);
            conversationDtos.Add(dto);
        }

        return conversationDtos.OrderByDescending(c => c.LastMessageAt);
    }

    public async Task<ConversationDto?> GetConversationAsync(int conversationId, string currentUserId)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        
        if (conversation == null || !conversation.HasParticipant(currentUserId))
        {
            return null;
        }

        return await MapConversationToDtoAsync(conversation, currentUserId);
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

        // Send push notification (without creating inbox message - the direct message itself is already there)
        var sender = await _userManager.FindByIdAsync(senderId);
        if (sender != null && !string.IsNullOrEmpty(messageDto.Body))
        {
            var senderName = !string.IsNullOrEmpty(sender.Nickname) ? sender.Nickname : $"{sender.FirstName} {sender.LastName}";
            var messagePreview = messageDto.Body.Length > MessagePreviewMaxLength ? messageDto.Body.Substring(0, MessagePreviewMaxLength) + "..." : messageDto.Body;

            await _pushNotificationService.SendPushOnlyAsync(messageDto.ReceiverId, new SendPushNotificationDto
            {
                Title = $"Nova mensagem de {senderName}",
                Body = messagePreview,
                Icon = "/icons/rtub-logo-192.png",
                Url = "/messages",
                Tag = $"message-{conversation.Id}"
            });
        }

        return MapMessageToDto(message, senderId);
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

    public async Task DeleteConversationAsync(int conversationId, string userId)
    {
        var conversation = await _conversationRepository.GetByIdAsync(conversationId);
        
        if (conversation != null && conversation.HasParticipant(userId))
        {
            await _conversationRepository.ArchiveConversationAsync(conversationId);
        }
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _messageRepository.GetUnreadCountForUserAsync(userId);
    }

    public async Task<ConversationDto> GetOrCreateConversationAsync(string userId1, string userId2)
    {
        var conversation = await _conversationRepository.GetOrCreateOneToOneAsync(userId1, userId2);
        return await MapConversationToDtoAsync(conversation, userId1);
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
        
        return await MapConversationToDtoAsync(conversation, creatorUserId);
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
        
        // Send push notifications to all other participants
        var sender = message.Sender;
        if (sender != null && !string.IsNullOrEmpty(body))
        {
            var senderName = !string.IsNullOrEmpty(sender.Nickname) ? sender.Nickname : $"{sender.FirstName} {sender.LastName}";
            var messagePreview = body.Length > MessagePreviewMaxLength ? body.Substring(0, MessagePreviewMaxLength) + "..." : body;
            var groupName = conversation.Title ?? "Grupo";
            
            var otherParticipants = conversation.GetParticipantIds().Where(id => id != senderId);
            
            foreach (var participantId in otherParticipants)
            {
                // Use SendPushOnlyAsync to avoid creating system messages - the group message itself is already in the conversation
                await _pushNotificationService.SendPushOnlyAsync(participantId, new SendPushNotificationDto
                {
                    Title = $"Nova mensagem no grupo {groupName}",
                    Body = $"{senderName}: {messagePreview}",
                    Icon = "/icons/rtub-logo-192.png",
                    Url = "/messages",
                    Tag = $"message-{conversationId}"
                });
            }
        }
        
        return MapMessageToDto(message, senderId);
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
            
            _logger.LogInformation("Created system group: {GroupTitle} with {ParticipantCount} participants", 
                groupTitle, participantIds.Count);
        }
        
        return await MapConversationToDtoAsync(conversation, participantIds.FirstOrDefault() ?? string.Empty);
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
        
        _logger.LogInformation("Updated participants for group {GroupTitle} (ID: {ConversationId})", 
            conversation.Title, conversationId);
    }

    private async Task<ConversationDto> MapConversationToDtoAsync(Conversation conversation, string currentUserId)
    {
        var dto = new ConversationDto
        {
            Id = conversation.Id,
            ParticipantIds = conversation.GetParticipantIds(),
            LastMessageAt = conversation.LastMessageAt,
            Title = conversation.Title,
            IsSystemConversation = conversation.IsSystemConversation,
            IsGroup = conversation.IsGroup,
            CreatedByUserId = conversation.CreatedByUserId,
            UnreadCount = await _messageRepository.GetUnreadCountForConversationAsync(conversation.Id, currentUserId)
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
                    ? lastMessage.Body.Substring(0, MessagePreviewMaxLength) + "..." 
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
            Link = message.Link
        };

        if (message.Sender != null)
        {
            dto.SenderName = $"{message.Sender.FirstName} {message.Sender.LastName}";
            dto.SenderNickname = message.Sender.Nickname;
            dto.SenderAvatar = message.Sender.ImageUrl;
        }

        return dto;
    }
}
