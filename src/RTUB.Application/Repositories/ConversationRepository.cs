using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Conversation entity
/// </summary>
public class ConversationRepository : Repository<Conversation>, IConversationRepository
{
    public ConversationRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<Conversation>> GetUserConversationsAsync(string userId, bool includeArchived = false)
    {
        using var context = CreateContext();
        var query = context.Set<Conversation>()
            .AsNoTracking()
            .Where(c => c.Participants.Contains(userId));

        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        return await query
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt).Take(1))
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();
    }

    public async Task<Conversation?> GetWithMessagesAsync(int conversationId, int? limit = null)
    {
        using var context = CreateContext();
        var query = context.Set<Conversation>()
            .AsNoTracking()
            .Include(c => c.Messages.OrderByDescending(m => m.CreatedAt))
            .ThenInclude(m => m.Sender)
            .AsQueryable();

        if (limit.HasValue)
        {
            query = query.Select(c => new Conversation
            {
                Id = c.Id,
                Participants = c.Participants,
                LastMessageAt = c.LastMessageAt,
                LastMessageId = c.LastMessageId,
                Title = c.Title,
                IsSystemConversation = c.IsSystemConversation,
                IsArchived = c.IsArchived,
                IsGroup = c.IsGroup,
                CreatedByUserId = c.CreatedByUserId,
                CreatedAt = c.CreatedAt,
                CreatedBy = c.CreatedBy,
                UpdatedAt = c.UpdatedAt,
                UpdatedBy = c.UpdatedBy,
                Messages = c.Messages.OrderByDescending(m => m.CreatedAt).Take(limit.Value).ToList()
            });
        }

        return await query.FirstOrDefaultAsync(c => c.Id == conversationId);
    }

    public async Task<Conversation> GetOrCreateOneToOneAsync(string userId1, string userId2)
    {
        // Normalize participant order (alphabetically) for consistent lookup
        var participantIds = new List<string> { userId1, userId2 }.OrderBy(id => id).ToList();
        var participantsString = string.Join(";", participantIds);

        using var context = CreateContext();
        var conversation = await context.Set<Conversation>()
            .FirstOrDefaultAsync(c => c.Participants == participantsString && !c.IsSystemConversation && !c.IsGroup);

        if (conversation == null)
        {
            conversation = new Conversation
            {
                Participants = participantsString,
                LastMessageAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsGroup = false
            };

            await AddAsync(conversation);
        }

        return conversation;
    }

    public async Task<Conversation?> GetByParticipantsAsync(List<string> participantIds)
    {
        var participantsString = string.Join(";", participantIds.OrderBy(id => id));

        using var context = CreateContext();
        return await context.Set<Conversation>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Participants == participantsString);
    }

    public async Task<Conversation?> GetSystemConversationForUserAsync(string userId)
    {
        using var context = CreateContext();
        return await context.Set<Conversation>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Participants == userId && c.IsSystemConversation);
    }

    public async Task ArchiveConversationAsync(int conversationId)
    {
        var conversation = await GetByIdAsync(conversationId);
        if (conversation != null)
        {
            conversation.IsArchived = true;
            conversation.UpdatedAt = DateTime.UtcNow;
            await UpdateAsync(conversation);
        }
    }

    public async Task<Conversation?> GetGroupByTitleAsync(string title)
    {
        using var context = CreateContext();
        return await context.Set<Conversation>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsGroup && c.Title == title && !c.IsArchived);
    }

    public async Task<IEnumerable<Conversation>> GetSystemGroupsAsync()
    {
        using var context = CreateContext();
        return await context.Set<Conversation>()
            .AsNoTracking()
            .Where(c => c.IsGroup && c.CreatedByUserId == "system" && !c.IsArchived)
            .ToListAsync();
    }
}
