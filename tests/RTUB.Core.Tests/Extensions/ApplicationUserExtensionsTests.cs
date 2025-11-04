using FluentAssertions;
using RTUB.Core.Entities;
using RTUB.Core.Extensions;

namespace RTUB.Core.Tests.Extensions;

/// <summary>
/// Unit tests for ApplicationUserExtensions
/// </summary>
public class ApplicationUserExtensionsTests
{
    [Fact]
    public void GetFullName_WithBothNames_ReturnsFullName()
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "John",
            LastName = "Doe"
        };

        // Act
        var result = user.GetFullName();

        // Assert
        result.Should().Be("John Doe");
    }

    [Fact]
    public void GetFullName_WithNullUser_ReturnsEmpty()
    {
        // Arrange
        ApplicationUser? user = null;

        // Act
        var result = user!.GetFullName();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetFullName_WithOnlyFirstName_ReturnsFirstNameWithSpace()
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "John",
            LastName = null
        };

        // Act
        var result = user.GetFullName();

        // Assert
        result.Should().Be("John ");
    }
}
