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

        // Assert - password should be 6-10 characters (4-6 letters + 2-4 digits)
        password.Length.Should().BeInRange(6, 10);
    }

    [Fact]
    public void GeneratePassword_ContainsLettersFollowedByDigits()
    {
        // Act
        var password = PasswordGenerator.GeneratePassword();

        // Assert - password should match pattern: 4-6 lowercase letters followed by 2-4 digits
        password.Should().MatchRegex("^[a-z]{4,6}[0-9]{2,4}$", "password should be 4-6 letters followed by 2-4 digits");
    }

    [Fact]
    public void GeneratePassword_GeneratesDifferentPasswords()
    {
        // Act
        var password1 = PasswordGenerator.GeneratePassword();
        var password2 = PasswordGenerator.GeneratePassword();
        var password3 = PasswordGenerator.GeneratePassword();

        // Assert - passwords should be different (randomness test)
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
            password.Length.Should().BeInRange(6, 10);
            password.Should().MatchRegex("^[a-z]{4,6}[0-9]{2,4}$", "password should be 4-6 letters followed by 2-4 digits");
        }
    }
}
