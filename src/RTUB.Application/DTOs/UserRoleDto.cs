namespace RTUB.Application.DTOs;

/// <summary>
/// DTO representing a user-role relationship
/// Used for efficient batch loading of user roles
/// </summary>
public class UserRoleDto
{
    public string UserId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
}
