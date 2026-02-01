using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Document entity
/// </summary>
public class DocumentRepository : Repository<Document>, IDocumentRepository
{
    public DocumentRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task<Document?> GetByIdAsync(int id)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(d => d.Folder)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task<Document?> GetByObjectKeyAsync(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return null;

        return await _dbSet
            .AsNoTracking()
            .Include(d => d.Folder)
            .FirstOrDefaultAsync(d => d.ObjectKey == objectKey);
    }

    public async Task<IEnumerable<Document>> GetByFolderIdAsync(int folderId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(d => d.FolderId == folderId)
            .OrderBy(d => d.DisplayName)
            .ToListAsync();
    }

    public override async Task<Document> AddAsync(Document document)
    {
        await _dbSet.AddAsync(document);
        await SaveChangesAsync();
        return document;
    }

    public override async Task UpdateAsync(Document document)
    {
        await base.UpdateAsync(document);
    }

    public override async Task DeleteAsync(int id)
    {
        await base.DeleteAsync(id);
    }

    public async Task<bool> ExistsAsync(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            return false;

        return await _dbSet
            .AnyAsync(d => d.ObjectKey == objectKey);
    }
}
