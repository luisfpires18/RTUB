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
    public ConversationUserSettingsRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<ConversationUserSettings?> GetByUserAndConversationAsync(string userId, int conversationId)
    {
        using var context = CreateContext();
        return await context.Set<ConversationUserSettings>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.ConversationId == conversationId);
    }

    public async Task<IEnumerable<ConversationUserSettings>> GetByUserAsync(string userId)
    {
        using var context = CreateContext();
        return await context.Set<ConversationUserSettings>()
            .AsNoTracking()
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

    public async Task<HashSet<string>> GetMutedUserIdsAsync(int conversationId, IEnumerable<string> userIds)
    {
        var userIdList = userIds.ToList();
        if (userIdList.Count == 0)
            return [];

        using var context = CreateContext();
        var mutedUserIds = await context.Set<ConversationUserSettings>()
            .AsNoTracking()
            .Where(s => s.ConversationId == conversationId && userIdList.Contains(s.UserId) && s.IsMuted)
            .Select(s => s.UserId)
            .ToListAsync();

        return [.. mutedUserIds];
    }
}
