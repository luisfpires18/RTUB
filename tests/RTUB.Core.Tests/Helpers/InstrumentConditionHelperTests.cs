using Xunit;
using FluentAssertions;
using RTUB.Core.Enums;
using RTUB.Core.Helpers;

namespace RTUB.Core.Tests.Helpers;

/// <summary>
/// Unit tests for InstrumentConditionHelper
/// </summary>
public class InstrumentConditionHelperTests
{
    #region GetDisplayName Tests

    [Theory]
    [InlineData(InstrumentCondition.Excellent, "Óptimo")]
    [InlineData(InstrumentCondition.Good, "Bom")]
    [InlineData(InstrumentCondition.Worn, "Velho")]
    [InlineData(InstrumentCondition.NeedsMaintenance, "Precisa Manutenção")]
    [InlineData(InstrumentCondition.Lost, "Perdido")]
    public void GetDisplayName_ReturnsCorrectPortugueseName(InstrumentCondition condition, string expectedName)
    {
        // Act
        var displayName = InstrumentConditionHelper.GetDisplayName(condition);

        // Assert
        displayName.Should().Be(expectedName, $"Condition {condition} should display as {expectedName}");
    }

    #endregion

    #region GetBadgeClass Tests

    [Theory]
    [InlineData(InstrumentCondition.Excellent, "bg-success")]
    [InlineData(InstrumentCondition.Good, "bg-info")]
    [InlineData(InstrumentCondition.Worn, "bg-warning")]
    [InlineData(InstrumentCondition.NeedsMaintenance, "bg-warning")]
    [InlineData(InstrumentCondition.Lost, "bg-danger")]
    public void GetBadgeClass_ReturnsCorrectCssClass(InstrumentCondition condition, string expectedClass)
    {
        // Act
        var badgeClass = InstrumentConditionHelper.GetBadgeClass(condition);

        // Assert
        badgeClass.Should().Be(expectedClass, $"Condition {condition} should have badge class {expectedClass}");
    }

    [Fact]
    public void GetBadgeClass_Excellent_ReturnsGreen()
    {
        // Arrange
        var condition = InstrumentCondition.Excellent;

        // Act
        var badgeClass = InstrumentConditionHelper.GetBadgeClass(condition);

        // Assert
        badgeClass.Should().Be("bg-success", "Excellent condition should be green (success)");
    }

    [Fact]
    public void GetBadgeClass_NeedsMaintenance_ReturnsYellow()
    {
        // Arrange
        var condition = InstrumentCondition.NeedsMaintenance;

        // Act
        var badgeClass = InstrumentConditionHelper.GetBadgeClass(condition);

        // Assert
        badgeClass.Should().Be("bg-warning", "NeedsMaintenance condition should be yellow (warning)");
    }

    [Fact]
    public void GetBadgeClass_Lost_ReturnsRed()
    {
        // Arrange
        var condition = InstrumentCondition.Lost;

        // Act
        var badgeClass = InstrumentConditionHelper.GetBadgeClass(condition);

        // Assert
        badgeClass.Should().Be("bg-danger", "Lost condition should be red (danger)");
    }

    #endregion
}
