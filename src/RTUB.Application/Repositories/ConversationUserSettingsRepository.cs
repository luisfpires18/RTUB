using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for ConversationUserSettings entity
/// </summary>
public class ConversationUserSettingsRepository : Repository<ConversationUserSettings>, IConversationUserSettingsRepository
{
    public ConversationUserSettingsRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<ConversationUserSettings?> GetByUserAndConversationAsync(string userId, int conversationId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ConversationId == conversationId);
    }

    public async Task<IEnumerable<ConversationUserSettings>> GetByUserAsync(string userId)
    {
        return await _dbSet
            .Where(s => s.UserId == userId)
            .ToListAsync();
    }

    public async Task<ConversationUserSettings> GetOrCreateAsync(string userId, int conversationId)
    {
        var settings = await GetByUserAndConversationAsync(userId, conversationId);

        if (settings == null)
        {
            settings = new ConversationUserSettings
            {
                UserId = userId,
                ConversationId = conversationId,
                IsMuted = false,
                IsPinned = false,
                CreatedAt = DateTime.UtcNow
            };

            await AddAsync(settings);
        }

        return settings;
    }

    public async Task<bool> IsConversationMutedAsync(string userId, int conversationId)
    {
        var settings = await GetByUserAndConversationAsync(userId, conversationId);
        return settings?.IsMuted ?? false;
    }

    public async Task<bool> IsConversationPinnedAsync(string userId, int conversationId)
    {
        var settings = await GetByUserAndConversationAsync(userId, conversationId);
        return settings?.IsPinned ?? false;
    }
}
