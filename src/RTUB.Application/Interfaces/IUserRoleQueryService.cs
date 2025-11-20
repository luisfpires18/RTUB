namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for user role query operations
/// Provides efficient batch queries for user roles
/// Extracts complex queries from Blazor pages following SOLID principles
/// </summary>
public interface IUserRoleQueryService
{
    /// <summary>
    /// Gets user roles for a list of user IDs in a single optimized query
    /// Performs a join between UserRoles and Roles tables
    /// </summary>
    /// <param name="userIds">List of user IDs to query</param>
    /// <returns>List of user ID and role name pairs</returns>
    Task<List<UserRoleDto>> GetUserRolesAsync(List<string> userIds);
}

/// <summary>
/// DTO representing a user-role relationship
/// Used for efficient batch loading of user roles
/// </summary>
public class UserRoleDto
{
    public string UserId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}
