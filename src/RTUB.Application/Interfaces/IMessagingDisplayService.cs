using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for messaging display operations (message group position, preview truncation)
/// Extracted from Inbox.razor to improve separation of concerns
/// </summary>
public interface IMessagingDisplayService
{
    /// <summary>
    /// Determines the position of a message within a group of consecutive messages from the same sender
    /// </summary>
    /// <param name="messages">List of messages</param>
    /// <param name="index">Index of the current message</param>
    /// <returns>The message group position</returns>
    MessageGroupPosition GetMessageGroupPosition(List<MessageDto> messages, int index);

    /// <summary>
    /// Truncates a message body for preview display
    /// </summary>
    /// <param name="body">The message body</param>
    /// <param name="maxLength">Maximum length for preview (default: 50)</param>
    /// <returns>Truncated message body with ellipsis if needed</returns>
    string TruncateMessagePreview(string body, int maxLength = 50);
}
