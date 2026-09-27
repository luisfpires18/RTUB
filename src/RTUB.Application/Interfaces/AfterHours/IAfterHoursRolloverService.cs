namespace RTUB.Application.Interfaces.AfterHours;

/// <summary>What a rollover did. A repeated equivalent call returns the same ids with <see cref="Replayed"/> set.</summary>
public sealed record RolloverResult(int ArchiveId, int SourceCycleId, int TargetCycleId, bool Replayed);

/// <summary>
/// Server-side cycle transitions: archive the source into the yearbook, finish it and activate the next
/// cycle, all in one write transaction. No page or endpoint calls this yet; AH-010 binds it to owner/admin
/// tooling. Refusals throw <see cref="InvalidOperationException"/> (or <see cref="ArgumentException"/> for
/// invalid boundaries) and change nothing.
/// </summary>
public interface IAfterHoursRolloverService
{
    /// <summary>
    /// Annual Live → Live rollover. Needs an Active Live cycle whose end has passed, and exactly one RTUB
    /// fiscal year starting where the source's fiscal year ends. The new Live cycle runs from 1 September
    /// 00:00 Lisbon of that fiscal year's start year to 1 September of its end year.
    /// </summary>
    Task<RolloverResult> RolloverLiveAsync(int sourceCycleId);

    /// <summary>
    /// Pilot → Live launch. The Pilot may be finished before its end (pilot length is administrative).
    /// The Live cycle's fiscal year and exact UTC boundaries are given explicitly.
    /// </summary>
    Task<RolloverResult> TransitionPilotToLiveAsync(int pilotCycleId, int targetFiscalYearId, DateTime targetStartUtc, DateTime targetEndUtc);
}
