using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for MemberInstrument entity
/// </summary>
public class MemberInstrumentRepository : Repository<MemberInstrument>, IMemberInstrumentRepository
{
    public MemberInstrumentRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<MemberInstrument>> GetByMemberIdAsync(string memberId)
    {
        using var context = CreateContext();
        return await context.Set<MemberInstrument>()
            .AsNoTracking()
            .Where(mi => mi.MemberId == memberId)
            .OrderByDescending(mi => mi.IsPrimary)
            .ThenBy(mi => mi.InstrumentType)
            .ToListAsync();
    }

    public async Task<MemberInstrument?> GetPrimaryInstrumentAsync(string memberId)
    {
        using var context = CreateContext();
        return await context.Set<MemberInstrument>()
            .AsNoTracking()
            .FirstOrDefaultAsync(mi => mi.MemberId == memberId && mi.IsPrimary);
    }
}
