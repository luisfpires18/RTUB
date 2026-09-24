using RTUB.Core.Entities.AfterHours;
using RTUB.Core.Enums.AfterHours;

namespace RTUB.Application.Interfaces.AfterHours;

/// <summary>
/// After Hours cycle records. No automatic scheduling or rollover: every transition is an
/// explicit call.
/// </summary>
public interface IGameCycleService
{
    /// <summary>
    /// The cycle that is Active and inside its UTC boundaries now, with its FiscalYear, or null
    /// when there is none. Never creates a cycle.
    /// </summary>
    Task<GameCycle?> GetActiveCycleAsync();

    /// <summary>Creates a Scheduled cycle. Throws ArgumentException for invalid boundaries.</summary>
    Task<GameCycle> CreateCycleAsync(int fiscalYearId, GameCycleKind kind, DateTime startUtc, DateTime endUtc);

    /// <summary>
    /// Scheduled -> Active. Throws InvalidOperationException if the cycle is not Scheduled or
    /// another cycle is already Active.
    /// </summary>
    Task ActivateAsync(int cycleId);

    /// <summary>Active -> Finished. Throws InvalidOperationException if the cycle is not Active.</summary>
    Task FinishAsync(int cycleId);
}
