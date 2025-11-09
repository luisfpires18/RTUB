using RTUB.Core.Entities;

namespace RTUB.Core.Extensions;

/// <summary>
/// Extension methods for ApplicationUser
/// </summary>
public static class ApplicationUserExtensions
{
    /// <summary>
    /// Gets the full name of the user by combining first and last name
    /// </summary>
    /// <param name="user">The application user</param>
    /// <returns>Full name as "FirstName LastName"</returns>
    public static string GetFullName(this ApplicationUser user)
    {
        if (user == null)
            return string.Empty;
        
        return $"{user.FirstName} {user.LastName}";
    }

    /// <summary>
    /// Gets the profile picture URL with fallback to default avatar if not set
    /// </summary>
    /// <param name="user">The application user</param>
    /// <returns>Profile picture URL or default avatar path</returns>
    public static string GetProfilePictureUrl(this ApplicationUser user)
    {
        if (user == null)
            return "/images/default-avatar.png";
        
        return !string.IsNullOrEmpty(user.ProfilePictureSrc) 
            ? user.ProfilePictureSrc 
            : "/images/default-avatar.png";
    }
}
