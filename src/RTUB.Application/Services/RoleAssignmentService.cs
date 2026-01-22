using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using RTUB.Core.Exceptions;


namespace RTUB.Application.Services;

/// <summary>
/// RoleAssignment service implementation
/// Contains business logic for role assignment operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class RoleAssignmentService : IRoleAssignmentService
{
    private readonly IRoleAssignmentRepository _roleAssignmentRepository;

    public RoleAssignmentService(IRoleAssignmentRepository roleAssignmentRepository)
    {
        _roleAssignmentRepository = roleAssignmentRepository;
    }

    public async Task<RoleAssignment?> GetRoleAssignmentByIdAsync(int id)
    {
        return await _roleAssignmentRepository.GetByIdAsync(id);
    }

    public async Task<IEnumerable<RoleAssignment>> GetAllRoleAssignmentsAsync()
    {
        return await _roleAssignmentRepository.GetAllAsync();
    }

    public async Task<IEnumerable<RoleAssignment>> GetRoleAssignmentsByUserIdAsync(string userId)
    {
        return await _roleAssignmentRepository.GetByUserIdAsync(userId);
    }

    public async Task<IEnumerable<RoleAssignment>> GetRoleAssignmentsByPositionAsync(Position position)
    {
        return await _roleAssignmentRepository.GetByPositionAsync(position);
    }

    public async Task<RoleAssignment> CreateRoleAssignmentAsync(string userId, Position position, int startYear, int endYear, string? notes = null, string? createdBy = null)
    {
        var roleAssignment = RoleAssignment.Create(userId, position, startYear, endYear, notes, createdBy);
        return await _roleAssignmentRepository.AddAsync(roleAssignment);
    }

    public async Task UpdateRoleAssignmentAsync(int id, Position position, int startYear, int endYear, string? notes)
    {
        var roleAssignment = await _roleAssignmentRepository.GetByIdAsync(id);
        if (roleAssignment == null)
            throw new EntityNotFoundException(nameof(RoleAssignment), id);

        roleAssignment.UpdateDetails(position, startYear, endYear, notes);
        await _roleAssignmentRepository.UpdateAsync(roleAssignment);
    }

    public async Task DeleteRoleAssignmentAsync(int id)
    {
        var roleAssignment = await _roleAssignmentRepository.GetByIdAsync(id);
        if (roleAssignment == null)
            throw new EntityNotFoundException(nameof(RoleAssignment), id);

        await _roleAssignmentRepository.DeleteAsync(id);
    }
}
