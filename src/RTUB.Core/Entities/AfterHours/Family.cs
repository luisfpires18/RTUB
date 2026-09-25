using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Entities.AfterHours;

/// <summary>
/// An After Hours family (a game crew; nothing to do with RTUB mentors, roles or MyTuno). Persistent:
/// it belongs to no cycle and survives academic-year resets. Never deleted: when its last member
/// leaves it is disbanded and keeps its name reserved.
/// </summary>
public class Family : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Trimmed, upper-invariant name; unique across all families, disbanded ones included.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    public string? Motto { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>Set when the last member left. A disbanded family takes no new members.</summary>
    public DateTime? DisbandedAtUtc { get; set; }

    public bool IsDisbanded => DisbandedAtUtc is not null;
}

/// <summary>
/// One stay of a user in a family. Persistent history: leaving sets <see cref="LeftAtUtc"/> and keeps the
/// row. Active = no <see cref="LeftAtUtc"/>; a user has at most one active membership and a family at
/// most one active Boss (filtered unique indexes).
/// </summary>
public class FamilyMembership : BaseEntity
{
    public int FamilyId { get; set; }
    public Family? Family { get; set; }
    public string UserId { get; set; } = string.Empty;
    public FamilyRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; }
    public DateTime? LeftAtUtc { get; set; }

    public bool IsActive => LeftAtUtc is null;
}

/// <summary>An invitation from a family's Boss. Does not expire and does not reserve a slot.</summary>
public class FamilyInvitation : BaseEntity
{
    public int FamilyId { get; set; }
    public string InvitedUserId { get; set; } = string.Empty;
    public string InvitedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public FamilyInvitationStatus Status { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

/// <summary>
/// A family's annual state in one cycle: the treasury (and, later, upgrades and score). One row per
/// family and cycle, created lazily; a new cycle starts at 0.
/// </summary>
public class FamilyCycleState : BaseEntity
{
    public int FamilyId { get; set; }
    public int GameCycleId { get; set; }
    public long TreasuryCash { get; set; }
}
