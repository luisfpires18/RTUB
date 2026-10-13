using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Validation;

/// <summary>
/// Unit tests for ApplicationUser validation attributes
/// Tests Email validation
/// </summary>
public class ApplicationUserValidationTests
{
    #region Email Validation Tests

    [Fact]
    public void Email_WithNull_ShouldFailValidation()
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "Test",
            LastName = "User",
            Nickname = "testuser",
            PhoneNumber = "912345678",
            Email = null // Invalid: Required
        };

        // Act
        var validationResults = ValidateModel(user);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("Email") && v.ErrorMessage!.Contains("obrigatório"));
    }

    [Fact]
    public void Email_WithEmptyString_ShouldFailValidation()
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "Test",
            LastName = "User",
            Nickname = "testuser",
            PhoneNumber = "912345678",
            Email = "" // Invalid: Required
        };

        // Act
        var validationResults = ValidateModel(user);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("Email") && v.ErrorMessage!.Contains("obrigatório"));
    }

    [Fact]
    public void Email_WithInvalidFormat_ShouldFailValidation()
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "Test",
            LastName = "User",
            Nickname = "testuser",
            PhoneNumber = "912345678",
            Email = "invalid-email" // Invalid: Not email format
        };

        // Act
        var validationResults = ValidateModel(user);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("Email") && v.ErrorMessage!.Contains("inválido"));
    }

    [Fact]
    public void Email_WithValidFormat_ShouldPassValidation()
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "Test",
            LastName = "User",
            Nickname = "testuser",
            PhoneNumber = "912345678",
            Email = "test@example.com" // Valid
        };

        // Act
        var validationResults = ValidateModel(user);

        // Assert
        validationResults.Should().NotContain(v => v.MemberNames.Contains("Email"));
    }

    [Theory]
    [InlineData("user@rtub.pt")]
    [InlineData("test.user@example.com")]
    [InlineData("user+tag@domain.co.uk")]
    [InlineData("user123@test-domain.org")]
    public void Email_WithVariousValidFormats_ShouldPassValidation(string email)
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "Test",
            LastName = "User",
            Nickname = "testuser",
            PhoneNumber = "912345678",
            Email = email
        };

        // Act
        var validationResults = ValidateModel(user);

        // Assert
        validationResults.Should().NotContain(v => v.MemberNames.Contains("Email"),
            $"Email '{email}' should be valid");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@")]
    [InlineData("@missing.com")]
    public void Email_WithVariousInvalidFormats_ShouldFailValidation(string email)
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "Test",
            LastName = "User",
            Nickname = "testuser",
            PhoneNumber = "912345678",
            Email = email
        };

        // Act
        var validationResults = ValidateModel(user);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("Email"),
            $"Email '{email}' should be invalid");
    }

    #endregion

    #region Combined Validation Tests

    [Fact]
    public void User_WithAllRequiredFields_PassesValidation()
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "João",
            LastName = "Silva",
            Nickname = "Joãozinho",
            PhoneNumber = "912345678",
            Email = "joao@rtub.pt",
        };

        // Act
        var validationResults = ValidateModel(user);

        // Assert
        validationResults.Should().BeEmpty("User has all required fields filled correctly");
    }

    [Fact]
    public void User_WithMissingEmail_FailsValidation()
    {
        // Arrange
        var user = new ApplicationUser
        {
            FirstName = "João",
            LastName = "Silva",
            Nickname = "Joãozinho",
            PhoneNumber = "912345678",
            Email = null, // Missing required field
        };

        // Act
        var validationResults = ValidateModel(user);

        // Assert
        validationResults.Should().HaveCount(1);
        validationResults.First().MemberNames.Should().Contain("Email");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Validates a model using DataAnnotations
    /// </summary>
    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model);
        Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);
        return validationResults;
    }

    #endregion
}
