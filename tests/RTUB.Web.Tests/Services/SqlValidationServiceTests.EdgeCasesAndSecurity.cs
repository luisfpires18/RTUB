// SQL validation edge cases and security
using FluentAssertions;
using RTUB.Application.Interfaces;
using RTUB.Web.Services;
using Xunit;

namespace RTUB.Web.Tests.Services;

public partial class SqlValidationServiceTests
{
    #region SQL Validation Tests - Edge Cases

    [Fact]
    public void ValidateSqlQuery_SelectUpdatedAt_ShouldBeValid()
    {
        // Arrange - "UpdatedAt" contains "Update" but word boundary regex (\\bUPDATE\\b) correctly distinguishes it
        var query = "SELECT UpdatedAt FROM Events";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        // Word boundary matching ensures 'UpdatedAt' doesn't match the 'UPDATE' keyword
        result.IsValid.Should().BeTrue("SELECT UpdatedAt should be valid - word boundary regex correctly distinguishes 'UpdatedAt' from 'UPDATE'");
    }

    [Fact]
    public void ValidateSqlQuery_SelectCreatedBy_ShouldBeValid()
    {
        // Arrange - "CreatedBy" contains "Create" but word boundary regex correctly distinguishes it
        var query = "SELECT CreatedBy FROM Events";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("SELECT CreatedBy should be valid - word boundary regex correctly distinguishes 'CreatedBy' from 'CREATE'");
    }

    [Fact]
    public void ValidateSqlQuery_SelectDeletedAt_ShouldBeValid()
    {
        // Arrange - "DeletedAt" contains "Delete" but word boundary regex (\\bDELETE\\b) correctly distinguishes it
        var query = "SELECT DeletedAt FROM Events WHERE DeletedAt IS NOT NULL";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        // Word boundary matching ensures 'DeletedAt' doesn't match the 'DELETE' keyword
        result.IsValid.Should().BeTrue("SELECT DeletedAt should be valid - word boundary regex correctly distinguishes 'DeletedAt' from 'DELETE'");
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithUnionSelect_ShouldBeValid()
    {
        // Arrange
        var query = "SELECT Name FROM Events UNION SELECT Title FROM Albums";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("SELECT with UNION SELECT should be valid");
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithCTE_ShouldBeInvalid()
    {
        // Arrange - CTE starts with WITH, not SELECT
        var query = "WITH cte AS (SELECT * FROM Events) SELECT * FROM cte";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeFalse("Query starting with WITH (CTE) should be invalid as it doesn't start with SELECT");
    }

    #endregion

    #region Security Tests

    [Fact]
    public void ValidateSqlQuery_SqlInjection_UnionBased_ShouldBeValid()
    {
        // Arrange - UNION-based injection is okay if it's just SELECT
        var query = "SELECT * FROM Events WHERE Id = 1 UNION SELECT * FROM Users";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        // This is technically valid SQL and read-only, so it passes validation
        // The authorization (Owner role) is the primary security control
        result.IsValid.Should().BeTrue("UNION SELECT is still read-only");
    }

    [Fact]
    public void ValidateSqlQuery_MultipleStatements_WithDangerousKeyword_ShouldBeRejected()
    {
        // Arrange
        var query = "SELECT 1; DELETE FROM Events; SELECT 2";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeFalse("Multiple statements with DELETE should be rejected");
    }

    [Theory]
    [InlineData("SELECT * FROM Events WHERE Name LIKE '%INSERT%'")]
    [InlineData("SELECT * FROM Events WHERE Description LIKE '%UPDATE%'")]
    [InlineData("SELECT * FROM Events WHERE Note LIKE '%DELETE%'")]
    public void ValidateSqlQuery_DangerousKeywordInLike_ShouldBeRejected(string query)
    {
        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        // These contain dangerous keywords, even in LIKE clauses, so they're rejected for safety
        result.IsValid.Should().BeFalse("Queries with dangerous keywords even in LIKE clauses should be rejected");
    }

    [Theory]
    [InlineData("SELECT * FROM Events;INSERT INTO Events (Name) VALUES ('x')")]
    [InlineData("SELECT * FROM Events;DELETE FROM Events")]
    [InlineData("SELECT * FROM Events;UPDATE Events SET Name = 'x'")]
    public void ValidateSqlQuery_SemicolonWithDangerousKeyword_ShouldBeRejected(string query)
    {
        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        // Queries with dangerous keywords after semicolon should be rejected
        result.IsValid.Should().BeFalse("Queries with dangerous keywords after semicolon should be rejected");
    }

    [Theory]
    [InlineData("SELECT * FROM Events WHERE Id = (DELETE FROM Events)")]
    [InlineData("SELECT * FROM Events WHERE Id IN (INSERT INTO Events VALUES (1))")]
    public void ValidateSqlQuery_ParenthesesWithDangerousKeyword_ShouldBeRejected(string query)
    {
        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        // Queries with dangerous keywords after parentheses should be rejected
        result.IsValid.Should().BeFalse("Queries with dangerous keywords after parentheses should be rejected");
    }

    #endregion
}
