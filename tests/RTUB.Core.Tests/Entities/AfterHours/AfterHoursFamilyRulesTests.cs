using FluentAssertions;
using RTUB.Core.Helpers.AfterHours;
using Xunit;

namespace RTUB.Core.Tests.Entities.AfterHours;

/// <summary>AH-007 family rules that need no database.</summary>
public class AfterHoursFamilyRulesTests
{
    [Fact]
    public void Constants_MatchTheManualAndAh007Defaults()
    {
        (FamilyRules.MaxActiveMembers, FamilyRules.CreateLevel, FamilyRules.CreateCost, FamilyRules.LeaveCooldown)
            .Should().Be((4, 5, 1_500L, TimeSpan.FromHours(72)));
        (FamilyRules.NameMinLength, FamilyRules.NameMaxLength, FamilyRules.MottoMaxLength).Should().Be((3, 24, 120));
    }

    [Theory]
    [InlineData(null, "Enter a family name.")]
    [InlineData("   ", "Enter a family name.")]
    [InlineData("ab", "Family names are 3–24 characters.")]
    [InlineData("  ab  ", "Family names are 3–24 characters.")]
    [InlineData("abcdefghijklmnopqrstuvwxy", "Family names are 3–24 characters.")]
    public void Name_IsRequiredAndBounded(string? name, string error) =>
        FamilyRules.ValidateName(name, out _).Should().Be(error);

    [Fact]
    public void Name_IsTrimmed_AndNormalizedCaseInsensitively()
    {
        FamilyRules.ValidateName("  The Crew ", out var trimmed).Should().BeNull();
        trimmed.Should().Be("The Crew");
        FamilyRules.ValidateName("abcdefghijklmnopqrstuvwx", out _).Should().BeNull("24 characters is allowed");
        FamilyRules.NormalizeName("  The Crew ").Should().Be(FamilyRules.NormalizeName("the CREW"));
    }

    [Fact]
    public void Motto_IsOptionalTrimmedAndBounded()
    {
        FamilyRules.ValidateMotto("   ", out var empty).Should().BeNull();
        empty.Should().BeNull();
        FamilyRules.ValidateMotto("  Loyal to the night  ", out var motto).Should().BeNull();
        motto.Should().Be("Loyal to the night");
        FamilyRules.ValidateMotto(new string('m', 120), out _).Should().BeNull();
        FamilyRules.ValidateMotto(new string('m', 121), out _).Should().NotBeNull();
    }

    [Fact]
    public void LeaveCooldown_EndsExactlyAt72Hours()
    {
        var left = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        FamilyRules.InLeaveCooldown(null, left).Should().BeFalse("never left a family");
        FamilyRules.InLeaveCooldown(left, left.AddHours(72).AddTicks(-1)).Should().BeTrue();
        FamilyRules.InLeaveCooldown(left, left.AddHours(72)).Should().BeFalse();
        FamilyRules.JoinAllowedFromUtc(left).Should().Be(left.AddHours(72));
    }

    [Fact]
    public void RequestFingerprints_AreStable_AndChangeWithTheRequest()
    {
        FamilyRules.CreateRequest(" The Crew ", "motto").Should().Be(FamilyRules.CreateRequest("the crew", " motto "));
        FamilyRules.CreateRequest("The Crew", "motto").Should().NotBe(FamilyRules.CreateRequest("The Crew", "other"));
        FamilyRules.CreateRequest(new string('x', 24), new string('m', 120)).Length.Should().BeLessThanOrEqualTo(64);
    }
}
