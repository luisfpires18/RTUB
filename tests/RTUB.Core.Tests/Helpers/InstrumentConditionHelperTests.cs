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
}
