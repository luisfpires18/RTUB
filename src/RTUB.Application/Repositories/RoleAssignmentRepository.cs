using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for RoleAssignment entity
/// </summary>
public class RoleAssignmentRepository : Repository<RoleAssignment>, IRoleAssignmentRepository
{
    public RoleAssignmentRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<IEnumerable<RoleAssignment>> GetByUserIdAsync(string userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(ra => ra.UserId == userId)
            .ToListAsync();
    }

    public async Task<IEnumerable<RoleAssignment>> GetByPositionAsync(Position position)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(ra => ra.Position == position)
            .ToListAsync();
    }
}
