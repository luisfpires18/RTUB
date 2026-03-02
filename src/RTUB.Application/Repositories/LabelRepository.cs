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
    public LabelRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Label?> GetByReferenceAsync(string reference)
    {
        using var context = CreateContext();
        return await context.Set<Label>()
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Reference == reference);
    }

    public async Task<IEnumerable<Label>> GetActiveLabelsAsync()
    {
        using var context = CreateContext();
        return await context.Set<Label>()
            .AsNoTracking()
            .Where(l => l.IsActive)
            .ToListAsync();
    }
}
