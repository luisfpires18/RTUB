using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for calculating rehearsal statistics
/// Extracted from Rehearsals.razor to improve separation of concerns
/// </summary>
public class RehearsalStatisticsService : IRehearsalStatisticsService
{
    /// <summary>
    /// Calculates primary and other instrument counts for a rehearsal's attendances
    /// </summary>
    /// <param name="rehearsal">The rehearsal (must have attendances populated)</param>
    /// <param name="memberInstruments">Dictionary mapping userId to their list of instruments</param>
    /// <returns>Tuple of (primaryInstrumentCounts, otherInstrumentCounts)</returns>
    public (Dictionary<InstrumentType, int> primaryInstrumentCounts, Dictionary<InstrumentType, int> otherInstrumentCounts)
        CalculateInstrumentCounts(Rehearsal rehearsal, Dictionary<string, List<MemberInstrument>> memberInstruments)
    {
        // Use the Rehearsal entity methods for calculation
        var primaryInstrumentCounts = rehearsal.GetPrimaryInstrumentCounts(memberInstruments);
        var otherInstrumentCounts = rehearsal.GetOtherInstrumentCounts(memberInstruments);

        return (primaryInstrumentCounts, otherInstrumentCounts);
    }
}
