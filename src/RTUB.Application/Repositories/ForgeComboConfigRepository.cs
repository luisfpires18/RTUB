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
        using var context = CreateContext();
        return await context.Set<ForgeComboConfig>()
            .AsNoTracking()
            .OrderBy(c => c.ComboKey)
            .ToListAsync();
    }

    public async Task<ForgeComboConfig?> GetByComboKeyAsync(string comboKey)
    {
        using var context = CreateContext();
        return await context.Set<ForgeComboConfig>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ComboKey == comboKey);
    }

    public async Task<List<ForgeComboConfig>> GetAllWithPicturesAsync()
    {
        using var context = CreateContext();
        return await context.Set<ForgeComboConfig>()
            .AsNoTracking()
            .Where(c => c.PictureUrl != null)
            .ToListAsync();
    }
}
