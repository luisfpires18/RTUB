namespace RTUB.Core.Helpers.AfterHours;

/// <summary>
/// Cycle rollover and yearbook rules.
/// Annual Live boundary: 1 September 00:00 Europe/Lisbon (the academic year).
/// <b>AH-009 choices</b> (the manual defines no tie handling): every player or family tied for the highest
/// <b>positive</b> score in an official (Live) archive is champion; a cycle where everyone scored 0 has no
/// champion; a Pilot never has one.
/// </summary>
public static class RolloverRules
{
    /// <summary>1 September of <paramref name="year"/>, 00:00 in Lisbon, as a UTC instant.</summary>
    public static DateTime SeptemberStartUtc(int year) => LisbonCalendar.NextMidnightUtc(new DateOnly(year, 8, 31));

    public static bool IsChampion(bool official, int rank, int annualScore) => official && rank == 1 && annualScore > 0;
}
