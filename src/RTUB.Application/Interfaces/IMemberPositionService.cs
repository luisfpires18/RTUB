using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for member position operations
/// Extracted from Members.razor to improve separation of concerns
/// </summary>
public interface IMemberPositionService
{
    /// <summary>
    /// Gets the current fiscal year position for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="userRoleAssignments">Dictionary mapping userId to their role assignments</param>
    /// <returns>The position for the current fiscal year, or null if none</returns>
    Position? GetCurrentFiscalYearPosition(string userId, Dictionary<string, List<RoleAssignment>> userRoleAssignments);
}
