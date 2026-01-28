using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for member position operations
/// Extracted from Members.razor to improve separation of concerns
/// </summary>
public class MemberPositionService : IMemberPositionService
{
    /// <summary>
    /// Gets the current fiscal year position for a user
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="userRoleAssignments">Dictionary mapping userId to their role assignments</param>
    /// <returns>The position for the current fiscal year, or null if none</returns>
    public Position? GetCurrentFiscalYearPosition(string userId, Dictionary<string, List<RoleAssignment>> userRoleAssignments)
    {
        int currentFiscalStartYear = FiscalYearHelper.GetCurrentFiscalYearStartYear();

        // Check if user has a role assignment for current fiscal year
        if (userRoleAssignments.ContainsKey(userId))
        {
            var currentAssignment = userRoleAssignments[userId]
                .FirstOrDefault(ra => ra.StartYear == currentFiscalStartYear);

            return currentAssignment?.Position;
        }

        return null;
    }
}
