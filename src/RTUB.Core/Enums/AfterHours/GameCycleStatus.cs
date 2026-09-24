namespace RTUB.Core.Enums.AfterHours;

/// <summary>
/// Lifecycle of an After Hours cycle: Scheduled -> Active -> Finished.
/// At most one cycle is Active at a time (enforced by a filtered unique index).
/// Stored as its integer value; do not renumber.
/// </summary>
public enum GameCycleStatus
{
    Scheduled = 1,
    Active = 2,
    Finished = 3
}
