using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for ItemTypeConfig entity.
/// </summary>
public class ItemTypeConfigRepository : Repository<ItemTypeConfig>, IItemTypeConfigRepository
{
    public ItemTypeConfigRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<List<ItemTypeConfig>> GetAllOrderedAsync()
    {
        using var context = CreateContext();
        return await context.Set<ItemTypeConfig>()
            .AsNoTracking()
            .OrderBy(c => c.Category)
            .ThenBy(c => c.DisplayName)
            .ToListAsync();
    }

    public async Task<List<ItemTypeConfig>> GetByCategoryOrderedAsync(string category)
    {
        using var context = CreateContext();
        return await context.Set<ItemTypeConfig>()
            .AsNoTracking()
            .Where(c => c.Category == category)
            .OrderBy(c => c.DisplayName)
            .ToListAsync();
    }

    public async Task<ItemTypeConfig?> GetByTypeKeyAsync(string typeKey)
    {
        using var context = CreateContext();
        return await context.Set<ItemTypeConfig>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TypeKey == typeKey);
    }

    public async Task<bool> ExistsByTypeKeyAsync(string typeKey)
    {
        using var context = CreateContext();
        return await context.Set<ItemTypeConfig>()
            .AsNoTracking()
            .AnyAsync(c => c.TypeKey == typeKey);
    }
}
