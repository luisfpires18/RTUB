using FluentAssertions;
using RTUB.Application.Helpers;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// Unit tests for PasswordGenerator
/// Tests simple password generation for older users
/// </summary>
public class PasswordGeneratorTests
{
    [Fact]
    public void GeneratePassword_ReturnsPasswordWithCorrectLength()
    {
        // Act
        var password = PasswordGenerator.GeneratePassword();

        // Assert
        password.Should().HaveLength(4);
    }

    [Fact]
    public void GeneratePassword_ContainsOnlyDigits()
    {
        // Act
        var password = PasswordGenerator.GeneratePassword();

        // Assert
        password.Should().MatchRegex("^[0-9]+$", "password should contain only digits for easier memorization");
    }

    [Fact]
    public void GeneratePassword_GeneratesDifferentPasswords()
    {
        // Act
        var password1 = PasswordGenerator.GeneratePassword();
        var password2 = PasswordGenerator.GeneratePassword();
        var password3 = PasswordGenerator.GeneratePassword();

        // Assert - at least some passwords should be different (randomness test)
        // We can't guarantee all are different with only 4 digits, but statistically they should be
        var passwords = new[] { password1, password2, password3 };
        passwords.Distinct().Count().Should().BeGreaterThanOrEqualTo(2, "passwords should be random");
    }

    [Fact]
    public void GeneratePassword_MeetsAllRequirements()
    {
        // Act - generate multiple passwords to ensure consistency
        for (int i = 0; i < 10; i++)
        {
            var password = PasswordGenerator.GeneratePassword();

            // Assert
            password.Should().HaveLength(4);
            password.Should().MatchRegex("^[0-9]+$", "password should contain only digits");
        }
    }
}
