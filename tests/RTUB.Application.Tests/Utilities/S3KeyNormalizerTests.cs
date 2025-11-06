using FluentAssertions;
using RTUB.Application.Utilities;
using Xunit;

namespace RTUB.Application.Tests.Utilities;

/// <summary>
/// Tests for S3KeyNormalizer utility
/// </summary>
public class S3KeyNormalizerTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("Simple", "simple")]
    [InlineData("Simple Text", "simple_text")]
    [InlineData("Boémios e Trovadores", "boemios_e_trovadores")]
    [InlineData("01. Test Album", "01_test_album")]
    [InlineData("Test & Album", "test_album")]
    [InlineData("Test / Song", "test_song")]
    [InlineData("Álbum com Acentos", "album_com_acentos")]
    [InlineData("Canção Especial", "cancao_especial")]
    [InlineData("  Leading and Trailing  ", "leading_and_trailing")]
    [InlineData("Multiple___Underscores", "multiple_underscores")]
    [InlineData("Special@#$%Characters", "special_characters")]
    [InlineData("Números 123", "numeros_123")]
    public void NormalizeForS3Key_ReturnsExpectedResult(string input, string expected)
    {
        // Act
        var result = S3KeyNormalizer.NormalizeForS3Key(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeForS3Key_WithNull_ReturnsEmptyString()
    {
        // Act
        var result = S3KeyNormalizer.NormalizeForS3Key(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("___leading", "leading")]
    [InlineData("trailing___", "trailing")]
    [InlineData("___both___", "both")]
    public void NormalizeForS3Key_TrimsUnderscores(string input, string expected)
    {
        // Act
        var result = S3KeyNormalizer.NormalizeForS3Key(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Test 01. Album Name", "test_01_album_name")]
    [InlineData("02. Song Title", "02_song_title")]
    [InlineData("Track #3", "track_3")]
    public void NormalizeForS3Key_PreservesNumbers(string input, string expected)
    {
        // Act
        var result = S3KeyNormalizer.NormalizeForS3Key(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("àáâãäå", "aaaaaa")]
    [InlineData("èéêë", "eeee")]
    [InlineData("ìíîï", "iiii")]
    [InlineData("òóôõö", "ooooo")]
    [InlineData("ùúûü", "uuuu")]
    [InlineData("ñ", "n")]
    [InlineData("ç", "c")]
    public void NormalizeForS3Key_RemovesAccents(string input, string expected)
    {
        // Act
        var result = S3KeyNormalizer.NormalizeForS3Key(input);

        // Assert
        result.Should().Be(expected);
    }
}
