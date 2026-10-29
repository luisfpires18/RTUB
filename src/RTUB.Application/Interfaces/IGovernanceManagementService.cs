using System.Security.Claims;
using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>Members' side of the Órgãos Sociais (React track 016): the RGI, fiscal years and position assignments.</summary>
public interface IGovernanceManagementService
{
    /// <summary><paramref name="fiscalYear"/> when it exists; else the current one when created; else the newest.</summary>
    Task<EventResult<GovernanceManageDto>> GetAsync(string? fiscalYear, ClaimsPrincipal user);

    Task<EventResult<GovernanceManageDto>> CreateFiscalYearAsync(GovernanceFiscalYearInput input, ClaimsPrincipal user);

    Task<EventResult<IReadOnlyList<GovernanceCandidateDto>>> SearchMembersAsync(string? query, ClaimsPrincipal user);

    Task<EventResult<GovernanceManageDto>> AssignAsync(GovernanceAssignmentInput input, ClaimsPrincipal user);

    Task<EventResult<GovernanceManageDto>> RemoveAsync(int assignmentId, ClaimsPrincipal user);

    Task<EventResult<GovernanceRgiDto>> GetRgiAsync(ClaimsPrincipal user);

    /// <summary>
    /// Once the current fiscal year has holders, every member's positions (and Admin / Mod, through
    /// <see cref="IRoleManagementService"/>) follow that mandate only: past holders lose their seats (forced sign-out
    /// when the role drops), missing seats are granted. No holders yet: nothing changes. Idempotent. Returns the
    /// number of members changed.
    /// </summary>
    Task<int> SyncCurrentMandateAsync();
}
