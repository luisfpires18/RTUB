using FluentAssertions;
using RTUB.Application.Extensions;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Extensions;

// ApplicationUserExtensionsTests: Month Aware Years And Primary Category tests.
public partial class ApplicationUserExtensionsTests
{
    [Fact]
    public void GetYearsAsTuno_WithYearAndMonth_ReturnsCorrectYears()
    {
        // Arrange
        var now = DateTime.Now;
        var twoYearsAgo = now.AddYears(-2).AddMonths(-6); // 2.5 years ago
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = twoYearsAgo.Year;
        user.MonthTuno = twoYearsAgo.Month;

        // Act
        var years = user.GetYearsAsTuno();

        // Assert - Should be 2 complete years (integer division)
        years.Should().Be(2);
    }

    [Fact]
    public void GetYearsAsTuno_WithYearAndNullMonth_DefaultsToJanuary()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var currentMonth = DateTime.Now.Month;
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = currentYear - 2;
        user.MonthTuno = null; // Should default to January (month 1)

        // Act
        var years = user.GetYearsAsTuno();

        // Assert - Calculates from January of that year
        var expectedMonths = ((currentYear - (currentYear - 2)) * 12) + (currentMonth - 1);
        years.Should().Be(expectedMonths / 12);
    }

    [Fact]
    public void GetYearsAsTuno_WithMonthInCurrentYear_ReturnsPartialYears()
    {
        // Arrange
        var now = DateTime.Now;
        var sixMonthsAgo = now.AddMonths(-6);
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = sixMonthsAgo.Year;
        user.MonthTuno = sixMonthsAgo.Month;

        // Act
        var years = user.GetYearsAsTuno();

        // Assert - Should be 0 complete years (integer division of ~6 months)
        years.Should().Be(0);
    }

    [Fact]
    public void QualifiesForVeterano_WithExactly2YearsInMonths_ReturnsTrue()
    {
        // Arrange
        var now = DateTime.Now;
        var twoYearsAgo = now.AddYears(-2);
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = twoYearsAgo.Year;
        user.MonthTuno = twoYearsAgo.Month;

        // Act & Assert
        user.QualifiesForVeterano().Should().BeTrue();
    }

    [Fact]
    public void QualifiesForVeterano_WithAlmost2Years_ReturnsFalse()
    {
        // Arrange
        var now = DateTime.Now;
        var almostTwoYears = now.AddYears(-2).AddMonths(1); // 1 month shy of 2 years
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = almostTwoYears.Year;
        user.MonthTuno = almostTwoYears.Month;

        // Act & Assert
        user.QualifiesForVeterano().Should().BeFalse();
    }

    [Fact]
    public void QualifiesForTunossauro_WithExactly6YearsInMonths_ReturnsTrue()
    {
        // Arrange
        var now = DateTime.Now;
        var sixYearsAgo = now.AddYears(-6);
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = sixYearsAgo.Year;
        user.MonthTuno = sixYearsAgo.Month;

        // Act & Assert
        user.QualifiesForTunossauro().Should().BeTrue();
    }

    [Fact]
    public void QualifiesForTunossauro_WithAlmost6Years_ReturnsFalse()
    {
        // Arrange
        var now = DateTime.Now;
        var almostSixYears = now.AddYears(-6).AddMonths(1); // 1 month shy of 6 years
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = almostSixYears.Year;
        user.MonthTuno = almostSixYears.Month;

        // Act & Assert
        user.QualifiesForTunossauro().Should().BeFalse();
    }

    [Fact]
    public void GetYearsAsTuno_BackwardCompatibility_WithOnlyYear_StillWorks()
    {
        // Arrange - User from old system with only year, no month
        var currentYear = DateTime.Now.Year;
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = currentYear - 3;
        user.MonthTuno = null;

        // Act
        var years = user.GetYearsAsTuno();

        // Assert - Should still calculate years correctly (defaulting to January)
        years.Should().NotBeNull();
        years.Should().BeGreaterThanOrEqualTo(2);
    }



    [Fact]
    public void GetPrimaryCategory_WithNoCategories_ReturnsNull()
    {
        var user = CreateUserWithCategories();

        user.GetPrimaryCategory().Should().BeNull();
    }

    [Theory]
    [InlineData(MemberCategory.Leitao, MemberCategory.Leitao)]
    [InlineData(MemberCategory.Caloiro, MemberCategory.Caloiro)]
    [InlineData(MemberCategory.Tuno, MemberCategory.Tuno)]
    [InlineData(MemberCategory.Veterano, MemberCategory.Veterano)]
    [InlineData(MemberCategory.Tunossauro, MemberCategory.Tunossauro)]
    [InlineData(MemberCategory.Fundador, MemberCategory.Fundador)]
    [InlineData(MemberCategory.TunoHonorario, MemberCategory.TunoHonorario)]
    public void GetPrimaryCategory_SingleCategory_ReturnsThatCategory(MemberCategory only, MemberCategory expected)
    {
        var user = CreateUserWithCategories(only);

        user.GetPrimaryCategory().Should().Be(expected);
    }

    [Fact]
    public void GetPrimaryCategory_TunoHonorario_OutranksEverything()
    {
        var user = CreateUserWithCategories(
            MemberCategory.Tuno,
            MemberCategory.Caloiro,
            MemberCategory.TunoHonorario,
            MemberCategory.Fundador);

        user.GetPrimaryCategory().Should().Be(MemberCategory.TunoHonorario);
    }

    [Fact]
    public void GetPrimaryCategory_FundadorOutranksTunossauro()
    {
        var user = CreateUserWithCategories(MemberCategory.Tunossauro, MemberCategory.Fundador);

        user.GetPrimaryCategory().Should().Be(MemberCategory.Fundador);
    }

    [Fact]
    public void GetPrimaryCategory_TunossauroOutranksVeteranoAndTuno()
    {
        var user = CreateUserWithCategories(MemberCategory.Tuno, MemberCategory.Veterano, MemberCategory.Tunossauro);

        user.GetPrimaryCategory().Should().Be(MemberCategory.Tunossauro);
    }

    [Fact]
    public void GetPrimaryCategory_CaloiroOutranksLeitao()
    {
        var user = CreateUserWithCategories(MemberCategory.Leitao, MemberCategory.Caloiro);

        user.GetPrimaryCategory().Should().Be(MemberCategory.Caloiro);
    }
}
