using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Repository interface for RoleAssignment entity
/// Provides data access operations for role assignments
/// </summary>
public interface IRoleAssignmentRepository : IRepository<RoleAssignment>
{
    /// <summary>
    /// Gets role assignments by user ID
    /// </summary>
    Task<IEnumerable<RoleAssignment>> GetByUserIdAsync(string userId);
    
    /// <summary>
    /// Gets role assignments by position
    /// </summary>
    Task<IEnumerable<RoleAssignment>> GetByPositionAsync(Position position);
}
