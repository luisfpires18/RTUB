using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Helpers;
using Xunit;

namespace RTUB.Core.Tests.Helpers;

/// <summary>
/// #226 F02: Portugal's calendar and clock whatever the server's zone (Azure runs on UTC). Summer (WEST) is UTC+1,
/// winter (WET) is UTC+0; in 2026 the clocks go forward on 29 March at 01:00 UTC and back on 25 October at 01:00 UTC.
/// </summary>
public class PortugalTimeTests
{
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static Clock At(int y, int mo, int d, int h, int mi) => new(new DateTimeOffset(y, mo, d, h, mi, 0, TimeSpan.Zero));

    private static DateTime Utc(int y, int mo, int d, int h, int mi) => new(y, mo, d, h, mi, 0, DateTimeKind.Utc);

    [Fact]
    public void Summer_2330Utc_IsAlreadyTomorrow_0030InPortugal()
    {
        var clock = At(2026, 7, 14, 23, 30);
        PortugalTime.Today(clock).Should().Be(new DateTime(2026, 7, 15));
        PortugalTime.Now(clock).Should().Be(new DateTime(2026, 7, 15, 0, 30, 0));
    }

    [Fact]
    public void Winter_UtcAndPortugalAgree()
    {
        var clock = At(2026, 1, 14, 23, 30);
        PortugalTime.Today(clock).Should().Be(new DateTime(2026, 1, 14));
        PortugalTime.Now(clock).Should().Be(new DateTime(2026, 1, 14, 23, 30, 0));
    }

    [Fact]
    public void NextUtc_SummerTime_IsOneHourEarlierInUtc()
    {
        PortugalTime.NextUtc(At(2026, 7, 14, 8, 0), new TimeSpan(10, 30, 0)).Should().Be(Utc(2026, 7, 14, 9, 30));
        PortugalTime.NextUtc(At(2026, 7, 14, 9, 30), new TimeSpan(10, 30, 0)).Should().Be(Utc(2026, 7, 15, 9, 30), "exactly now has passed");
    }

    [Fact]
    public void NextUtc_WinterTime_MatchesUtc()
    {
        PortugalTime.NextUtc(At(2026, 1, 14, 8, 0), new TimeSpan(10, 30, 0)).Should().Be(Utc(2026, 1, 14, 10, 30));
        PortugalTime.NextUtc(At(2026, 1, 14, 11, 0), new TimeSpan(10, 30, 0)).Should().Be(Utc(2026, 1, 15, 10, 30));
    }

    [Fact]
    public void NextUtc_MidnightJob_InSummer_Runs2300UtcTheDayBefore()
    {
        PortugalTime.NextUtc(At(2026, 7, 14, 12, 0), TimeSpan.Zero).Should().Be(Utc(2026, 7, 14, 23, 0));
        PortugalTime.NextUtc(At(2026, 7, 14, 23, 30), TimeSpan.Zero).Should().Be(Utc(2026, 7, 15, 23, 0), "00:30 in Portugal: today's midnight is gone");
    }

    [Fact]
    public void NextUtc_AcrossTheSpringChange_KeepsTheLocalClockTime()
    {
        // Saturday 28 March 2026 12:00 UTC (winter); the 10:00 job of Sunday is already in summer time.
        PortugalTime.NextUtc(At(2026, 3, 28, 12, 0), new TimeSpan(10, 0, 0)).Should().Be(Utc(2026, 3, 29, 9, 0));
    }

    [Fact]
    public void NextUtc_ATimeTheSpringChangeSkips_RunsOnceAnHourLater()
    {
        // 01:30 does not exist in Portugal on 29 March 2026 (01:00 jumps to 02:00): it runs at 02:30 WEST = 01:30 UTC.
        PortugalTime.NextUtc(At(2026, 3, 29, 0, 0), new TimeSpan(1, 30, 0)).Should().Be(Utc(2026, 3, 29, 1, 30));
        PortugalTime.NextUtc(At(2026, 3, 29, 1, 30), new TimeSpan(1, 30, 0)).Should().Be(Utc(2026, 3, 30, 0, 30), "the next day is summer time");
    }

    [Fact]
    public void NextUtc_ATimeTheAutumnChangeRepeats_RunsOnlyAtItsFirstOccurrence()
    {
        // 01:30 happens twice in Portugal on 25 October 2026: 01:30 WEST (00:30 UTC), then 01:30 WET (01:30 UTC).
        PortugalTime.NextUtc(At(2026, 10, 25, 0, 0), new TimeSpan(1, 30, 0)).Should().Be(Utc(2026, 10, 25, 0, 30));
        PortugalTime.NextUtc(At(2026, 10, 25, 0, 45), new TimeSpan(1, 30, 0)).Should().Be(Utc(2026, 10, 26, 1, 30),
            "between the two 01:30s the job already ran: no second run that day");
    }

    [Fact]
    public void NextUtc_AcrossTheAutumnChange_KeepsTheLocalClockTime()
    {
        PortugalTime.NextUtc(At(2026, 10, 24, 12, 0), new TimeSpan(10, 0, 0)).Should().Be(Utc(2026, 10, 25, 10, 0), "winter: 10:00 = 10:00 UTC");
    }

    [Fact]
    public void NextUtc_Weekly_IsTheNextMondayInPortugal()
    {
        // Sunday 12 July 2026 23:30 UTC is Monday 00:30 in Portugal: Monday's 12:00 is still ahead, at 11:00 UTC.
        PortugalTime.NextUtc(At(2026, 7, 12, 23, 30), new TimeSpan(12, 0, 0), DayOfWeek.Monday).Should().Be(Utc(2026, 7, 13, 11, 0));
        PortugalTime.NextUtc(At(2026, 7, 13, 11, 0), new TimeSpan(12, 0, 0), DayOfWeek.Monday).Should().Be(Utc(2026, 7, 20, 11, 0));
        PortugalTime.NextUtc(At(2026, 1, 14, 9, 0), new TimeSpan(12, 0, 0), DayOfWeek.Monday).Should().Be(Utc(2026, 1, 19, 12, 0));
    }

    [Fact]
    public void Age_TurnsOnThePortugueseBirthday()
    {
        var user = new ApplicationUser { DateOfBirth = new DateTime(2000, 7, 15) };
        user.AgeOn(PortugalTime.Today(At(2026, 7, 14, 22, 59))).Should().Be(25, "still 14 July in Portugal");
        user.AgeOn(PortugalTime.Today(At(2026, 7, 14, 23, 30))).Should().Be(26, "already 15 July in Portugal (the server's UTC date is still the 14th)");
        user.AgeOn(PortugalTime.Today(At(2026, 1, 14, 23, 30))).Should().Be(25);
        new ApplicationUser().AgeOn(new DateTime(2026, 1, 1)).Should().BeNull();
    }
}
