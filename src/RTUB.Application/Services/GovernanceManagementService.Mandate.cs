// Governance: sync the current mandate, remove an assignment, read the RGI.
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RTUB.Application.DTOs;
using RTUB.Application.Helpers;
using RTUB.Application.Interfaces;

namespace RTUB.Application.Services;

public sealed partial class GovernanceManagementService
{
    public async Task<int> SyncCurrentMandateAsync()
    {
        var current = FiscalYearHelper.GetCurrentFiscalYearStartYear();
        await using var db = await _contexts.CreateDbContextAsync();
        var held = (await db.RoleAssignments.AsNoTracking()
                .Where(a => a.StartYear == current && a.EndYear == current + 1)
                .Select(a => new { a.UserId, a.Position })
                .ToListAsync())
            .ToLookup(a => a.UserId, a => a.Position);
        if (held.Count == 0)
        {
            // A new fiscal year before its first assignment: the previous mandate stays in office.
            return 0;
        }

        // Positions is a JSON column: compared in memory (~100 members).
        var changed = (await db.Users.AsNoTracking().Select(u => new { u.Id, u.Positions }).ToListAsync())
            .Where(u => !(u.Positions ?? []).ToHashSet().SetEquals(held[u.Id]))
            .Select(u => u.Id)
            .ToList();

        foreach (var id in changed)
        {
            if (await _users.FindByIdAsync(id) is not { } member)
            {
                continue;
            }

            // Promote first, so a member re-elected to another seat keeps the role while the old seat goes.
            foreach (var position in held[id].Except(member.Positions).ToList())
            {
                await _roles.PromoteUserForPositionAsync(member, position, current, current);
            }

            foreach (var position in member.Positions.Except(held[id]).ToList())
            {
                await _roles.DemoteUserForPositionAsync(member, position, current, current);
            }
        }

        if (changed.Count > 0)
        {
            _logger.LogInformation("Mandate {FiscalYear}: positions and roles synced for {Count} member(s)",
                GovernanceService.Label(current), changed.Count);
        }

        return changed.Count;
    }

    public async Task<EventResult<GovernanceManageDto>> RemoveAsync(int assignmentId, ClaimsPrincipal user)
    {
        if (Refusal<GovernanceManageDto>(user) is { } refused)
        {
            return refused;
        }

        if (await _assignments.GetRoleAssignmentByIdAsync(assignmentId) is not { } assignment)
        {
            return EventResult<GovernanceManageDto>.Fail(EventResultStatus.NotFound);
        }

        await _assignments.DeleteRoleAssignmentAsync(assignment.Id);
        if (await _users.FindByIdAsync(assignment.UserId) is { } member)
        {
            await _roles.DemoteUserForPositionAsync(member, assignment.Position, assignment.StartYear, FiscalYearHelper.GetCurrentFiscalYearStartYear());
        }

        return EventResult<GovernanceManageDto>.Ok(await BuildAsync(GovernanceService.Label(assignment.StartYear)));
    }

    public async Task<EventResult<GovernanceRgiDto>> GetRgiAsync(ClaimsPrincipal user)
    {
        if (!GovernanceAuthorization.IsMember(user))
        {
            return EventResult<GovernanceRgiDto>.Fail(EventResultStatus.SignInRequired);
        }

        if (!GovernanceAuthorization.CanViewRgi(user))
        {
            return EventResult<GovernanceRgiDto>.Fail(EventResultStatus.Forbidden);
        }

        try
        {
            // Resolved here, not injected: storage that cannot be built (no R2 credentials) must not take the fiscal
            // year and assignment endpoints down with it.
            var documents = _services.GetRequiredService<IDocumentStorageService>();
            return EventResult<GovernanceRgiDto>.Ok(new GovernanceRgiDto(await documents.GetDocumentUrlAsync(RgiPath)));
        }
        catch (Exception ex)
        {
            // Any storage error answers "not available".
            _logger.LogWarning(ex, "Could not sign the RGI URL");
            return EventResult<GovernanceRgiDto>.Ok(new GovernanceRgiDto(null));
        }
    }
}
