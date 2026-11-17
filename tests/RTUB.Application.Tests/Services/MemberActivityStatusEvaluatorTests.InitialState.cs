// MemberActivityStatusEvaluator: initial state.
using FluentAssertions;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

public partial class MemberActivityStatusEvaluatorTests
{
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
}
