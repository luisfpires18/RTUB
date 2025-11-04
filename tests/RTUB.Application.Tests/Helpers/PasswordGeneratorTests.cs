using FluentAssertions;
using RTUB.Application.Helpers;
using System.Text.RegularExpressions;

namespace RTUB.Application.Tests.Helpers;

/// <summary>
/// Unit tests for PasswordGenerator
/// Tests password generation requirements
/// </summary>
public class PasswordGeneratorTests
{
    [Fact]
    public void GeneratePassword_ReturnsPasswordWithCorrectLength()
    {
        // Act
        var password = PasswordGenerator.GeneratePassword();

        // Assert
        password.Should().HaveLength(10);
    }

    [Fact]
    public void GeneratePassword_ContainsUppercaseLetter()
    {
        // Act
        var password = PasswordGenerator.GeneratePassword();

        // Assert
        password.Should().MatchRegex("[A-Z]", "password should contain at least one uppercase letter");
    }

    [Fact]
    public void GeneratePassword_ContainsLowercaseLetter()
    {
        // Act
        var password = PasswordGenerator.GeneratePassword();

        // Assert
        password.Should().MatchRegex("[a-z]", "password should contain at least one lowercase letter");
    }

    [Fact]
    public void GeneratePassword_ContainsDigit()
    {
        // Act
        var password = PasswordGenerator.GeneratePassword();

        // Assert
        password.Should().MatchRegex("[0-9]", "password should contain at least one digit");
    }

    [Fact]
    public void GeneratePassword_ContainsSpecialCharacter()
    {
        // Act
        var password = PasswordGenerator.GeneratePassword();

        // Assert
        password.Should().MatchRegex(@"[!@#$%^&*]", "password should contain at least one special character");
    }

    [Fact]
    public void GeneratePassword_GeneratesDifferentPasswords()
    {
        // Act
        var password1 = PasswordGenerator.GeneratePassword();
        var password2 = PasswordGenerator.GeneratePassword();
        var password3 = PasswordGenerator.GeneratePassword();

        // Assert
        password1.Should().NotBe(password2, "passwords should be random");
        password2.Should().NotBe(password3, "passwords should be random");
        password1.Should().NotBe(password3, "passwords should be random");
    }

    [Fact]
    public void GeneratePassword_MeetsAllRequirements()
    {
        // Act - generate multiple passwords to ensure consistency
        for (int i = 0; i < 10; i++)
        {
            var password = PasswordGenerator.GeneratePassword();

            // Assert
            password.Should().HaveLength(10);
            password.Should().MatchRegex("[A-Z]", "password should contain uppercase");
            password.Should().MatchRegex("[a-z]", "password should contain lowercase");
            password.Should().MatchRegex("[0-9]", "password should contain digit");
            password.Should().MatchRegex(@"[!@#$%^&*]", "password should contain special character");
        }
    }
}
