using FluentAssertions;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>The member activity rules (task 044B), pure: a fixed "now", no database.</summary>
public class MemberActivityStatusEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime D(int year, int month, int day) => new(year, month, day, 20, 0, 0);

    private static MemberActivityEvaluation Active(params DateTime[] activity) =>
        MemberActivityStatusEvaluator.Evaluate(false, activity, null, Now);

    private static MemberActivityEvaluation Reformed(DateTime now, params DateTime[] activity) =>
        MemberActivityStatusEvaluator.Evaluate(true, activity, null, now);

    // ---------- Initial state ----------

    [Fact]
    public void NewLeitao_StartsActive() =>
        MemberActivityStatusEvaluator.InitialIsRetired(new[] { MemberCategory.Leitao }).Should().BeFalse();

    [Theory]
    [InlineData(MemberCategory.Caloiro)]
    [InlineData(MemberCategory.Tuno)]
    [InlineData(MemberCategory.Veterano)]
    [InlineData(MemberCategory.Tunossauro)]
    [InlineData(MemberCategory.TunoHonorario)]
    [InlineData(MemberCategory.Fundador)]
    public void NewNonLeitao_StartsReformed(MemberCategory category) =>
        MemberActivityStatusEvaluator.InitialIsRetired(new[] { category }).Should().BeTrue();

    [Fact]
    public void MembershipCategories_AreSubjectToTheRule()
    {
        MemberActivityStatusEvaluator.IsSubjectToRule(new ApplicationUser { Categories = { MemberCategory.Leitao } }).Should().BeTrue();
        MemberActivityStatusEvaluator.IsSubjectToRule(new ApplicationUser { Categories = { MemberCategory.Caloiro } }).Should().BeTrue();
        MemberActivityStatusEvaluator.IsSubjectToRule(new ApplicationUser { Categories = { MemberCategory.Tuno, MemberCategory.Fundador } }).Should().BeTrue();
    }

    [Theory]
    [InlineData(new[] { MemberCategory.Tuno, MemberCategory.TunoHonorario })]
    [InlineData(new[] { MemberCategory.Veterano, MemberCategory.TunoHonorario })]
    [InlineData(new[] { MemberCategory.Leitao, MemberCategory.TunoHonorario })]
    [InlineData(new[] { MemberCategory.Caloiro, MemberCategory.TunoHonorario })]
    [InlineData(new[] { MemberCategory.Tunossauro, MemberCategory.TunoHonorario })]
    [InlineData(new[] { MemberCategory.Tuno })]
    [InlineData(new[] { MemberCategory.Veterano })]
    public void TunoHonorario_NeverTakesPrecedenceOverAMembershipCategory(MemberCategory[] categories) =>
        MemberActivityStatusEvaluator.IsSubjectToRule(new ApplicationUser { Categories = categories.ToList() }).Should().BeTrue();

    [Fact]
    public void OnlyTunoHonorario_IsExcluded() =>
        MemberActivityStatusEvaluator.IsSubjectToRule(new ApplicationUser { Categories = { MemberCategory.TunoHonorario } }).Should().BeFalse();

    [Fact]
    public void NoCategories_IsExcluded() =>
        MemberActivityStatusEvaluator.IsSubjectToRule(new ApplicationUser()).Should().BeFalse();

    [Fact]
    public void ApplyInitialState_NewLeitao_IsActive_WithTheCreationMonthAsLeitaoDate()
    {
        var leitao = new ApplicationUser { Categories = { MemberCategory.Leitao } };
        MemberActivityStatusEvaluator.ApplyInitialState(leitao, Now);

        leitao.IsRetired.Should().BeFalse();
        (leitao.YearLeitao, leitao.MonthLeitao).Should().Be((2026, 10));
        MemberActivityStatusEvaluator.ActiveSince(leitao).Should().Be(new DateTime(2026, 10, 1));
    }

    [Fact]
    public void ApplyInitialState_KeepsAGivenLeitaoDate_AndReformsOtherCategories()
    {
        var leitao = new ApplicationUser { Categories = { MemberCategory.Leitao }, YearLeitao = 2026, MonthLeitao = 3 };
        MemberActivityStatusEvaluator.ApplyInitialState(leitao, Now);
        (leitao.YearLeitao, leitao.MonthLeitao).Should().Be((2026, 3));

        var tuno = new ApplicationUser { Categories = { MemberCategory.Tuno } };
        MemberActivityStatusEvaluator.ApplyInitialState(tuno, Now);
        tuno.IsRetired.Should().BeTrue();
        tuno.YearLeitao.Should().BeNull();
    }

    // ---------- New Leitão: the six months run from the start date until there is activity ----------

    private static readonly DateTime LeitaoStart = new(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

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
        result.MonthsUntilRetirement.Should().Be(0);
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
