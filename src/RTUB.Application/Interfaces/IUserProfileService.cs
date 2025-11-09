using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for user profile operations
/// Provides business logic for user profile management
/// </summary>
public interface IUserProfileService
{
    Task<ApplicationUser?> GetUserByIdAsync(string userId);
    Task<ApplicationUser?> GetUserByUsernameAsync(string username);
    Task<ApplicationUser?> GetUserByEmailAsync(string email);
    Task<IEnumerable<ApplicationUser>> GetAllUsersAsync();
    Task UpdateProfilePictureAsync(string userId, Stream imageStream, string fileName, string contentType);
    Task UpdateUserInfoAsync(string userId, string firstName, string lastName, string? nickname, DateTime? dateOfBirth, string? phoneContact);
    Task<bool> IsUserActiveAsync(string userId);
    
    // User Role Management methods
    Task<List<string>> GetUserRolesAsync(string userId);
    Task AddUserToRoleAsync(string userId, string roleName);
    Task RemoveUserFromRoleAsync(string userId, string roleName);
    Task<bool> IsUserInRoleAsync(string userId, string roleName);
}
