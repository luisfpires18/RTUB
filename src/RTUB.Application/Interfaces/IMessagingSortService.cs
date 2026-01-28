using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for sorting conversations
/// Extracted from Inbox.razor to improve separation of concerns
/// </summary>
public interface IMessagingSortService
{
    /// <summary>
    /// Sorts conversations by pinned status first, then by last message date (descending)
    /// </summary>
    /// <param name="conversations">Collection of conversations to sort</param>
    /// <returns>Sorted list of conversations</returns>
    List<ConversationDto> SortConversations(IEnumerable<ConversationDto> conversations);
}
