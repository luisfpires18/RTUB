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
    [InlineData(RequestStatus.Pending, "bg-warning text-dark")]
    [InlineData(RequestStatus.Analysing, "bg-info text-dark")]
    [InlineData(RequestStatus.Confirmed, "bg-success")]
    [InlineData(RequestStatus.Rejected, "bg-danger")]
    public void GetStatusBadgeClass_ReturnsCorrectClass(RequestStatus status, string expected)
    {
        // Act
        var result = StatusHelper.GetStatusBadgeClass(status);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(RequestStatus.Pending, "Pendente")]
    [InlineData(RequestStatus.Analysing, "Em Análise")]
    [InlineData(RequestStatus.Confirmed, "Confirmado")]
    [InlineData(RequestStatus.Rejected, "Rejeitado")]
    public void GetStatusTranslation_ReturnsPortugueseTranslation(RequestStatus status, string expected)
    {
        // Act
        var result = StatusHelper.GetStatusTranslation(status);

        // Assert
        result.Should().Be(expected);
    }

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

    [Theory]
    [InlineData(MemberCategory.Tuno, "badge-tuno")]
    [InlineData(MemberCategory.Veterano, "badge-veterano")]
    [InlineData(MemberCategory.Tunossauro, "badge-tunossauro")]
    [InlineData(MemberCategory.Fundador, "badge-fundador")]
    [InlineData(MemberCategory.Caloiro, "badge-caloiro")]
    public void GetCategoryBadgeClass_ReturnsCorrectClass(MemberCategory category, string expected)
    {
        // Act
        var result = StatusHelper.GetCategoryBadgeClass(category);

        // Assert
        result.Should().Be(expected);
    }

}
