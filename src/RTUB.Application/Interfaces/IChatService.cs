using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for Chat operations
/// Manages chat messages for event enrollment pages
/// </summary>
public interface IChatService
{
    Task<ChatMessage?> GetMessageByIdAsync(int id);
    Task<IEnumerable<ChatMessage>> GetMessagesByEventIdAsync(int eventId, int limit = 100);
    Task<ChatMessage> CreateMessageAsync(int eventId, string userId, string message);
    Task DeleteMessageAsync(int id);
}
