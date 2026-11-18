// MemberActivityStatusEvaluator: active members.
using FluentAssertions;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

public partial class MemberActivityStatusEvaluatorTests
{
    [Fact]
    public void NewLeitao_NoActivity_BeforeTheThreshold_StaysActive()
    {
        var result = MemberActivityStatusEvaluator.Evaluate(false, Array.Empty<DateTime>(), LeitaoStart, new DateTime(2026, 7, 14, 23, 59, 59));

        result.IsRetired.Should().BeFalse();
        result.RetirementThreshold.Should().Be(new DateTime(2026, 7, 15));
    }

    [Fact]
    public void NewLeitao_NoActivity_AtTheThreshold_IsReformed()
    {
        var result = MemberActivityStatusEvaluator.Evaluate(false, Array.Empty<DateTime>(), LeitaoStart, new DateTime(2026, 7, 15));

        result.IsRetired.Should().BeTrue();
        result.Reason.Should().Be(MemberStatusTransition.SixMonthsWithoutActivity);
    }

    [Fact]
    public void NewLeitao_ActivityBeforeTheThreshold_MovesTheThresholdToSixMonthsAfterIt()
    {
        var result = MemberActivityStatusEvaluator.Evaluate(false, new[] { D(2026, 5, 3) }, LeitaoStart, new DateTime(2026, 7, 20));

        result.IsRetired.Should().BeFalse();
        result.RetirementThreshold.Should().Be(D(2026, 11, 3));
    }

    [Fact]
    public void ActiveSince_IsTheLatestAdmissionMilestone_JanuaryWhenTheMonthIsMissing()
    {
        MemberActivityStatusEvaluator.ActiveSince(new ApplicationUser { YearLeitao = 2024, MonthLeitao = 10, YearCaloiro = 2026, MonthCaloiro = 5 })
            .Should().Be(new DateTime(2026, 5, 1));
        MemberActivityStatusEvaluator.ActiveSince(new ApplicationUser { YearTuno = 2025 }).Should().Be(new DateTime(2025, 1, 1));
        MemberActivityStatusEvaluator.ActiveSince(new ApplicationUser()).Should().BeNull();
    }

    // ---------- Active -> Reformado ----------

    [Fact]
    public void Active_ActivityOneMonthAgo_StaysActive()
    {
        var result = Active(D(2026, 9, 13));

        result.IsRetired.Should().BeFalse();
        result.Changed.Should().BeFalse();
        result.RetirementThreshold.Should().Be(D(2027, 3, 13));
        result.MonthsUntilRetirement.Should().Be(5);
    }

    [Fact]
    public void Active_ActivityJustBeforeTheSixMonthThreshold_StaysActive()
    {
        var result = Active(Now.AddMonths(-6).AddSeconds(1));

        result.IsRetired.Should().BeFalse();
        result.MonthsUntilRetirement.Should().Be(1, "the final partial month still counts as 1 until the threshold");
    }

    [Theory]
    [InlineData(2026, 5, 5, 1)]   // Calhau: threshold 05 Nov, less than one full month left
    [InlineData(2026, 5, 29, 1)]  // Prepúcio: threshold 29 Nov, one month and a half left
    [InlineData(2026, 6, 14, 2)]  // threshold 14 Dec 20:00, two full months (and hours) left
    [InlineData(2026, 8, 14, 4)]  // threshold 14 Feb 20:00
    [InlineData(2026, 10, 1, 5)]  // threshold 01 Apr: five full months and a half
    public void Countdown_IsWholeMonths_WithTheFinalPartialMonthCountedAsOne(int year, int month, int day, int expected)
    {
        var result = MemberActivityStatusEvaluator.Evaluate(false, new[] { D(year, month, day) }, null, HotfixNow);

        result.IsRetired.Should().BeFalse();
        result.MonthsUntilRetirement.Should().Be(expected);
    }

    [Fact]
    public void Countdown_ExactlyOneMonthLeft_IsOne()
    {
        var threshold = HotfixNow.AddMonths(1);
        var result = MemberActivityStatusEvaluator.Evaluate(false, new[] { threshold.AddMonths(-6) }, null, HotfixNow);

        result.RetirementThreshold.Should().Be(threshold);
        result.MonthsUntilRetirement.Should().Be(1);
    }

    [Fact]
    public void Countdown_OneSecondBeforeTheThreshold_IsStillOne_AndAtTheThreshold_IsReformed()
    {
        var last = D(2026, 5, 5);
        var threshold = last.AddMonths(6);

        MemberActivityStatusEvaluator.Evaluate(false, new[] { last }, null, threshold.AddSeconds(-1)).MonthsUntilRetirement.Should().Be(1);

        var reached = MemberActivityStatusEvaluator.Evaluate(false, new[] { last }, null, threshold);
        reached.IsRetired.Should().BeTrue();
        reached.Reason.Should().Be(MemberStatusTransition.SixMonthsWithoutActivity);
        reached.MonthsUntilRetirement.Should().BeNull();
    }

    [Fact]
    public void Active_ExactlySixMonths_IsReformed_BecauseTheComparisonIsNowGreaterOrEqualThreshold()
    {
        var result = Active(Now.AddMonths(-6));

        result.IsRetired.Should().BeTrue();
        result.Changed.Should().BeTrue();
        result.Reason.Should().Be(MemberStatusTransition.SixMonthsWithoutActivity);
        result.RetirementThreshold.Should().Be(Now);
    }

    [Fact]
    public void Active_ImmediatelyAfterTheThreshold_IsReformed() =>
        Active(Now.AddMonths(-6).AddSeconds(-1)).IsRetired.Should().BeTrue();

    [Fact]
    public void Active_VeryOldActivity_IsReformed()
    {
        var result = Active(D(2020, 2, 1), D(2021, 5, 3));

        result.IsRetired.Should().BeTrue();
        result.LastActivity.Should().Be(D(2021, 5, 3));
        result.RetirementThreshold.Should().Be(D(2021, 11, 3));
    }

    [Fact]
    public void Active_SixMonthsAreCalendarMonths_NotOneHundredEightyDays()
    {
        // 31 Aug + 6 months = 28 Feb (2027 is not a leap year); 180 days would be 27 Feb.
        var lastDay = new DateTime(2026, 8, 31, 12, 0, 0);
        MemberActivityStatusEvaluator.Evaluate(false, new[] { lastDay }, null, new DateTime(2027, 2, 27, 23, 0, 0)).IsRetired.Should().BeFalse();
        MemberActivityStatusEvaluator.Evaluate(false, new[] { lastDay }, null, new DateTime(2027, 2, 28, 12, 0, 0)).IsRetired.Should().BeTrue();
    }

    [Fact]
    public void Active_NoActivityAndNoAdmissionDate_DoesNotStayActiveForever()
    {
        var result = Active();

        result.IsRetired.Should().BeTrue();
        result.Reason.Should().Be(MemberStatusTransition.NoQualifyingActivity);
    }

    [Fact]
    public void Active_NoActivity_SixMonthsRunFromTheAdmissionDate()
    {
        // Promoted (or admitted) two months ago with an empty attendance table: still active, four months left.
        var recent = MemberActivityStatusEvaluator.Evaluate(false, Array.Empty<DateTime>(), new DateTime(2026, 8, 1), Now);
        recent.IsRetired.Should().BeFalse();
        recent.RetirementThreshold.Should().Be(new DateTime(2027, 2, 1));

        // An old record with no activity at all: reformed.
        var old = MemberActivityStatusEvaluator.Evaluate(false, Array.Empty<DateTime>(), new DateTime(2019, 1, 1), Now);
        old.IsRetired.Should().BeTrue();
        old.Reason.Should().Be(MemberStatusTransition.SixMonthsWithoutActivity);
    }

    [Fact]
    public void Active_TheLaterOfLastActivityAndAdmission_StartsTheSixMonths() =>
        MemberActivityStatusEvaluator.Evaluate(false, new[] { D(2025, 1, 10) }, new DateTime(2026, 6, 1), Now)
            .RetirementThreshold.Should().Be(new DateTime(2026, 12, 1));

    [Fact]
    public void FutureActivity_NeverCounts() =>
        Active(Now.AddDays(1)).IsRetired.Should().BeTrue();
}
