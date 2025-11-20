using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for Label entity
/// </summary>
public class LabelRepository : Repository<Label>, ILabelRepository
{
    public LabelRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<Label?> GetByReferenceAsync(string reference)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Reference == reference);
    }

    public async Task<IEnumerable<Label>> GetActiveLabelsAsync()
    {
        return await _dbSet
            .AsNoTracking()
            .Where(l => l.IsActive)
            .ToListAsync();
    }
}
