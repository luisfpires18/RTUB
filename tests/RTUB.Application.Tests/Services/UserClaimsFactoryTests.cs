using System.Security.Claims;
using FluentAssertions;
using RTUB.Application.Services;
using RTUB.Core.Constants;
using RTUB.Core.Entities;
using RTUB.Core.Enums;
using Xunit;

namespace RTUB.Application.Tests.Services;

public class UserClaimsFactoryTests
{
    [Fact]
    public void CreateClaims_WithNullUser_ThrowsArgumentNullException()
    {
        // Arrange
        ApplicationUser? user = null;
        
        // Act
        Action act = () => UserClaimsFactory.CreateClaims(user!);
        
        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public void CreateClaims_WithSingleCategory_AddsCategoryClaim()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester",
            PhoneContact = "123456789",
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };
        
        // Act
        var claims = UserClaimsFactory.CreateClaims(user).ToList();
        
        // Assert
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.Category && c.Value == CategoryClaims.Tuno);
    }
    
    [Fact]
    public void CreateClaims_WithMultipleCategories_AddsAllCategoryClaims()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester",
            PhoneContact = "123456789",
            Categories = new List<MemberCategory> 
            { 
                MemberCategory.Leitao, 
                MemberCategory.Caloiro,
                MemberCategory.Tuno 
            }
        };
        
        // Act
        var claims = UserClaimsFactory.CreateClaims(user).ToList();
        
        // Assert
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.Category && c.Value == CategoryClaims.Leitao);
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.Category && c.Value == CategoryClaims.Caloiro);
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.Category && c.Value == CategoryClaims.Tuno);
    }
    
    [Fact]
    public void CreateClaims_WithTunoCategory_SetsPrimaryCategory()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester",
            PhoneContact = "123456789",
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };
        
        // Act
        var claims = UserClaimsFactory.CreateClaims(user).ToList();
        
        // Assert
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.PrimaryCategory && c.Value == CategoryClaims.Tuno);
    }
    
    [Fact]
    public void CreateClaims_WithMultipleCategories_SetsHighestPrimaryCategory()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester",
            PhoneContact = "123456789",
            Categories = new List<MemberCategory> 
            { 
                MemberCategory.Leitao, 
                MemberCategory.Caloiro,
                MemberCategory.Tuno,
                MemberCategory.Veterano
            }
        };
        
        // Act
        var claims = UserClaimsFactory.CreateClaims(user).ToList();
        
        // Assert
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.PrimaryCategory && c.Value == CategoryClaims.Veterano);
    }
    
    [Fact]
    public void CreateClaims_WithPositions_AddsPositionClaims()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester",
            PhoneContact = "123456789",
            Positions = new List<Position> { Position.Magister, Position.Secretario }
        };
        
        // Act
        var claims = UserClaimsFactory.CreateClaims(user).ToList();
        
        // Assert
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.Position && c.Value == PositionClaims.Magister);
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.Position && c.Value == PositionClaims.Secretario);
    }
    
    [Fact]
    public void CreateClaims_WithYearTuno_AddsYearsAsTunoClaim()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester",
            PhoneContact = "123456789",
            YearTuno = DateTime.Now.Year - 3, // 3 years ago
            MonthTuno = DateTime.Now.Month
        };
        
        // Act
        var claims = UserClaimsFactory.CreateClaims(user).ToList();
        
        // Assert
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.YearsAsTuno && c.Value == "3");
    }
    
    [Fact]
    public void CreateClaims_WithFundadorCategory_AddsIsFounderClaim()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester",
            PhoneContact = "123456789",
            Categories = new List<MemberCategory> { MemberCategory.Fundador }
        };
        
        // Act
        var claims = UserClaimsFactory.CreateClaims(user).ToList();
        
        // Assert
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.IsFounder && c.Value == "true");
    }
    
    [Fact]
    public void CreateClaims_WithoutFundadorCategory_DoesNotAddIsFounderClaim()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester",
            PhoneContact = "123456789",
            Categories = new List<MemberCategory> { MemberCategory.Tuno }
        };
        
        // Act
        var claims = UserClaimsFactory.CreateClaims(user).ToList();
        
        // Assert
        claims.Should().NotContain(c => c.Type == RtubClaimTypes.IsFounder);
    }
    
    [Fact]
    public void CreateClaims_WithNoCategories_ReturnsEmptyClaims()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester",
            PhoneContact = "123456789"
        };
        
        // Act
        var claims = UserClaimsFactory.CreateClaims(user).ToList();
        
        // Assert
        claims.Should().NotContain(c => c.Type == RtubClaimTypes.Category);
        claims.Should().NotContain(c => c.Type == RtubClaimTypes.PrimaryCategory);
    }
    
    [Theory]
    [InlineData(MemberCategory.Leitao, CategoryClaims.Leitao)]
    [InlineData(MemberCategory.Caloiro, CategoryClaims.Caloiro)]
    [InlineData(MemberCategory.Tuno, CategoryClaims.Tuno)]
    [InlineData(MemberCategory.Veterano, CategoryClaims.Veterano)]
    [InlineData(MemberCategory.Tunossauro, CategoryClaims.Tunossauro)]
    [InlineData(MemberCategory.TunoHonorario, CategoryClaims.TunoHonorario)]
    [InlineData(MemberCategory.Fundador, CategoryClaims.Fundador)]
    public void CreateClaims_MapsCategoryCorrectly(MemberCategory category, string expectedClaim)
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester",
            PhoneContact = "123456789",
            Categories = new List<MemberCategory> { category }
        };
        
        // Act
        var claims = UserClaimsFactory.CreateClaims(user).ToList();
        
        // Assert
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.Category && c.Value == expectedClaim);
    }
    
    [Theory]
    [InlineData(Position.Magister, PositionClaims.Magister)]
    [InlineData(Position.ViceMagister, PositionClaims.ViceMagister)]
    [InlineData(Position.Secretario, PositionClaims.Secretario)]
    [InlineData(Position.PrimeiroTesoureiro, PositionClaims.PrimeiroTesoureiro)]
    [InlineData(Position.SegundoTesoureiro, PositionClaims.SegundoTesoureiro)]
    [InlineData(Position.Ensaiador, PositionClaims.Ensaiador)]
    public void CreateClaims_MapsPositionCorrectly(Position position, string expectedClaim)
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "test-user",
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Nickname = "Tester",
            PhoneContact = "123456789",
            Positions = new List<Position> { position }
        };
        
        // Act
        var claims = UserClaimsFactory.CreateClaims(user).ToList();
        
        // Assert
        claims.Should().Contain(c => 
            c.Type == RtubClaimTypes.Position && c.Value == expectedClaim);
    }
}
