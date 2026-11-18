using FluentAssertions;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Extensions;

// ApplicationUserExtensionsTests: Years Calculation tests.
public partial class ApplicationUserExtensionsTests
{
    private ApplicationUser CreateUserWithYearTuno(int yearTuno)
    {
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = yearTuno;
        return user;
    }

    [Fact]
    public void GetYearsAsTuno_WithValidYearTuno_ReturnsCorrectYears()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = CreateUserWithYearTuno(currentYear - 3);

        // Act
        var years = user.GetYearsAsTuno();

        // Assert
        years.Should().Be(3);
    }

    [Fact]
    public void GetYearsAsTuno_WithNullYearTuno_ReturnsNull()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = null;

        // Act
        var years = user.GetYearsAsTuno();

        // Assert
        years.Should().BeNull();
    }

    [Fact]
    public void GetYearsAsTuno_WithCurrentYear_ReturnsZero()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = CreateUserWithYearTuno(currentYear);

        // Act
        var years = user.GetYearsAsTuno();

        // Assert
        years.Should().Be(0);
    }

    [Fact]
    public void HasBeenTunoForYears_WithSufficientYears_ReturnsTrue()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = CreateUserWithYearTuno(currentYear - 5);

        // Act & Assert
        user.HasBeenTunoForYears(3).Should().BeTrue();
        user.HasBeenTunoForYears(5).Should().BeTrue();
    }

    [Fact]
    public void HasBeenTunoForYears_WithInsufficientYears_ReturnsFalse()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = CreateUserWithYearTuno(currentYear - 2);

        // Act & Assert
        user.HasBeenTunoForYears(3).Should().BeFalse();
        user.HasBeenTunoForYears(5).Should().BeFalse();
    }

    [Fact]
    public void HasBeenTunoForYears_WithNullYearTuno_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = null;

        // Act & Assert
        user.HasBeenTunoForYears(2).Should().BeFalse();
    }

    [Fact]
    public void QualifiesForVeterano_With2YearsAsTuno_ReturnsTrue()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = CreateUserWithYearTuno(currentYear - 2);

        // Act & Assert
        user.QualifiesForVeterano().Should().BeTrue();
    }

    [Fact]
    public void QualifiesForVeterano_With1YearAsTuno_ReturnsFalse()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = CreateUserWithYearTuno(currentYear - 1);

        // Act & Assert
        user.QualifiesForVeterano().Should().BeFalse();
    }

    [Fact]
    public void QualifiesForTunossauro_With6YearsAsTuno_ReturnsTrue()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = CreateUserWithYearTuno(currentYear - 6);

        // Act & Assert
        user.QualifiesForTunossauro().Should().BeTrue();
    }

    [Fact]
    public void QualifiesForTunossauro_With5YearsAsTuno_ReturnsFalse()
    {
        // Arrange
        var currentYear = DateTime.Now.Year;
        var user = CreateUserWithYearTuno(currentYear - 5);

        // Act & Assert
        user.QualifiesForTunossauro().Should().BeFalse();
    }



    [Fact]
    public void CategoryChecks_WithEmptyCategories_ReturnFalse()
    {
        // Arrange
        var user = CreateUserWithCategories();

        // Act & Assert
        user.IsLeitao().Should().BeFalse();
        user.IsCaloiro().Should().BeFalse();
        user.IsTuno().Should().BeFalse();
        user.IsVeterano().Should().BeFalse();
        user.IsTunossauro().Should().BeFalse();
        user.IsOnlyLeitao().Should().BeFalse();
        user.IsTunoOrHigher().Should().BeFalse();
        user.IsEffectiveMember().Should().BeFalse();
    }

    [Fact]
    public void CategoryChecks_WithMultipleCategories_WorkCorrectly()
    {
        // Arrange
        var user = CreateUserWithCategories(
            MemberCategory.Leitao,
            MemberCategory.Caloiro,
            MemberCategory.Tuno
        );

        // Act & Assert
        user.IsLeitao().Should().BeTrue();
        user.IsCaloiro().Should().BeTrue();
        user.IsTuno().Should().BeTrue();
        user.IsOnlyLeitao().Should().BeFalse(); // Not ONLY Leitao
        user.IsTunoOrHigher().Should().BeTrue();
        user.IsEffectiveMember().Should().BeTrue();
    }
}
