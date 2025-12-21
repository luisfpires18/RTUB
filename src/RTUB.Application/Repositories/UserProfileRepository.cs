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
    public UserProfileRepository(ApplicationDbContext context) : base(context)
    {
    }

    public override async Task UpdateAsync(ApplicationUser entity)
    {
        var localUser = _context.Users.Local.FirstOrDefault(u => u.Id == entity.Id);
        if (localUser != null && localUser != entity)
        {
            _context.Entry(localUser).State = EntityState.Detached;
        }

        var entry = _context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            _context.Users.Attach(entity);
        }

        entry.State = EntityState.Modified;
        await SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<ApplicationUser?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.UserName == username);
    }

    public async Task<IEnumerable<ApplicationUser>> GetAllUsersAsync()
    {
        return await _context.Users
            .ToListAsync();
    }
}
