using FluentAssertions;
using RTUB.Application.Helpers;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// Unit tests for StatusHelper
/// Tests status formatting and translation logic
/// </summary>
public class StatusHelperTests
{
    [Theory]
    [InlineData(Position.Magister, "MAGISTER")]
    [InlineData(Position.ViceMagister, "VICE-MAGISTER")]
    [InlineData(Position.Secretario, "SECRETÁRIO")]
    [InlineData(Position.PrimeiroTesoureiro, "1º TESOUREIRO")]
    [InlineData(Position.SegundoTesoureiro, "2º TESOUREIRO")]
    public void GetPositionDisplay_ReturnsCorrectName(Position position, string expected)
    {
        // Act
        var result = StatusHelper.GetPositionDisplay(position);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(MemberCategory.Tuno, "TUNO")]
    [InlineData(MemberCategory.Veterano, "VETERANO")]
    [InlineData(MemberCategory.Tunossauro, "TUNOSSAURO")]
    [InlineData(MemberCategory.TunoHonorario, "TUNO HONORÁRIO")]
    [InlineData(MemberCategory.Fundador, "FUNDADOR")]
    [InlineData(MemberCategory.Caloiro, "CALOIRO")]
    [InlineData(MemberCategory.Leitao, "LEITÃO")]
    public void GetCategoryDisplay_ReturnsCorrectName(MemberCategory category, string expected)
    {
        // Act
        var result = StatusHelper.GetCategoryDisplay(category);

        // Assert
        result.Should().Be(expected);
    }
}
