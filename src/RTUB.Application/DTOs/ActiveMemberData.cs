using RTUB.Core.Entities;

namespace RTUB.Application.DTOs;

/// <summary>
/// DTO for active member data including member and status information
/// Used in Members page for displaying active members with their status
/// </summary>
public class ActiveMemberData
{
    /// <summary>
    /// The member user entity
    /// </summary>
    public ApplicationUser Member { get; set; } = null!;

    /// <summary>
    /// The member's status result (retirement status, last activity dates, etc.)
    /// </summary>
    public MemberStatusResult? StatusData { get; set; }
}
