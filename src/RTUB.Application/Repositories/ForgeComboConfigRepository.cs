using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for ForgeComboConfig entity.
/// </summary>
public class ForgeComboConfigRepository : Repository<ForgeComboConfig>, IForgeComboConfigRepository
{
    public ForgeComboConfigRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<List<ForgeComboConfig>> GetAllOrderedAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .OrderBy(c => c.ComboKey)
            .ToListAsync();
    }

    public async Task<ForgeComboConfig?> GetByComboKeyAsync(string comboKey)
    {
        return await _dbSet
            .FirstOrDefaultAsync(c => c.ComboKey == comboKey);
    }

    public async Task<List<ForgeComboConfig>> GetAllWithPicturesAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Where(c => c.PictureUrl != null)
            .ToListAsync();
    }
}
