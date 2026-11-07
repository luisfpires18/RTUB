using FluentAssertions;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Task 076: which member-information fields apply, by category (the member form's existing rules), so the Membros queue
/// never asks for something a member cannot have: a Leitão no Caloiro / Tuno dates, Fundador and Tuno Honorário no
/// Leitão / Caloiro dates or padrinho, a Fundador no Tuno date (always December 1991), a Tuno Honorário no instrument.
/// </summary>
public class MemberInfoRulesTests
{
    [Theory]
    [InlineData("Leitao", "city phone instrument leitao mentor")]
    [InlineData("Caloiro", "city phone instrument leitao caloiro mentor")]
    [InlineData("Tuno", "city phone instrument leitao caloiro tuno mentor")]
    [InlineData("Tuno Fundador", "city phone instrument")]
    [InlineData("Tuno TunoHonorario", "city phone tuno")]
    public void Applicable_FollowsTheCategory(string categories, string expected)
    {
        MemberInfoRules.Applicable(Parse(categories)).Should().Equal(expected.Split(' '));
    }

    [Fact]
    public void Missing_IsOnlyWhatAppliesAndIsEmpty()
    {
        var leitao = new ApplicationUser { Categories = [MemberCategory.Leitao], City = "Braga", PhoneNumber = "912", YearLeitao = null, MentorId = null };
        var fundador = new ApplicationUser { Categories = [MemberCategory.Tuno, MemberCategory.Fundador], City = "", PhoneNumber = "912", YearTuno = 1991 };
        var complete = new ApplicationUser
        {
            Categories = [MemberCategory.Tuno], City = "Braga", PhoneNumber = "912", YearLeitao = 2015, YearCaloiro = 2016, YearTuno = 2017, MentorId = "x",
        };

        MemberInfoRules.Missing(leitao, hasInstrument: false).Should().Equal("instrument", "leitao", "mentor");
        MemberInfoRules.Missing(fundador, hasInstrument: true).Should().Equal("city");
        MemberInfoRules.Missing(complete, hasInstrument: true).Should().BeEmpty("a year-only date counts as filled");
    }

    [Fact]
    public void InScope_LeavesOutTheExpelledAndAccountsWithoutAMembershipCategory()
    {
        MemberInfoRules.InScope(new ApplicationUser { Categories = [MemberCategory.Caloiro] }).Should().BeTrue();
        MemberInfoRules.InScope(new ApplicationUser { Categories = [MemberCategory.Caloiro], IsExpelled = true }).Should().BeFalse();
        MemberInfoRules.InScope(new ApplicationUser { Categories = [MemberCategory.TunoHonorario] }).Should().BeFalse();
        MemberInfoRules.InScope(new ApplicationUser { Categories = [] }).Should().BeFalse();
    }

    private static List<MemberCategory> Parse(string categories) =>
        categories.Split(' ').Select(Enum.Parse<MemberCategory>).ToList();
}
