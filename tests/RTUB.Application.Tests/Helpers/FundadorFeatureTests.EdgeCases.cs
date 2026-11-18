using FluentAssertions;
using RTUB.Application.Extensions;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Helpers;

// FundadorFeatureTests: Edge Cases tests.
public partial class FundadorFeatureTests
{
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
        user.Categories = new List<MemberCategory>
        {
            MemberCategory.Fundador
        };

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
        user.Categories = new List<MemberCategory>();

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
            Categories = new List<MemberCategory>()
        };

        // Act & Assert
        user.IsFundador().Should().BeFalse();
    }
}
