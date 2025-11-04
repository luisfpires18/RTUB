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
    Task<IEnumerable<RoleAssignment>> GetRoleAssignmentsByUserIdAsync(string userId);
    Task<IEnumerable<RoleAssignment>> GetRoleAssignmentsByPositionAsync(Position position);
    Task<RoleAssignment> CreateRoleAssignmentAsync(string userId, Position position, int startYear, int endYear, string? notes = null, string? createdBy = null);
    Task UpdateRoleAssignmentAsync(int id, Position position, int startYear, int endYear, string? notes);
    Task DeleteRoleAssignmentAsync(int id);
}
