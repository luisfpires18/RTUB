using Microsoft.AspNetCore.Authorization;

namespace RTUB.Application.Authorization;

/// <summary>
/// Requirement that checks if user has a specific permission claim
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }

    public PermissionRequirement(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
    }
}
