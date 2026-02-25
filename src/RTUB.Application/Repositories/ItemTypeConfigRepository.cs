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
        return await _dbSet
            .AsNoTracking()
            .OrderBy(c => c.Category)
            .ThenBy(c => c.DisplayName)
            .ToListAsync();
    }

    public async Task<List<ItemTypeConfig>> GetByCategoryOrderedAsync(string category)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(c => c.Category == category)
            .OrderBy(c => c.DisplayName)
            .ToListAsync();
    }

    public async Task<ItemTypeConfig?> GetByTypeKeyAsync(string typeKey)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.TypeKey == typeKey);
    }

    public async Task<bool> ExistsByTypeKeyAsync(string typeKey)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(c => c.TypeKey == typeKey);
    }
}
