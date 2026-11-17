// ApplicationUser current role
using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public partial class ApplicationUserTests
{
    #region CurrentRole Tests

    [Fact]
    public void CurrentRole_WithNoYearTuno_ReturnsNA()
    {
        // Arrange
        var user = new ApplicationUser { YearTuno = null };

        // Act
        var role = user.CurrentRole;

        // Assert
        role.Should().Be("N/A");
    }

    [Fact]
    public void CurrentRole_WithYearAndMonthFor2Years_ReturnsVETERANO()
    {
        // Arrange
        var now = DateTime.Now;
        var twoYearsAgo = now.AddYears(-2);
        var user = new ApplicationUser
        {
            YearTuno = twoYearsAgo.Year,
            MonthTuno = twoYearsAgo.Month
        };

        // Act
        var role = user.CurrentRole;

        // Assert
        role.Should().Be("VETERANO");
    }

    [Fact]
    public void CurrentRole_WithYearAndMonthFor6Years_ReturnsTUNOSSAURO()
    {
        // Arrange
        var now = DateTime.Now;
        var sixYearsAgo = now.AddYears(-6);
        var user = new ApplicationUser
        {
            YearTuno = sixYearsAgo.Year,
            MonthTuno = sixYearsAgo.Month
        };

        // Act
        var role = user.CurrentRole;

        // Assert
        role.Should().Be("TUNOSSAURO");
    }

    [Fact]
    public void CurrentRole_WithLessThan2Years_ReturnsTUNO()
    {
        // Arrange
        var now = DateTime.Now;
        var oneYearAgo = now.AddYears(-1);
        var user = new ApplicationUser
        {
            YearTuno = oneYearAgo.Year,
            MonthTuno = oneYearAgo.Month
        };

        // Act
        var role = user.CurrentRole;

        // Assert
        role.Should().Be("TUNO");
    }

    [Fact]
    public void CurrentRole_WithOnlyYearNoMonth_DefaultsToJanuary()
    {
        // Arrange - 3 years ago without month (backward compatibility)
        var currentYear = DateTime.Now.Year;
        var user = new ApplicationUser
        {
            YearTuno = currentYear - 3,
            MonthTuno = null
        };

        // Act
        var role = user.CurrentRole;

        // Assert - Should be VETERANO or TUNOSSAURO depending on months
        role.Should().BeOneOf("VETERANO", "TUNOSSAURO");
    }

    [Fact]
    public void CurrentRole_WithAlmost2Years_ReturnsTUNO()
    {
        // Arrange
        var now = DateTime.Now;
        var almostTwoYears = now.AddYears(-2).AddMonths(1); // 1 month shy
        var user = new ApplicationUser
        {
            YearTuno = almostTwoYears.Year,
            MonthTuno = almostTwoYears.Month
        };

        // Act
        var role = user.CurrentRole;

        // Assert
        role.Should().Be("TUNO");
    }

    #endregion
}
