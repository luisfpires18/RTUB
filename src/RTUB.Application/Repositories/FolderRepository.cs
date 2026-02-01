using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Folder entity
/// Note: Visibility filtering logic is handled in the service layer as it requires user context
/// </summary>
public class FolderRepository : Repository<Folder>, IFolderRepository
{
    public FolderRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<Folder?> GetByIdAsync(int id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(f => f.Documents)
            .Include(f => f.FolderViewers)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public async Task<Folder?> GetByNormalizedKeyAsync(string normalizedKey)
    {
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return null;

        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.NormalizedKey == normalizedKey);
    }

    public override async Task<IEnumerable<Folder>> GetAllAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Include(f => f.Documents)
            .OrderBy(f => f.DisplayName)
            .ToListAsync();
    }

    public async Task<IEnumerable<Folder>> GetVisibleFoldersAsync(string userId, bool isAdmin)
    {
        // Note: This is a simplified version. Full visibility filtering is handled in the service layer
        // because it requires ApplicationUser context which creates tight coupling in repository
        var query = _dbSet
            .AsNoTracking()
            .Include(f => f.Documents)
            .OrderBy(f => f.DisplayName);

        return await query.ToListAsync();
    }

    public override async Task<Folder> AddAsync(Folder folder)
    {
        await _dbSet.AddAsync(folder);
        await SaveChangesAsync();
        return folder;
    }

    public override async Task UpdateAsync(Folder folder)
    {
        await base.UpdateAsync(folder);
    }

    public override async Task DeleteAsync(int id)
    {
        await base.DeleteAsync(id);
    }

    public async Task<bool> ExistsAsync(string normalizedKey)
    {
        if (string.IsNullOrWhiteSpace(normalizedKey))
            return false;

        return await _dbSet
            .AnyAsync(f => f.NormalizedKey == normalizedKey);
    }
}
