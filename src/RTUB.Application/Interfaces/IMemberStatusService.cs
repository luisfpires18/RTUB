using RTUB.Application.DTOs;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Member activity status (active / Reformado), rules in <see cref="Services.MemberActivityStatusEvaluator"/>.
/// <c>ApplicationUser.IsRetired</c> is the authoritative flag, <c>MemberStatus</c> its calculated projection.
/// <b>Read</b> methods calculate for display and never write; <b>Reconcile</b> methods persist transitions, audit and notify.
/// </summary>
public interface IMemberStatusService
{
    /// <summary>Read: one member's status (persisted flag, computed progress). No writes. Throws <see cref="InvalidOperationException"/> for an unknown user.</summary>
    Task<MemberStatusResult> GetMemberStatusAsync(string userId);

    /// <summary>Read: several members; unknown ids map to null. No writes.</summary>
    Task<Dictionary<string, MemberStatusResult?>> GetMemberStatusesBatchAsync(IEnumerable<string> userIds);

    /// <summary>Reconcile one member (rehearsal attendance / event enrollment triggers).</summary>
    Task<MemberStatusResult> ReconcileMemberStatusAsync(string userId);

    /// <summary>Reconcile every member, active and Reformado alike (startup/daily job, Owner sync). Per-member failures are logged and counted.</summary>
    Task<MemberStatusReconciliationResult> ReconcileAllMemberStatusesAsync();
}
