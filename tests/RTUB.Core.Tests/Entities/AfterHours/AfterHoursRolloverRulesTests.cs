using FluentAssertions;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Core.Tests.Entities.AfterHours;

/// <summary>AH-009 rollover and champion rules. The Lisbon zone is resolved by IANA id, never the machine's local zone.</summary>
public class AfterHoursRolloverRulesTests
{
    [Theory]
    [InlineData(2026, "2026-08-31T23:00:00Z")]
    [InlineData(2027, "2027-08-31T23:00:00Z")]
    [InlineData(2030, "2030-08-31T23:00:00Z")]
    public void SeptemberStart_IsLisbonMidnight_InSummerTime(int year, string expected)
    {
        var start = RolloverRules.SeptemberStartUtc(year);

        start.Should().Be(DateTime.Parse(expected, null, System.Globalization.DateTimeStyles.AdjustToUniversal));
        start.Kind.Should().Be(DateTimeKind.Utc);
        LisbonCalendar.DateOf(start).Should().Be(new DateOnly(year, 9, 1));
        LisbonCalendar.DateOf(start.AddTicks(-1)).Should().Be(new DateOnly(year, 8, 31));
    }

    [Fact]
    public void ConsecutiveSeptemberStarts_SpanOneLisbonYear()
    {
        var span = RolloverRules.SeptemberStartUtc(2027) - RolloverRules.SeptemberStartUtc(2026);

        span.Should().Be(TimeSpan.FromDays(365), "both boundaries fall in summer time, so the UTC offset is the same");
        (RolloverRules.SeptemberStartUtc(2028) - RolloverRules.SeptemberStartUtc(2027)).Should().Be(TimeSpan.FromDays(366), "2028 is a leap year");
    }

    [Theory]
    [InlineData(true, 1, 100, true)]
    [InlineData(true, 1, 1, true)]
    [InlineData(true, 1, 0, false)]  // everyone on 0: no champion
    [InlineData(true, 2, 100, false)]
    [InlineData(false, 1, 100, false)] // Pilot: never
    public void Champion_IsTopRank_PositiveScore_OfficialOnly(bool official, int rank, int score, bool champion) =>
        RolloverRules.IsChampion(official, rank, score).Should().Be(champion);
}
