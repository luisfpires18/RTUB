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
    [InlineData(RequestStatus.Pending, "pendente")]
    [InlineData(RequestStatus.Analysing, "em análise")]
    [InlineData(RequestStatus.Confirmed, "confirmado")]
    [InlineData(RequestStatus.Rejected, "rejeitado")]
    public void GetFilterStatusText_ReturnsLowercaseText(RequestStatus status, string expected)
    {
        // Act
        var result = StatusHelper.GetFilterStatusText(status);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetFilterStatusText_WithNull_ReturnsEmpty()
    {
        // Act
        var result = StatusHelper.GetFilterStatusText(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(InstrumentType.Guitarra, "Guitarra")]
    [InlineData(InstrumentType.Bandolim, "Bandolim")]
    [InlineData(InstrumentType.Cavaquinho, "Cavaquinho")]
    [InlineData(InstrumentType.Acordeao, "Acordeão")]
    [InlineData(InstrumentType.Percussao, "Percussão")]
    public void GetInstrumentDisplay_ReturnsCorrectName(InstrumentType instrument, string expected)
    {
        // Act
        var result = StatusHelper.GetInstrumentDisplay(instrument);

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

    [Fact]
    public void GetDisplayCategories_WithTunoLessThan2Years_ReturnsOnlyTuno()
    {
        // Arrange
        var categories = new List<MemberCategory> { MemberCategory.Tuno };
        var yearTuno = DateTime.Now.Year; // Started this year

        // Act
        var result = StatusHelper.GetDisplayCategories(categories, yearTuno);

        // Assert
        result.Should().Contain(MemberCategory.Tuno);
        result.Should().NotContain(MemberCategory.Veterano);
        result.Should().NotContain(MemberCategory.Tunossauro);
    }

    [Fact]
    public void GetDisplayCategories_WithTuno2To5Years_AddsVeterano()
    {
        // Arrange
        var categories = new List<MemberCategory> { MemberCategory.Tuno };
        var yearTuno = DateTime.Now.Year - 3; // Started 3 years ago

        // Act
        var result = StatusHelper.GetDisplayCategories(categories, yearTuno);

        // Assert
        result.Should().Contain(MemberCategory.Tuno);
        result.Should().Contain(MemberCategory.Veterano);
        result.Should().NotContain(MemberCategory.Tunossauro);
    }

    [Fact]
    public void GetDisplayCategories_WithTuno6PlusYears_AddsBothVeteranoAndTunossauro()
    {
        // Arrange
        var categories = new List<MemberCategory> { MemberCategory.Tuno };
        var yearTuno = DateTime.Now.Year - 7; // Started 7 years ago

        // Act
        var result = StatusHelper.GetDisplayCategories(categories, yearTuno);

        // Assert
        result.Should().Contain(MemberCategory.Tuno);
        result.Should().Contain(MemberCategory.Veterano);
        result.Should().Contain(MemberCategory.Tunossauro);
    }

    [Fact]
    public void GetDisplayCategories_WithoutTunoCategory_ReturnsOriginalCategories()
    {
        // Arrange
        var categories = new List<MemberCategory> { MemberCategory.Caloiro };
        var yearTuno = DateTime.Now.Year - 5;

        // Act
        var result = StatusHelper.GetDisplayCategories(categories, yearTuno);

        // Assert
        result.Should().BeEquivalentTo(categories);
        result.Should().NotContain(MemberCategory.Veterano);
        result.Should().NotContain(MemberCategory.Tunossauro);
    }
}
