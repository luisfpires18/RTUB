using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Entities.AfterHours;

/// <summary>
/// A player's progress on one objective instance: a daily objective for one Lisbon day, or a weekly one
/// for one game week. One row per state, period, period key and objective (unique index). Target, XP
/// and points are copied from the catalogue when the row is created and never recalculated, so tuning
/// the catalogue later leaves history untouched.
/// </summary>
public class PlayerObjectiveProgress : BaseEntity
{
    public int GameCycleId { get; set; }
    public int PlayerCycleStateId { get; set; }
    public ObjectivePeriod Period { get; set; }

    /// <summary>Daily: the Lisbon <see cref="DateOnly.DayNumber"/>. Weekly: the 1-based game week.</summary>
    public int PeriodKey { get; set; }

    public string ObjectiveKey { get; set; } = string.Empty;
    public ObjectiveCategory? Category { get; set; }
    public long Progress { get; set; }
    public long Target { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    /// <summary>Daily XP actually awarded (0 until completed).</summary>
    public long XpAwarded { get; set; }

    /// <summary>Weekly championship points actually awarded (0 until completed; PvP grows per credited win).</summary>
    public int PointsAwarded { get; set; }
}

/// <summary>
/// A family's shared progress on one weekly objective, attributed to the family the acting member
/// belonged to when the action was accepted. Unique per family, cycle, week and objective.
/// </summary>
public class FamilyObjectiveProgress : BaseEntity
{
    public int FamilyId { get; set; }
    public int GameCycleId { get; set; }
    public int Week { get; set; }
    public string ObjectiveKey { get; set; } = string.Empty;
    public long Progress { get; set; }
    public long Target { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int PointsAwarded { get; set; }
}

/// <summary>
/// A PvP win that qualified for objectives: the attacker won, the target was outside the attacker's
/// family and not new-player protected. It records the week's distinct-target decisions so they can
/// never count twice (filtered unique indexes) and the individual points it awarded.
/// </summary>
public class PvpObjectiveCredit : BaseEntity
{
    public int GameCycleId { get; set; }
    public int Week { get; set; }
    public int PvpBattleId { get; set; }
    public PvpBattle? PvpBattle { get; set; }
    public int AttackerStateId { get; set; }
    public string DefenderUserId { get; set; } = string.Empty;

    /// <summary>First win over this target by this attacker this week.</summary>
    public bool CountsForIndividual { get; set; }
    public int IndividualPoints { get; set; }

    /// <summary>The attacker's family at battle time, if any.</summary>
    public int? FamilyId { get; set; }

    /// <summary>First win over this target by anyone in <see cref="FamilyId"/> this week.</summary>
    public bool CountsForFamily { get; set; }
}
