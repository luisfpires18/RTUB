using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Interfaces;

/// <summary>
/// Service for calculating enrollment statistics (instrument counts)
/// Extracted from EventEnrollments.razor to improve separation of concerns
/// </summary>
public interface IEnrollmentStatisticsService
{
    /// <summary>
    /// Calculates primary and other instrument counts for an event's enrollments
    /// </summary>
    /// <param name="eventItem">The event (must have Enrollments navigation property populated)</param>
    /// <param name="memberInstruments">Dictionary mapping userId to their list of instruments (required by Event entity methods)</param>
    /// <returns>Tuple of (primaryInstrumentCounts, otherInstrumentCounts)</returns>
    (Dictionary<InstrumentType, int> primaryInstrumentCounts, Dictionary<InstrumentType, int> otherInstrumentCounts) CalculateInstrumentCounts(
        Event eventItem,
        Dictionary<string, List<MemberInstrument>> memberInstruments);
}
