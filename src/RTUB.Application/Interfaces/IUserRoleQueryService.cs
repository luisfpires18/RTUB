using RTUB.Application.DTOs;

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
