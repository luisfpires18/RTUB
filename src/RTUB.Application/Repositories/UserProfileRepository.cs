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
        context.Users.Update(entity);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<ApplicationUser?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == username);
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
    {
        return await _context.Users
            .AsNoTracking()
            .ToListAsync();
    }
}
