using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for calculating rehearsal statistics
/// Extracted from Rehearsals.razor to improve separation of concerns
/// </summary>
public interface IRehearsalStatisticsService
{
    /// <summary>
    /// Calculates primary and other instrument counts for a rehearsal's attendances
    /// </summary>
    /// <param name="rehearsal">The rehearsal (must have attendances populated)</param>
    /// <param name="memberInstruments">Dictionary mapping userId to their list of instruments</param>
    /// <returns>Tuple of (primaryInstrumentCounts, otherInstrumentCounts)</returns>
    (Dictionary<InstrumentType, int> primaryInstrumentCounts, Dictionary<InstrumentType, int> otherInstrumentCounts)
        CalculateInstrumentCounts(Rehearsal rehearsal, Dictionary<string, List<MemberInstrument>> memberInstruments);
}
