using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.DTOs;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

/// <summary>
/// User role query service implementation
/// Provides efficient batch queries for user roles
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class UserRoleQueryService : IUserRoleQueryService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ApplicationDbContext _context;

    public UserRoleQueryService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _context = contextFactory.CreateDbContext();
    }

    /// <summary>
    /// Gets user roles for a list of user IDs in a single optimized query
    /// Extracts the query logic from Members.razor (lines 1053-1056)
    /// </summary>
    public async Task<List<UserRoleDto>> GetUserRolesAsync(List<string> userIds)
    {
        if (userIds == null || !userIds.Any())
        {
            return [];
        }

        // Query: Join UserRoles and Roles tables, filter by user IDs, select user ID and role name
        // Original query from Members.razor:
        // var userRolesQuery = await DbContext.UserRoles
        //     .Where(ur => userIdList.Contains(ur.UserId))
        //     .Join(DbContext.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
        //     .ToListAsync();

        var userRoles = await _context.UserRoles
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(_context.Roles,
                  ur => ur.RoleId,
                  r => r.Id,
                  (ur, r) => new UserRoleDto
                  {
                      UserId = ur.UserId,
                      RoleName = r.Name!
                  })
            .ToListAsync();

        return userRoles;
    }
}
