using System.Security.Claims;
using FluentAssertions;
using RTUB.Core.Constants;
using RTUB.Core.Extensions;
using Xunit;

namespace RTUB.Core.Tests.Extensions;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal CreatePrincipalWithClaims(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }
    
    [Fact]
    public void IsCategory_WithMatchingCategory_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Tuno));
        
        // Act & Assert
        principal.IsCategory(CategoryClaims.Tuno).Should().BeTrue();
    }
    
    [Fact]
    public void IsCategory_WithNonMatchingCategory_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Leitao));
        
        // Act & Assert
        principal.IsCategory(CategoryClaims.Tuno).Should().BeFalse();
    }
    
    [Fact]
    public void IsCategory_WithNullPrincipal_ReturnsFalse()
    {
        // Arrange
        ClaimsPrincipal? principal = null;
        
        // Act & Assert
        principal!.IsCategory(CategoryClaims.Tuno).Should().BeFalse();
    }
    
    [Fact]
    public void IsCategory_WithEmptyCategory_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Tuno));
        
        // Act & Assert
        principal.IsCategory("").Should().BeFalse();
    }
    
    [Fact]
    public void IsCategory_CaseInsensitive_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Tuno));
        
        // Act & Assert
        principal.IsCategory("tuno").Should().BeTrue();
        principal.IsCategory("TUNO").Should().BeTrue();
        principal.IsCategory("Tuno").Should().BeTrue();
    }
    
    [Fact]
    public void IsAtLeastCategory_WithTuno_AllowsCaloiro()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Tuno));
        
        // Act & Assert
        principal.IsAtLeastCategory(CategoryClaims.Caloiro).Should().BeTrue();
    }
    
    [Fact]
    public void IsAtLeastCategory_WithLeitao_DeniesTuno()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Leitao));
        
        // Act & Assert
        principal.IsAtLeastCategory(CategoryClaims.Tuno).Should().BeFalse();
    }
    
    [Fact]
    public void IsAtLeastCategory_WithTunossauro_AllowsAll()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Tunossauro));
        
        // Act & Assert
        principal.IsAtLeastCategory(CategoryClaims.Leitao).Should().BeTrue();
        principal.IsAtLeastCategory(CategoryClaims.Caloiro).Should().BeTrue();
        principal.IsAtLeastCategory(CategoryClaims.Tuno).Should().BeTrue();
        principal.IsAtLeastCategory(CategoryClaims.Veterano).Should().BeTrue();
        principal.IsAtLeastCategory(CategoryClaims.Tunossauro).Should().BeTrue();
    }
    
    [Fact]
    public void IsAtLeastCategory_WithMultipleCategories_UsesHighest()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Leitao),
            new Claim(RtubClaimTypes.Category, CategoryClaims.Tuno));
        
        // Act & Assert
        principal.IsAtLeastCategory(CategoryClaims.Tuno).Should().BeTrue();
        principal.IsAtLeastCategory(CategoryClaims.Veterano).Should().BeFalse();
    }
    
    [Fact]
    public void IsOnlyLeitao_WithOnlyLeitao_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Leitao));
        
        // Act & Assert
        principal.IsOnlyLeitao().Should().BeTrue();
    }
    
    [Fact]
    public void IsOnlyLeitao_WithLeitaoAndCaloiro_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Leitao),
            new Claim(RtubClaimTypes.Category, CategoryClaims.Caloiro));
        
        // Act & Assert
        principal.IsOnlyLeitao().Should().BeFalse();
    }
    
    [Fact]
    public void HasPosition_WithMatchingPosition_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Position, PositionClaims.Magister));
        
        // Act & Assert
        principal.HasPosition(PositionClaims.Magister).Should().BeTrue();
    }
    
    [Fact]
    public void HasPosition_WithNonMatchingPosition_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Position, PositionClaims.Magister));
        
        // Act & Assert
        principal.HasPosition(PositionClaims.Secretario).Should().BeFalse();
    }
    
    [Fact]
    public void GetCategories_ReturnsAllCategories()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Leitao),
            new Claim(RtubClaimTypes.Category, CategoryClaims.Caloiro),
            new Claim(RtubClaimTypes.Category, CategoryClaims.Tuno));
        
        // Act
        var categories = principal.GetCategories().ToList();
        
        // Assert
        categories.Should().HaveCount(3);
        categories.Should().Contain(CategoryClaims.Leitao);
        categories.Should().Contain(CategoryClaims.Caloiro);
        categories.Should().Contain(CategoryClaims.Tuno);
    }
    
    [Fact]
    public void GetPositions_ReturnsAllPositions()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Position, PositionClaims.Magister),
            new Claim(RtubClaimTypes.Position, PositionClaims.Secretario));
        
        // Act
        var positions = principal.GetPositions().ToList();
        
        // Assert
        positions.Should().HaveCount(2);
        positions.Should().Contain(PositionClaims.Magister);
        positions.Should().Contain(PositionClaims.Secretario);
    }
    
    [Fact]
    public void GetPrimaryCategory_ReturnsPrimaryCategory()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.PrimaryCategory, CategoryClaims.Tuno));
        
        // Act
        var primaryCategory = principal.GetPrimaryCategory();
        
        // Assert
        primaryCategory.Should().Be(CategoryClaims.Tuno);
    }
    
    [Fact]
    public void IsFounder_WithFounderClaim_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.IsFounder, "true"));
        
        // Act & Assert
        principal.IsFounder().Should().BeTrue();
    }
    
    [Fact]
    public void IsFounder_WithoutFounderClaim_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims();
        
        // Act & Assert
        principal.IsFounder().Should().BeFalse();
    }
    
    [Fact]
    public void GetYearsAsTuno_ReturnsYearsValue()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.YearsAsTuno, "5"));
        
        // Act
        var years = principal.GetYearsAsTuno();
        
        // Assert
        years.Should().Be(5);
    }
    
    [Fact]
    public void GetYearsAsTuno_WithoutClaim_ReturnsNull()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims();
        
        // Act
        var years = principal.GetYearsAsTuno();
        
        // Assert
        years.Should().BeNull();
    }
    
    [Fact]
    public void CanBeMentor_WithTunoOrHigher_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Tuno));
        
        // Act & Assert
        principal.CanBeMentor().Should().BeTrue();
    }
    
    [Fact]
    public void CanBeMentor_WithLeitao_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Leitao));
        
        // Act & Assert
        principal.CanBeMentor().Should().BeFalse();
    }
    
    [Fact]
    public void CanHoldPresidentPosition_WithTunoOrHigher_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Veterano));
        
        // Act & Assert
        principal.CanHoldPresidentPosition().Should().BeTrue();
    }
    
    [Fact]
    public void IsCaloiroAdmin_WithCaloiroAdmin_ReturnsTrue()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Caloiro),
            new Claim(System.Security.Claims.ClaimTypes.Role, "Admin"));
        
        // Act & Assert
        principal.IsCaloiroAdmin().Should().BeTrue();
    }
    
    [Fact]
    public void IsCaloiroAdmin_WithCaloiroOwner_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Caloiro),
            new Claim(System.Security.Claims.ClaimTypes.Role, "Admin"),
            new Claim(System.Security.Claims.ClaimTypes.Role, "Owner"));
        
        // Act & Assert
        principal.IsCaloiroAdmin().Should().BeFalse();
    }
    
    [Fact]
    public void IsCaloiroAdmin_WithTunoAdmin_ReturnsFalse()
    {
        // Arrange
        var principal = CreatePrincipalWithClaims(
            new Claim(RtubClaimTypes.Category, CategoryClaims.Tuno),
            new Claim(System.Security.Claims.ClaimTypes.Role, "Admin"));
        
        // Act & Assert
        principal.IsCaloiroAdmin().Should().BeFalse();
    }
}
