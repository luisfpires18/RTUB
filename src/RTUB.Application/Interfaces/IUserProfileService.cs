using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for user profile operations
/// Provides business logic for user profile management
/// </summary>
public interface IUserProfileService
{
    Task<ApplicationUser?> GetUserByIdAsync(string userId);
    Task<IEnumerable<ApplicationUser>> GetAllUsersAsync();
    Task UpdateProfilePictureAsync(string userId, Stream imageStream, string fileName, string contentType);
    Task UpdateUserInfoAsync(string userId, string firstName, string lastName, string? nickname, DateTime? dateOfBirth, string? phoneContact);
    Task<bool> IsUserActiveAsync(string userId);

    /// <summary>
    /// Deletes a member and all related entities that have FK constraints preventing direct deletion
    /// </summary>
    /// <param name="userId">The user ID to delete</param>
    /// <returns>True if deletion succeeded, false otherwise</returns>
    Task<bool> DeleteMemberWithRelatedDataAsync(string userId);

    /// <summary>
    /// Gets a user's categories (MemberCategory) without change tracking.
    /// Useful for read-only checks like determining if a user is a Leitão.
    /// </summary>
    /// <param name="userId">The user ID to get categories for</param>
    /// <returns>The user's categories, or empty collection if user not found</returns>
    Task<IEnumerable<Core.Enums.MemberCategory>> GetUserCategoriesAsync(string userId);
}
