using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;

namespace RTUB.Application.Repositories;

/// <summary>
/// Repository implementation for ApplicationUser entity (Identity)
/// Note: This repository complements UserManager for custom queries
/// Identity operations should still use UserManager
/// </summary>
public class UserProfileRepository : Repository<ApplicationUser>, IUserProfileRepository
{
    public UserProfileRepository(IDbContextFactory<ApplicationDbContext> contextFactory) : base(contextFactory)
    {
    }

    public override async Task UpdateAsync(ApplicationUser entity)
    {
        using var context = CreateContext();
        // Use Entry().State instead of Update() to avoid traversing nav props
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<ApplicationUser?> GetByUsernameAsync(string username)
    {
        using var context = CreateContext();
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == username);
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
    {
        using var context = CreateContext();
        return await context.Users
            .AsNoTracking()
            .ToListAsync();
    }
}
