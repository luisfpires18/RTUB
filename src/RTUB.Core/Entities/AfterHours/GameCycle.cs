using RTUB.Core.Enums.AfterHours;

namespace RTUB.Core.Entities.AfterHours;

/// <summary>
/// One playable After Hours period. Linked to an RTUB <see cref="Entities.FiscalYear"/> for its
/// academic-year identity, but its boundaries are its own: exact UTC instants, never derived
/// from the fiscal year or the server clock's local date. A fiscal year may hold several cycles
/// (e.g. a Pilot and a Live one); history is kept, cycles are finished, not deleted.
/// </summary>
public class GameCycle : BaseEntity
{
    public int FiscalYearId { get; set; }
    public FiscalYear? FiscalYear { get; set; }

    public GameCycleKind Kind { get; set; }
    public GameCycleStatus Status { get; set; }

    /// <summary>Inclusive start, UTC.</summary>
    public DateTime StartUtc { get; set; }

    /// <summary>Exclusive end, UTC. Always after <see cref="StartUtc"/>.</summary>
    public DateTime EndUtc { get; set; }

    // For EF Core
    public GameCycle() { }

    /// <summary>
    /// Creates a <see cref="GameCycleStatus.Scheduled"/> cycle. Both boundaries must be UTC and
    /// the end must be after the start.
    /// </summary>
    public static GameCycle Create(int fiscalYearId, GameCycleKind kind, DateTime startUtc, DateTime endUtc)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cycle kind");
        if (startUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Start must be a UTC instant", nameof(startUtc));
        if (endUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("End must be a UTC instant", nameof(endUtc));
        if (endUtc <= startUtc)
            throw new ArgumentException("End must be after start", nameof(endUtc));

        return new GameCycle
        {
            FiscalYearId = fiscalYearId,
            Kind = kind,
            Status = GameCycleStatus.Scheduled,
            StartUtc = startUtc,
            EndUtc = endUtc
        };
    }

    /// <summary>Active and inside its boundaries at <paramref name="utcNow"/>.</summary>
    public bool IsPlayableAt(DateTime utcNow) =>
        Status == GameCycleStatus.Active && StartUtc <= utcNow && utcNow < EndUtc;
}
