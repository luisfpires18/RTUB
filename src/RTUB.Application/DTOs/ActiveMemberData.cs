using RTUB.Core.Entities;

namespace RTUB.Application.DTOs;

/// <summary>
/// DTO containing active member data with their status information.
/// Used for displaying member lists with status data in the Members page.
/// </summary>
public class ActiveMemberData
{
    /// <summary>
    /// The member's application user entity.
    /// </summary>
    public ApplicationUser Member { get; set; } = null!;

    /// <summary>
    /// The member's status data including retirement status and activity dates.
    /// </summary>
    public MemberStatusResult? StatusData { get; set; }
}
