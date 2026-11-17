// MemberActivityStatusEvaluator: reformed members.
using FluentAssertions;
using RTUB.Application.Services;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class MemberActivityStatusEvaluatorTests
{
    // ---------- Reformado -> active ----------

    [Fact]
    public void Reformed_OneQualifyingMonth_StaysReformed()
    {
        var result = Reformed(Now, D(2026, 9, 2));

        result.IsRetired.Should().BeTrue();
        result.ConsecutiveActiveMonths.Should().Be(1);
        result.RetirementThreshold.Should().BeNull();
    }

    [Fact]
    public void Reformed_TwoConsecutiveMonths_StaysReformed() =>
        Reformed(Now, D(2026, 8, 2), D(2026, 9, 2)).ConsecutiveActiveMonths.Should().Be(2);

    [Fact]
    public void Reformed_ThreeConsecutiveMonths_BecomesActive()
    {
        var result = Reformed(Now, D(2026, 8, 2), D(2026, 9, 2), D(2026, 10, 1));

        result.IsRetired.Should().BeFalse();
        result.Changed.Should().BeTrue();
        result.Reason.Should().Be(MemberStatusTransition.ThreeConsecutiveActiveMonths);
        result.StreakMonths.Should().Equal(new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), new DateTime(2026, 10, 1));
        result.RetirementThreshold.Should().Be(D(2027, 4, 1));
    }

    [Fact]
    public void Reformed_ThreeCompletedMonths_WithNothingYetThisMonth_BecomesActive() =>
        Reformed(Now, D(2026, 7, 2), D(2026, 8, 2), D(2026, 9, 2)).IsRetired.Should().BeFalse();

    [Fact]
    public void Reformed_SeveralActivitiesInOneMonth_CountAsOneMonth() =>
        Reformed(Now, D(2026, 9, 2), D(2026, 9, 9), D(2026, 9, 16), D(2026, 10, 1)).ConsecutiveActiveMonths.Should().Be(2);

    [Theory]
    // Jan + Feb + Mar => active; Feb + Mar + Apr => active; Jan + Mar + Apr and Jan + Feb + Apr => still reformed.
    [InlineData(new[] { 1, 2, 3 }, 3, false)]
    [InlineData(new[] { 2, 3, 4 }, 4, false)]
    [InlineData(new[] { 1, 3, 4 }, 4, true)]
    [InlineData(new[] { 1, 2, 4 }, 4, true)]
    public void Reformed_OnlyThreeConsecutiveCurrentMonths_BringThemBack(int[] months, int nowMonth, bool stillReformed)
    {
        var now = new DateTime(2026, nowMonth, 20, 12, 0, 0, DateTimeKind.Utc);
        Reformed(now, months.Select(m => D(2026, m, 10)).ToArray()).IsRetired.Should().Be(stillReformed);
    }

    [Fact]
    public void Reformed_AnOldStreak_IsNotCurrent() =>
        Reformed(Now, D(2026, 6, 2), D(2026, 7, 2), D(2026, 8, 2)).IsRetired.Should().BeTrue();

    [Fact]
    public void Reformed_PreReformationHistoryPlusOneNewMonth_StaysReformed()
    {
        // Active through early 2025, reformed after six empty months, back once in October 2026.
        var result = Reformed(Now, D(2025, 1, 5), D(2025, 2, 5), D(2025, 3, 5), D(2025, 4, 5), D(2026, 10, 2));

        result.IsRetired.Should().BeTrue();
        result.ConsecutiveActiveMonths.Should().Be(1);
    }

    [Fact]
    public void SameInputs_SameResult_AndNoChangeOnceApplied()
    {
        var activity = new[] { D(2026, 8, 2), D(2026, 9, 2), D(2026, 10, 1) };
        var first = MemberActivityStatusEvaluator.Evaluate(true, activity, null, Now);
        var again = MemberActivityStatusEvaluator.Evaluate(first.IsRetired, activity, null, Now);

        first.Changed.Should().BeTrue();
        again.IsRetired.Should().Be(first.IsRetired);
        again.Changed.Should().BeFalse();
        again.Reason.Should().Be(MemberStatusTransition.None);
    }
}
