using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

/// <summary>A family member as other players see them: name, game role, join date, level. No money.</summary>
public sealed record FamilyMemberView(int MembershipId, string UserId, string DisplayName, FamilyRole Role, DateTime JoinedAtUtc, int? Level);

/// <summary>A pending invitation. <see cref="OtherName"/> is the inviter (incoming) or the invitee (outgoing).</summary>
public sealed record FamilyInvitationView(int InvitationId, int FamilyId, string FamilyName, string OtherName, DateTime CreatedAtUtc);

/// <summary>A current-cycle player the Boss could invite.</summary>
public sealed record FamilyCandidateView(int StateId, string DisplayName, int Level);

public sealed record FamilyOverview(
    Family? Family,
    FamilyMemberView? Me,
    IReadOnlyList<FamilyMemberView> Members,
    long Treasury,
    IReadOnlyList<FamilyInvitationView> IncomingInvitations,
    IReadOnlyList<FamilyInvitationView> OutgoingInvitations,
    IReadOnlyList<FamilyCandidateView> InviteCandidates,
    DateTime? JoinAllowedFromUtc);

/// <summary>Family reads. Every write goes through <see cref="IAfterHoursActionService"/>.</summary>
public interface IFamilyService
{
    /// <summary>Everything the family page shows for this user, or null when no cycle is active.</summary>
    Task<FamilyOverview?> GetOverviewAsync(string userId);
}
