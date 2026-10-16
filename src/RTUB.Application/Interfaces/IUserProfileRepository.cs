using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for ApplicationUser entity (Identity)
/// Provides data access operations for user profiles
/// Note: This repository complements UserManager<ApplicationUser> for custom queries
/// </summary>
public interface IUserProfileRepository : IRepository<ApplicationUser>
{

    /// <summary>
    /// Gets all users
    /// </summary>
    Task<IEnumerable<ApplicationUser>> GetAllUsersAsync();
}
