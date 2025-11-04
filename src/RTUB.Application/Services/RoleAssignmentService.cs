using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;


namespace RTUB.Application.Services;

/// <summary>
/// RoleAssignment service implementation
/// Contains business logic for role assignment operations
/// Follows Single Responsibility and Dependency Inversion principles
/// </summary>
public class RoleAssignmentService : IRoleAssignmentService
{
    private readonly ApplicationDbContext _context;

    public RoleAssignmentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RoleAssignment?> GetRoleAssignmentByIdAsync(int id)
    {
        return await _context.RoleAssignments.FindAsync(id);
    }

    public async Task<IEnumerable<RoleAssignment>> GetAllRoleAssignmentsAsync()
    {
        return await _context.RoleAssignments.ToListAsync();
    }

    public async Task<IEnumerable<RoleAssignment>> GetRoleAssignmentsByUserIdAsync(string userId)
    {
        return await _context.RoleAssignments
            .Where(ra => ra.UserId == userId)
            .ToListAsync();
    }

    public async Task<IEnumerable<RoleAssignment>> GetRoleAssignmentsByPositionAsync(Position position)
    {
        var allAssignments = await _context.RoleAssignments.ToListAsync();
        return allAssignments.Where(ra => ra.Position == position);
    }

    public async Task<RoleAssignment> CreateRoleAssignmentAsync(string userId, Position position, int startYear, int endYear, string? notes = null, string? createdBy = null)
    {
        var roleAssignment = RoleAssignment.Create(userId, position, startYear, endYear, notes, createdBy);
        _context.RoleAssignments.Add(roleAssignment);
        await _context.SaveChangesAsync();
        return roleAssignment;
    }

    public async Task UpdateRoleAssignmentAsync(int id, Position position, int startYear, int endYear, string? notes)
    {
        var roleAssignment = await _context.RoleAssignments.FindAsync(id);
        if (roleAssignment == null)
            throw new InvalidOperationException($"RoleAssignment with ID {id} not found");

        roleAssignment.UpdateDetails(position, startYear, endYear, notes);
        _context.RoleAssignments.Update(roleAssignment);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteRoleAssignmentAsync(int id)
    {
        var roleAssignment = await _context.RoleAssignments.FindAsync(id);
        if (roleAssignment == null)
            throw new InvalidOperationException($"RoleAssignment with ID {id} not found");

        _context.RoleAssignments.Remove(roleAssignment);
        await _context.SaveChangesAsync();
    }
}
