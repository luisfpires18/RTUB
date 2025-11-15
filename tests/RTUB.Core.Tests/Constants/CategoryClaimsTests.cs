using FluentAssertions;
using RTUB.Core.Constants;
using Xunit;

namespace RTUB.Core.Tests.Constants;

public class CategoryClaimsTests
{
    [Fact]
    public void GetLevel_WithLeitao_Returns0()
    {
        // Arrange & Act
        var level = CategoryClaims.GetLevel(CategoryClaims.Leitao);
        
        // Assert
        level.Should().Be(0);
    }
    
    [Fact]
    public void GetLevel_WithCaloiro_Returns1()
    {
        // Arrange & Act
        var level = CategoryClaims.GetLevel(CategoryClaims.Caloiro);
        
        // Assert
        level.Should().Be(1);
    }
    
    [Fact]
    public void GetLevel_WithTuno_Returns2()
    {
        // Arrange & Act
        var level = CategoryClaims.GetLevel(CategoryClaims.Tuno);
        
        // Assert
        level.Should().Be(2);
    }
    
    [Fact]
    public void GetLevel_WithVeterano_Returns3()
    {
        // Arrange & Act
        var level = CategoryClaims.GetLevel(CategoryClaims.Veterano);
        
        // Assert
        level.Should().Be(3);
    }
    
    [Fact]
    public void GetLevel_WithTunossauro_Returns4()
    {
        // Arrange & Act
        var level = CategoryClaims.GetLevel(CategoryClaims.Tunossauro);
        
        // Assert
        level.Should().Be(4);
    }
    
    [Fact]
    public void GetLevel_WithNull_ReturnsMinus1()
    {
        // Arrange & Act
        var level = CategoryClaims.GetLevel(null);
        
        // Assert
        level.Should().Be(-1);
    }
    
    [Fact]
    public void GetLevel_WithEmptyString_ReturnsMinus1()
    {
        // Arrange & Act
        var level = CategoryClaims.GetLevel("");
        
        // Assert
        level.Should().Be(-1);
    }
    
    [Fact]
    public void GetLevel_WithUnknownCategory_ReturnsMinus1()
    {
        // Arrange & Act
        var level = CategoryClaims.GetLevel("UNKNOWN");
        
        // Assert
        level.Should().Be(-1);
    }
    
    [Fact]
    public void GetLevel_WithLowerCase_WorksCorrectly()
    {
        // Arrange & Act
        var level = CategoryClaims.GetLevel("tuno");
        
        // Assert
        level.Should().Be(2);
    }
    
    [Theory]
    [InlineData(CategoryClaims.Leitao, 0)]
    [InlineData(CategoryClaims.Caloiro, 1)]
    [InlineData(CategoryClaims.Tuno, 2)]
    [InlineData(CategoryClaims.Veterano, 3)]
    [InlineData(CategoryClaims.Tunossauro, 4)]
    public void GetLevel_VerifyHierarchy(string category, int expectedLevel)
    {
        // Arrange & Act
        var level = CategoryClaims.GetLevel(category);
        
        // Assert
        level.Should().Be(expectedLevel);
    }
    
    [Fact]
    public void Hierarchy_ContainsAllMainCategories()
    {
        // Arrange & Act
        var hierarchy = CategoryClaims.Hierarchy;
        
        // Assert
        hierarchy.Should().HaveCount(5);
        hierarchy.Should().Contain(CategoryClaims.Leitao);
        hierarchy.Should().Contain(CategoryClaims.Caloiro);
        hierarchy.Should().Contain(CategoryClaims.Tuno);
        hierarchy.Should().Contain(CategoryClaims.Veterano);
        hierarchy.Should().Contain(CategoryClaims.Tunossauro);
    }
    
    [Fact]
    public void Hierarchy_IsInCorrectOrder()
    {
        // Arrange & Act
        var hierarchy = CategoryClaims.Hierarchy;
        
        // Assert
        hierarchy[0].Should().Be(CategoryClaims.Leitao);
        hierarchy[1].Should().Be(CategoryClaims.Caloiro);
        hierarchy[2].Should().Be(CategoryClaims.Tuno);
        hierarchy[3].Should().Be(CategoryClaims.Veterano);
        hierarchy[4].Should().Be(CategoryClaims.Tunossauro);
    }
}
