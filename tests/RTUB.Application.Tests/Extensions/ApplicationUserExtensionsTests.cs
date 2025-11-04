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
}
