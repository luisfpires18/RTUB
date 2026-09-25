using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;
using RTUB.Core.Helpers.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

/// <summary>An objective with this player's (or family's) stored progress; 0 and not completed when no row exists yet.</summary>
public sealed record ObjectiveView(ObjectiveDefinition Definition, long Progress, long Target, bool Completed, long XpAwarded, int PointsAwarded);

public sealed record FamilyObjectivesView(int FamilyId, string FamilyName, IReadOnlyList<ObjectiveView> Objectives, int WeeklyScore);

public sealed record ObjectivesOverview(
    DateOnly LisbonDay,
    DateTime NextDailyResetUtc,
    GameWeek Week,
    IReadOnlyList<ObjectiveView> Daily,
    IReadOnlyList<ObjectiveView> Weekly,
    IReadOnlyDictionary<ObjectiveCategory, int> CategoryScores,
    int WeeklyScore,
    FamilyObjectivesView? Family);

/// <summary>One championship row. Rank is shared by equal scores; nobody is declared champion here.</summary>
public sealed record StandingRow(int Rank, int Id, string Name, int AnnualScore, int CurrentWeekScore, int ScoringWeeks,
    IReadOnlyList<int> CountedWeeks, bool IsMine);

public sealed record Leaderboards(int CurrentWeek, IReadOnlyList<StandingRow> Individual, IReadOnlyList<StandingRow> Families);

/// <summary>Objective and championship reads, from stored progress and awarded points only.</summary>
public interface IObjectiveService
{
    /// <summary>Null when no cycle is active.</summary>
    Task<ObjectivesOverview?> GetOverviewAsync(string userId);

    /// <summary>Null when no cycle is active.</summary>
    Task<Leaderboards?> GetLeaderboardsAsync(string userId);
}
