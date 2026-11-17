using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for NaipeComment entity
/// </summary>
public class NaipeCommentRepository : Repository<NaipeComment>, INaipeCommentRepository
{
    public NaipeCommentRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<NaipeComment?> GetByIdWithDetailsAsync(int id)
    {
        using var context = CreateContext();
        return await context.Set<NaipeComment>()
            .AsNoTracking()
            .Include(c => c.Author)
            .FirstOrDefaultAsync(c => c.Id == id);
    }
}
