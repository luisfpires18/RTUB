using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;

namespace RTUB.Application.Services;

/// <summary>
/// Chat service implementation
/// Contains business logic for chat message operations
/// </summary>
public class ChatService : IChatService
{
    private readonly ApplicationDbContext _context;
    private const int MaxMessageLength = 500;

    public ChatService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ChatMessage?> GetMessageByIdAsync(int id)
    {
        return await _context.ChatMessages
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesByEventIdAsync(int eventId, int limit = 100)
    {
        return await _context.ChatMessages
            .Where(m => m.EventId == eventId)
            .Include(m => m.User)
            .OrderByDescending(m => m.SentAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<ChatMessage> CreateMessageAsync(int eventId, string userId, string message)
    {
        // Validate message length
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Message cannot be empty", nameof(message));
        }

        if (message.Length > MaxMessageLength)
        {
            throw new ArgumentException($"Message cannot exceed {MaxMessageLength} characters", nameof(message));
        }

        var chatMessage = new ChatMessage
        {
            EventId = eventId,
            UserId = userId,
            Message = message,
            SentAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(chatMessage);
        await _context.SaveChangesAsync();

        // Reload with user data
        await _context.Entry(chatMessage).Reference(m => m.User).LoadAsync();

        return chatMessage;
    }

    public async Task DeleteMessageAsync(int id)
    {
        var message = await _context.ChatMessages.FindAsync(id);
        if (message != null)
        {
            _context.ChatMessages.Remove(message);
            await _context.SaveChangesAsync();
        }
    }
}
