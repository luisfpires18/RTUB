using FluentAssertions;
using RTUB.Application.Extensions;
using RTUB.Application.Helpers;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using System.Text.Json;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// Comprehensive unit tests for Fundador (Founder) feature
/// Tests the complete Fundador functionality including category management,
/// timeline display, and special business rules
/// </summary>
public class FundadorFeatureTests
{
    #region Test Data Helpers
    
    private ApplicationUser CreateFundador(string firstName = "João", string lastName = "Silva")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = firstName,
            LastName = lastName,
            Nickname = $"{firstName}Fundador",
            Email = $"{firstName.ToLower()}@tuna.com",
            UserName = $"{firstName.ToLower()}@tuna.com",
            YearTuno = 1991,
            YearLeitao = null,
            YearCaloiro = null,
            MentorId = null
        };
        
        // Fundador is a subcategory: Tuno + Fundador
        user.CategoriesJson = JsonSerializer.Serialize(new List<MemberCategory> 
        { 
            MemberCategory.Tuno, 
            MemberCategory.Fundador 
        });
        
        return user;
    }
    
    private ApplicationUser CreateRegularTuno(int yearTuno, int yearLeitao, int yearCaloiro)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = "Regular",
            LastName = "Tuno",
            Nickname = "RegularTuno",
            Email = "regular@tuna.com",
            UserName = "regular@tuna.com",
            YearTuno = yearTuno,
            YearLeitao = yearLeitao,
            YearCaloiro = yearCaloiro,
            MentorId = Guid.NewGuid().ToString()
        };
        
        user.CategoriesJson = JsonSerializer.Serialize(new List<MemberCategory> 
        { 
            MemberCategory.Tuno 
        });
        
        return user;
    }
    
    #endregion
    
    #region Fundador Category Tests
    
    [Fact]
    public void Fundador_ShouldHaveTunoAndFundadorCategories()
    {
        // Arrange & Act
        var fundador = CreateFundador();
        
        // Assert
        fundador.Categories.Should().Contain(MemberCategory.Tuno);
        fundador.Categories.Should().Contain(MemberCategory.Fundador);
        fundador.Categories.Should().HaveCount(2);
    }
    
    [Fact]
    public void Fundador_ShouldBeIdentifiedByIsFundadorMethod()
    {
        // Arrange
        var fundador = CreateFundador();
        
        // Act & Assert
        fundador.IsFundador().Should().BeTrue();
        fundador.IsTuno().Should().BeTrue(); // Also a Tuno
    }
    
    [Fact]
    public void RegularTuno_ShouldNotBeIdentifiedAsFundador()
    {
        // Arrange
        var regularTuno = CreateRegularTuno(2020, 2018, 2019);
        
        // Act & Assert
        regularTuno.IsFundador().Should().BeFalse();
        regularTuno.IsTuno().Should().BeTrue();
    }
    
    #endregion
    
    #region Fundador Business Rules Tests
    
    [Fact]
    public void Fundador_ShouldHaveYearTuno1991()
    {
        // Arrange & Act
        var fundador = CreateFundador();
        
        // Assert
        fundador.YearTuno.Should().Be(1991);
    }
    
    [Fact]
    public void Fundador_ShouldHaveNoLeitaoYear()
    {
        // Arrange & Act
        var fundador = CreateFundador();
        
        // Assert
        fundador.YearLeitao.Should().BeNull();
    }
    
    [Fact]
    public void Fundador_ShouldHaveNoCaloiroYear()
    {
        // Arrange & Act
        var fundador = CreateFundador();
        
        // Assert
        fundador.YearCaloiro.Should().BeNull();
    }
    
    [Fact]
    public void Fundador_ShouldHaveNoMentor()
    {
        // Arrange & Act
        var fundador = CreateFundador();
        
        // Assert
        fundador.MentorId.Should().BeNull();
    }
    
    [Fact]
    public void RegularTuno_ShouldHaveMentor()
    {
        // Arrange & Act
        var regularTuno = CreateRegularTuno(2020, 2018, 2019);
        
        // Assert
        regularTuno.MentorId.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public void RegularTuno_ShouldHaveLeitaoAndCaloiroYears()
    {
        // Arrange & Act
        var regularTuno = CreateRegularTuno(2020, 2018, 2019);
        
        // Assert
        regularTuno.YearLeitao.Should().Be(2018);
        regularTuno.YearCaloiro.Should().Be(2019);
        regularTuno.YearTuno.Should().Be(2020);
    }
    
    #endregion
    
    #region Display Categories Tests
    
    [Fact]
    public void GetDisplayCategories_ForFundador_ShouldIncludeBothTunoAndFundador()
    {
        // Arrange
        var fundador = CreateFundador();
        
        // Act
        var displayCategories = StatusHelper.GetDisplayCategories(fundador);
        
        // Assert
        displayCategories.Should().Contain(MemberCategory.Tuno);
        displayCategories.Should().Contain(MemberCategory.Fundador);
    }
    
    [Fact]
    public void GetDisplayCategories_ForRegularTuno_ShouldOnlyIncludeTuno()
    {
        // Arrange
        var regularTuno = CreateRegularTuno(2020, 2018, 2019);
        
        // Act
        var displayCategories = StatusHelper.GetDisplayCategories(regularTuno);
        
        // Assert
        displayCategories.Should().Contain(MemberCategory.Tuno);
        displayCategories.Should().NotContain(MemberCategory.Fundador);
    }
    
    [Fact]
    public void GetCategoryDisplay_ForFundador_ShouldReturnFUNDADOR()
    {
        // Arrange & Act
        var display = StatusHelper.GetCategoryDisplay(MemberCategory.Fundador);
        
        // Assert
        display.Should().Be("FUNDADOR");
    }
    
    [Fact]
    public void GetCategoryBadgeClass_ForFundador_ShouldReturnBadgeFundador()
    {
        // Arrange & Act
        var badgeClass = StatusHelper.GetCategoryBadgeClass(MemberCategory.Fundador);
        
        // Assert
        badgeClass.Should().Be("badge-fundador");
    }
    
    #endregion
    
    #region Multiple Fundadores Tests
    
    [Theory]
    [InlineData("Manuel", "Costa")]
    [InlineData("António", "Santos")]
    [InlineData("Pedro", "Oliveira")]
    public void MultipleFundadores_ShouldAllHaveSameYearAndNoMentor(string firstName, string lastName)
    {
        // Arrange & Act
        var fundador = CreateFundador(firstName, lastName);
        
        // Assert
        fundador.YearTuno.Should().Be(1991);
        fundador.MentorId.Should().BeNull();
        fundador.IsFundador().Should().BeTrue();
    }
    
    [Fact]
    public void FundadorAndVeterano_ShouldHaveBothCategories()
    {
        // Arrange
        var fundador = CreateFundador();
        
        // Simulate becoming Veterano (2+ years as Tuno)
        fundador.YearTuno = 1991;
        
        // Act
        var displayCategories = StatusHelper.GetDisplayCategories(fundador);
        
        // Assert - Fundador from 1991 should also be Veterano and Tunossauro by now
        displayCategories.Should().Contain(MemberCategory.Tuno);
        displayCategories.Should().Contain(MemberCategory.Fundador);
        displayCategories.Should().Contain(MemberCategory.Veterano);
        displayCategories.Should().Contain(MemberCategory.Tunossauro);
    }
    
    #endregion
    
    #region Edge Cases
    
    [Fact]
    public void Fundador_WithOnlyFundadorCategory_ShouldStillBeDetected()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = "Test",
            LastName = "User",
            YearTuno = 1991
        };
        user.CategoriesJson = JsonSerializer.Serialize(new List<MemberCategory> 
        { 
            MemberCategory.Fundador 
        });
        
        // Act & Assert
        user.IsFundador().Should().BeTrue();
    }
    
    [Fact]
    public void User_WithEmptyCategories_ShouldNotBeFundador()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = "Test",
            LastName = "User",
            YearTuno = 1991
        };
        user.CategoriesJson = JsonSerializer.Serialize(new List<MemberCategory>());
        
        // Act & Assert
        user.IsFundador().Should().BeFalse();
    }
    
    [Fact]
    public void User_WithNullCategories_ShouldNotBeFundador()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = "Test",
            LastName = "User",
            YearTuno = 1991,
            CategoriesJson = null
        };
        
        // Act & Assert
        user.IsFundador().Should().BeFalse();
    }
    
    #endregion
}
