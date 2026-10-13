namespace RTUB.Application.DTOs;

/// <summary>
/// Counts of one reconciliation run. <c>Evaluated</c> = members the activity rule applies to and that were processed;
/// <c>Excluded</c> = users who are only Tuno Honorário or have no category (projection refreshed, flag untouched); <c>Failed</c> = members whose
/// evaluation threw (logged with their id; the run went on).
/// </summary>
public sealed record MemberStatusReconciliationResult(int Evaluated, int Activated, int Reformed, int Unchanged, int Excluded, int Failed)
{
    public static readonly MemberStatusReconciliationResult Empty = new(0, 0, 0, 0, 0, 0);

    public int Changed => Activated + Reformed;
}
