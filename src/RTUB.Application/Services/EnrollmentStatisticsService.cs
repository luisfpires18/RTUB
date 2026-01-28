using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Services;

/// <summary>
/// Service for calculating enrollment statistics (instrument counts)
/// Extracted from EventEnrollments.razor to improve separation of concerns
/// </summary>
public class EnrollmentStatisticsService : IEnrollmentStatisticsService
{
    public (Dictionary<InstrumentType, int> primaryInstrumentCounts, Dictionary<InstrumentType, int> otherInstrumentCounts) CalculateInstrumentCounts(
        Event eventItem,
        Dictionary<string, List<MemberInstrument>> memberInstruments)
    {
        // Use the Event entity's methods for calculation
        // Note: The memberInstruments parameter is required by the method signature but may not be used
        var primaryInstrumentCounts = eventItem.GetPrimaryInstrumentCounts(memberInstruments);
        var otherInstrumentCounts = eventItem.GetOtherInstrumentCounts(memberInstruments);

        return (primaryInstrumentCounts, otherInstrumentCounts);
    }
}
