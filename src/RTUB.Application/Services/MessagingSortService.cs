using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// Service for sorting conversations
/// Extracted from Inbox.razor to improve separation of concerns
/// </summary>
public class MessagingSortService : IMessagingSortService
{
    /// <summary>
    /// Sorts conversations by pinned status first, then by last message date (descending)
    /// </summary>
    /// <param name="conversations">Collection of conversations to sort</param>
    /// <returns>Sorted list of conversations</returns>
    public List<ConversationDto> SortConversations(IEnumerable<ConversationDto> conversations)
    {
        return conversations
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastMessageAt)
            .ToList();
    }
}
