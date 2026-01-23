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

    public async Task<Dictionary<int, int>> GetUnreadCountsForConversationsAsync(IEnumerable<int> conversationIds, string userId)
    {
        var conversationIdList = conversationIds.ToList();
        if (!conversationIdList.Any())
        {
            return new Dictionary<int, int>();
        }

        var unreadCounts = await _dbSet
            .Where(m => conversationIdList.Contains(m.ConversationId) &&
                        m.SenderId != userId &&
                        !m.ReadBy.Contains(userId))
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ConversationId, x => x.Count);

        // Ensure all conversation IDs are in the result (with 0 count if no unread messages)
        var result = new Dictionary<int, int>();
        foreach (var conversationId in conversationIdList)
        {
            result[conversationId] = unreadCounts.GetValueOrDefault(conversationId, 0);
        }

        return result;
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

    public async Task<Dictionary<int, Message?>> GetLatestMessagesForConversationsAsync(IEnumerable<int> conversationIds)
    {
        var conversationIdList = conversationIds.ToList();
        if (!conversationIdList.Any())
        {
            return new Dictionary<int, Message?>();
        }

        // Get the latest message for each conversation using a subquery approach
        // This is more efficient than loading all messages and grouping in memory
        var latestMessages = await _dbSet
            .Where(m => conversationIdList.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.CreatedAt).FirstOrDefault()!)
            .ToListAsync();

        // Build dictionary, ensuring all conversation IDs are present (null if no messages)
        var result = new Dictionary<int, Message?>();
        foreach (var conversationId in conversationIdList)
        {
            result[conversationId] = latestMessages.FirstOrDefault(m => m.ConversationId == conversationId);
        }

        return result;
    }
}
