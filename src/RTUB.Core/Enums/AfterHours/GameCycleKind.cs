namespace RTUB.Core.Enums.AfterHours;

/// <summary>
/// Whether an After Hours cycle is a trial run or the real academic-year game.
/// Stored as its integer value; do not renumber.
/// </summary>
public enum GameCycleKind
{
    Pilot = 1,
    Live = 2
}
