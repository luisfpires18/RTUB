using FluentAssertions;
using RTUB.Core.Enums;

namespace RTUB.Core.Tests.Enums;

/// <summary>
/// Unit tests for MemberCategory enum
/// Tests category progression logic
/// </summary>
public class MemberCategoryTests
{
    [Fact]
    public void MemberCategory_AllValuesAreDefined()
    {
        // Arrange
        var expectedValues = new[]
        {
            MemberCategory.Tuno,
            MemberCategory.Veterano,
            MemberCategory.Tunossauro,
            MemberCategory.TunoHonorario,
            MemberCategory.Fundador,
            MemberCategory.Caloiro,
            MemberCategory.Leitao
        };

        // Act
        var actualValues = Enum.GetValues<MemberCategory>();

        // Assert
        actualValues.Should().BeEquivalentTo(expectedValues);
    }

    [Theory]
    [InlineData("Tuno", MemberCategory.Tuno)]
    [InlineData("Veterano", MemberCategory.Veterano)]
    [InlineData("Tunossauro", MemberCategory.Tunossauro)]
    [InlineData("TunoHonorario", MemberCategory.TunoHonorario)]
    [InlineData("Fundador", MemberCategory.Fundador)]
    [InlineData("Caloiro", MemberCategory.Caloiro)]
    [InlineData("Leitao", MemberCategory.Leitao)]
    public void MemberCategory_CanParse(string value, MemberCategory expected)
    {
        // Act
        var result = Enum.Parse<MemberCategory>(value);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void MemberCategory_Count_IsSeven()
    {
        // Act
        var count = Enum.GetValues<MemberCategory>().Length;

        // Assert
        count.Should().Be(7);
    }
}
