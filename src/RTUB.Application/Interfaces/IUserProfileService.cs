using RTUB.Core.Entities;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for user profile operations
/// Provides business logic for user profile management
/// </summary>
public interface IUserProfileService
{
    Task<ApplicationUser?> GetUserByIdAsync(string userId);
    Task UpdateProfilePictureAsync(string userId, Stream imageStream, string fileName, string contentType);

    /// <summary>
    /// Deletes a member and all related entities that have FK constraints preventing direct deletion
    /// </summary>
    /// <param name="userId">The user ID to delete</param>
    /// <returns>True if deletion succeeded, false otherwise</returns>
    Task<bool> DeleteMemberWithRelatedDataAsync(string userId);

}
