using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for messaging display operations (message group position, preview truncation)
/// Extracted from Inbox.razor to improve separation of concerns
/// </summary>
public class MessagingDisplayService : IMessagingDisplayService
{
    /// <summary>
    /// Determines the position of a message within a group of consecutive messages from the same sender
    /// </summary>
    /// <param name="messages">List of messages</param>
    /// <param name="index">Index of the current message</param>
    /// <returns>The message group position</returns>
    public MessageGroupPosition GetMessageGroupPosition(List<MessageDto> messages, int index)
    {
        if (index < 0 || index >= messages.Count)
        {
            return MessageGroupPosition.Single;
        }

        var currentMessage = messages[index];
        var previousMessage = index > 0 ? messages[index - 1] : null;
        var nextMessage = index < messages.Count - 1 ? messages[index + 1] : null;

        var hasSamePreviousSender = previousMessage != null && previousMessage.SenderId == currentMessage.SenderId;
        var hasSameNextSender = nextMessage != null && nextMessage.SenderId == currentMessage.SenderId;

        if (!hasSamePreviousSender && !hasSameNextSender)
        {
            return MessageGroupPosition.Single;
        }

        if (!hasSamePreviousSender && hasSameNextSender)
        {
            return MessageGroupPosition.First;
        }

        if (hasSamePreviousSender && hasSameNextSender)
        {
            return MessageGroupPosition.Middle;
        }

        // hasSamePreviousSender && !hasSameNextSender
        return MessageGroupPosition.Last;
    }

    /// <summary>
    /// Truncates a message body for preview display
    /// </summary>
    /// <param name="body">The message body</param>
    /// <param name="maxLength">Maximum length for preview (default: 50)</param>
    /// <returns>Truncated message body with ellipsis if needed</returns>
    public string TruncateMessagePreview(string body, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(body)) return string.Empty;

        return body.Length > maxLength
            ? body[..maxLength] + "..."
            : body;
    }
}
