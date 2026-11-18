using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service interface for RoleAssignment operations
/// Abstracts business logic from presentation layer
/// </summary>
public interface IRoleAssignmentService
{
    Task<RoleAssignment?> GetRoleAssignmentByIdAsync(int id);
    Task<IEnumerable<RoleAssignment>> GetAllRoleAssignmentsAsync();
    Task<RoleAssignment> CreateRoleAssignmentAsync(string userId, Position position, int startYear, int endYear, string? notes = null, string? createdBy = null);
    Task DeleteRoleAssignmentAsync(int id);
}
