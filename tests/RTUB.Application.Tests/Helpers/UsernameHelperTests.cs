using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// Unit tests for UsernameHelper
/// Tests username normalization logic
/// </summary>
public class UsernameHelperTests
{
    [Theory]
    [InlineData("Jeans", "jeans")]
    [InlineData("Saca-Rabos", "sacarabos")]
    [InlineData("Auto Escópio", "autoescopio")]
    [InlineData("1/2 kg", "12kg")]
    [InlineData("22", "22")]
    public void NormalizeUsername_WithVariousInputs_ReturnsExpectedOutput(string input, string expected)
    {
        // Act
        var result = UsernameHelper.NormalizeUsername(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeUsername_WithNull_ReturnsEmpty()
    {
        // Act
        var result = UsernameHelper.NormalizeUsername(null!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeUsername_WithEmpty_ReturnsEmpty()
    {
        // Act
        var result = UsernameHelper.NormalizeUsername(string.Empty);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeUsername_WithWhitespace_ReturnsEmpty()
    {
        // Act
        var result = UsernameHelper.NormalizeUsername("   ");

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("José", "jose")]
    [InlineData("François", "francois")]
    [InlineData("Müller", "muller")]
    [InlineData("Señor", "senor")]
    public void NormalizeUsername_RemovesAccents(string input, string expected)
    {
        // Act
        var result = UsernameHelper.NormalizeUsername(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Hello World", "helloworld")]
    [InlineData("Test-Name", "testname")]
    [InlineData("User_123", "user123")]
    [InlineData("Name@Domain", "namedomain")]
    [InlineData("Foo!Bar#Baz", "foobarbaz")]
    public void NormalizeUsername_RemovesSpecialCharacters(string input, string expected)
    {
        // Act
        var result = UsernameHelper.NormalizeUsername(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("ABC123", "abc123")]
    [InlineData("test", "test")]
    [InlineData("UPPER", "upper")]
    public void NormalizeUsername_ConvertsToLowercase(string input, string expected)
    {
        // Act
        var result = UsernameHelper.NormalizeUsername(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("123456", "123456")]
    [InlineData("42", "42")]
    [InlineData("007", "007")]
    public void NormalizeUsername_PreservesNumbers(string input, string expected)
    {
        // Act
        var result = UsernameHelper.NormalizeUsername(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("Mix3d-N@me!", "mix3dnme")]
    [InlineData("Tést 123 (OK)", "test123ok")]
    public void NormalizeUsername_ComplexCases(string input, string expected)
    {
        // Act
        var result = UsernameHelper.NormalizeUsername(input);

        // Assert
        result.Should().Be(expected);
    }
}
