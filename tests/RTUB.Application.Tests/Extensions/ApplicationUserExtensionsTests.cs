using FluentAssertions;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using System.Text.Json;

namespace RTUB.Application.Tests.Extensions;

/// <summary>
/// Unit tests for ApplicationUserExtensions
/// Tests category checks and role validation logic
/// </summary>
public class ApplicationUserExtensionsTests
{
    #region Test Data Helpers
    
    private ApplicationUser CreateUserWithCategories(params MemberCategory[] categories)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = "Test",
            LastName = "User",
            Nickname = "TestUser",
            Email = "test@example.com",
            UserName = "test@example.com"
        };
        
        if (categories.Any())
        {
            user.CategoriesJson = JsonSerializer.Serialize(categories.ToList());
        }
        
        return user;
    }
    
    private ApplicationUser CreateUserWithYearTuno(int yearTuno)
    {
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        user.YearTuno = yearTuno;
        return user;
    }
    
    #endregion
    
    #region Category Checker Tests
    
    [Fact]
    public void IsLeitao_WithLeitaoCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao);
        
        // Act & Assert
        user.IsLeitao().Should().BeTrue();
    }
    
    [Fact]
    public void IsLeitao_WithoutLeitaoCategory_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Caloiro);
        
        // Act & Assert
        user.IsLeitao().Should().BeFalse();
    }
    
    [Fact]
    public void IsCaloiro_WithCaloiroCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Caloiro);
        
        // Act & Assert
        user.IsCaloiro().Should().BeTrue();
    }
    
    [Fact]
    public void IsTuno_WithTunoCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        
        // Act & Assert
        user.IsTuno().Should().BeTrue();
    }
    
    [Fact]
    public void IsVeterano_WithVeteranoCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Veterano);
        
        // Act & Assert
        user.IsVeterano().Should().BeTrue();
    }
    
    [Fact]
    public void IsTunossauro_WithTunossauroCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tunossauro);
        
        // Act & Assert
        user.IsTunossauro().Should().BeTrue();
    }
    
    [Fact]
    public void IsTunoHonorario_WithTunoHonorarioCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.TunoHonorario);
        
        // Act & Assert
        user.IsTunoHonorario().Should().BeTrue();
    }
    
    [Fact]
    public void IsFundador_WithFundadorCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Fundador);
        
        // Act & Assert
        user.IsFundador().Should().BeTrue();
    }
    
    [Fact]
    public void IsFundador_WithoutFundadorCategory_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        
        // Act & Assert
        user.IsFundador().Should().BeFalse();
    }
    
    #endregion
    
    #region Combined Checks Tests
    
    [Fact]
    public void IsOnlyLeitao_WithOnlyLeitaoCategory_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao);
        
        // Act & Assert
        user.IsOnlyLeitao().Should().BeTrue();
    }
    
    [Fact]
    public void IsOnlyLeitao_WithLeitaoAndCaloiro_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao, MemberCategory.Caloiro);
        
        // Act & Assert
        user.IsOnlyLeitao().Should().BeFalse();
    }
    
    [Fact]
    public void IsOnlyLeitao_WithLeitaoAndTuno_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao, MemberCategory.Tuno);
        
        // Act & Assert
        user.IsOnlyLeitao().Should().BeFalse();
    }
    
    [Fact]
    public void IsOnlyLeitao_WithoutLeitao_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Caloiro);
        
        // Act & Assert
        user.IsOnlyLeitao().Should().BeFalse();
    }
    
    [Fact]
    public void IsTunoOrHigher_WithTuno_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        
        // Act & Assert
        user.IsTunoOrHigher().Should().BeTrue();
    }
    
    [Fact]
    public void IsTunoOrHigher_WithVeterano_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Veterano);
        
        // Act & Assert
        user.IsTunoOrHigher().Should().BeTrue();
    }
    
    [Fact]
    public void IsTunoOrHigher_WithTunossauro_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tunossauro);
        
        // Act & Assert
        user.IsTunoOrHigher().Should().BeTrue();
    }
    
    [Fact]
    public void IsTunoOrHigher_WithCaloiro_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Caloiro);
        
        // Act & Assert
        user.IsTunoOrHigher().Should().BeFalse();
    }
    
    [Fact]
    public void IsTunoOrHigher_WithLeitao_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao);
        
        // Act & Assert
        user.IsTunoOrHigher().Should().BeFalse();
    }
    
    [Fact]
    public void IsEffectiveMember_WithCaloiro_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Caloiro);
        
        // Act & Assert
        user.IsEffectiveMember().Should().BeTrue();
    }
    
    [Fact]
    public void IsEffectiveMember_WithTuno_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        
        // Act & Assert
        user.IsEffectiveMember().Should().BeTrue();
    }
    
    [Fact]
    public void IsEffectiveMember_WithVeterano_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Veterano);
        
        // Act & Assert
        user.IsEffectiveMember().Should().BeTrue();
    }
    
    [Fact]
    public void IsEffectiveMember_WithTunossauro_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tunossauro);
        
        // Act & Assert
        user.IsEffectiveMember().Should().BeTrue();
    }
    
    [Fact]
    public void IsEffectiveMember_WithOnlyLeitao_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao);
        
        // Act & Assert
        user.IsEffectiveMember().Should().BeFalse();
    }
    
    [Fact]
    public void CanBeMentor_WithTunoOrHigher_ReturnsTrue()
    {
        // Arrange
        var tunoUser = CreateUserWithCategories(MemberCategory.Tuno);
        var veteranoUser = CreateUserWithCategories(MemberCategory.Veterano);
        var tunossauroUser = CreateUserWithCategories(MemberCategory.Tunossauro);
        
        // Act & Assert
        tunoUser.CanBeMentor().Should().BeTrue();
        veteranoUser.CanBeMentor().Should().BeTrue();
        tunossauroUser.CanBeMentor().Should().BeTrue();
    }
    
    [Fact]
    public void CanBeMentor_WithCaloiroOrLeitao_ReturnsFalse()
    {
        // Arrange
        var caloiroUser = CreateUserWithCategories(MemberCategory.Caloiro);
        var leitaoUser = CreateUserWithCategories(MemberCategory.Leitao);
        
        // Act & Assert
        caloiroUser.CanBeMentor().Should().BeFalse();
        leitaoUser.CanBeMentor().Should().BeFalse();
    }
    
    [Fact]
    public void CanHoldPresidentPosition_WithTunoOrHigher_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Tuno);
        
        // Act & Assert
        user.CanHoldPresidentPosition().Should().BeTrue();
    }
    
    [Fact]
    public void CanHoldPresidentPosition_WithCaloiroOrLeitao_ReturnsFalse()
    {
        // Arrange
        var caloiroUser = CreateUserWithCategories(MemberCategory.Caloiro);
        var leitaoUser = CreateUserWithCategories(MemberCategory.Leitao);
        
        // Act & Assert
        caloiroUser.CanHoldPresidentPosition().Should().BeFalse();
        leitaoUser.CanHoldPresidentPosition().Should().BeFalse();
    }
    
    [Fact]
    public void IsNotOnlyLeitao_WithCaloiro_ReturnsTrue()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Caloiro);
        
        // Act & Assert
        user.IsNotOnlyLeitao().Should().BeTrue();
    }
    
    [Fact]
    public void IsNotOnlyLeitao_WithOnlyLeitao_ReturnsFalse()
    {
        // Arrange
        var user = CreateUserWithCategories(MemberCategory.Leitao);
        
        // Act & Assert
        user.IsNotOnlyLeitao().Should().BeFalse();
    }
    
    #endregion
    
    #region Years Calculation Tests
    
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
    
    #endregion
    
    #region Edge Cases
    
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
    
    #endregion
    
    #region Month-Aware Years Calculation Tests
    
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
    
    #endregion
}
