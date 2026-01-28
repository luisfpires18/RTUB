using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for validating role assignments
/// Extracted from Roles.razor to improve separation of concerns
/// </summary>
public interface IRoleAssignmentValidationService
{
    /// <summary>
    /// Validates if a role assignment can be created for a member
    /// </summary>
    /// <param name="member">The member to assign the role to</param>
    /// <param name="position">The position to assign</param>
    /// <param name="fiscalYear">The fiscal year string (format: "YYYY-YYYY")</param>
    /// <param name="existingAssignments">Collection of existing role assignments to check for conflicts</param>
    /// <returns>Validation result with error message if validation fails, null if valid</returns>
    string? ValidateRoleAssignment(
        ApplicationUser member,
        Position position,
        string fiscalYear,
        IEnumerable<RoleAssignment> existingAssignments);
}
