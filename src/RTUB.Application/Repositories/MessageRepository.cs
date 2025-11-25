using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Message entity
/// </summary>
public class MessageRepository : Repository<Message>, IMessageRepository
{
    public MessageRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Message>> GetConversationMessagesAsync(int conversationId, int? limit = null, int? offset = null)
    {
        var query = _dbSet
            .Where(m => m.ConversationId == conversationId)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.CreatedAt);

        if (offset.HasValue)
        {
            query = (IOrderedQueryable<Message>)query.Skip(offset.Value);
        }

        if (limit.HasValue)
        {
            query = (IOrderedQueryable<Message>)query.Take(limit.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<int> GetUnreadCountForUserAsync(string userId)
    {
        // Count messages in conversations where user is a participant
        // and the message is not read by the user and not sent by the user
        return await _dbSet
            .Where(m => m.Conversation != null &&
                        m.Conversation.Participants.Contains(userId) &&
                        !m.Conversation.IsArchived &&
                        m.SenderId != userId &&
                        !m.ReadBy.Contains(userId))
            .CountAsync();
    }

    public async Task<int> GetUnreadCountForConversationAsync(int conversationId, string userId)
    {
        return await _dbSet
            .Where(m => m.ConversationId == conversationId &&
                        m.SenderId != userId &&
                        !m.ReadBy.Contains(userId))
            .CountAsync();
    }

    public async Task MarkConversationAsReadAsync(int conversationId, string userId)
    {
        var unreadMessages = await _dbSet
            .Where(m => m.ConversationId == conversationId &&
                        m.SenderId != userId &&
                        !m.ReadBy.Contains(userId))
            .ToListAsync();

        foreach (var message in unreadMessages)
        {
            message.MarkAsReadBy(userId);
        }

        if (unreadMessages.Any())
        {
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Message?> GetLatestMessageAsync(int conversationId)
    {
        return await _dbSet
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync();
    }
}
