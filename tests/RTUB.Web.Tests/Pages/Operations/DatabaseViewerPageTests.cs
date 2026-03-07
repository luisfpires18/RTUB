using FluentAssertions;
using RTUB.Application.Interfaces;
using RTUB.Web.Services;
using Xunit;

namespace RTUB.Web.Tests.Pages.Operations;

/// <summary>
/// Unit tests for DatabaseViewer page (/owner/db)
/// Testing SQL validation logic for read-only queries
/// </summary>
public class DatabaseViewerPageTests
{
    private readonly ISqlValidationService _sqlValidationService;

    public DatabaseViewerPageTests()
    {
        _sqlValidationService = new SqlValidationService();
    }

    #region SQL Validation Tests - Valid Queries

    [Fact]
    public void ValidateSqlQuery_SimpleSelect_ShouldBeValid()
    {
        // Arrange
        var query = "SELECT * FROM Events";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("Simple SELECT queries should be valid");
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithWhereClause_ShouldBeValid()
    {
        // Arrange
        var query = "SELECT Id, Name FROM Events WHERE Id > 10";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("SELECT with WHERE clause should be valid");
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithJoin_ShouldBeValid()
    {
        // Arrange
        var query = "SELECT e.Name, a.Name FROM Events e JOIN Albums a ON e.Id = a.EventId";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("SELECT with JOIN should be valid");
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithOrderBy_ShouldBeValid()
    {
        // Arrange
        var query = "SELECT * FROM Events ORDER BY CreatedAt DESC";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("SELECT with ORDER BY should be valid");
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithGroupBy_ShouldBeValid()
    {
        // Arrange
        var query = "SELECT Status, COUNT(*) FROM Events GROUP BY Status";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("SELECT with GROUP BY should be valid");
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithLimitOffset_ShouldBeValid()
    {
        // Arrange
        var query = "SELECT * FROM Events LIMIT 10 OFFSET 20";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("SELECT with LIMIT OFFSET should be valid");
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSqlQuery_SelectLowerCase_ShouldBeValid()
    {
        // Arrange
        var query = "select * from events";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("Lowercase SELECT should be valid");
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSqlQuery_SelectMixedCase_ShouldBeValid()
    {
        // Arrange
        var query = "SeLeCt * FrOm Events";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("Mixed case SELECT should be valid");
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithLeadingWhitespace_ShouldBeValid()
    {
        // Arrange
        var query = "   SELECT * FROM Events";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("SELECT with leading whitespace should be valid");
        result.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithSubquery_ShouldBeValid()
    {
        // Arrange
        var query = "SELECT * FROM Events WHERE Id IN (SELECT EventId FROM Enrollments)";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeTrue("SELECT with subquery should be valid");
        result.ErrorMessage.Should().BeEmpty();
    }

    #endregion

    #region SQL Validation Tests - Invalid Queries (Empty/Non-SELECT)

    [Fact]
    public void ValidateSqlQuery_EmptyQuery_ShouldBeInvalid()
    {
        // Arrange
        var query = "";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeFalse("Empty query should be invalid");
        result.ErrorMessage.Should().Contain("cannot be empty");
    }

    [Fact]
    public void ValidateSqlQuery_WhitespaceOnly_ShouldBeInvalid()
    {
        // Arrange
        var query = "   ";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeFalse("Whitespace-only query should be invalid");
        result.ErrorMessage.Should().Contain("cannot be empty");
    }

    [Fact]
    public void ValidateSqlQuery_NullQuery_ShouldBeInvalid()
    {
        // Arrange
        string? query = null;

        // Act
        var result = _sqlValidationService.ValidateQuery(query!);

        // Assert
        result.IsValid.Should().BeFalse("Null query should be invalid");
    }

    [Fact]
    public void ValidateSqlQuery_NonSelectQuery_ShouldBeInvalid()
    {
        // Arrange
        var query = "SHOW TABLES";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeFalse("Non-SELECT query should be invalid");
        result.ErrorMessage.Should().Contain("SELECT");
    }

    #endregion

    #region SQL Validation Tests - Dangerous Keywords (Non-SELECT queries)

    [Theory]
    [InlineData("INSERT INTO Events (Name) VALUES ('Test')")]
    [InlineData("UPDATE Events SET Name = 'Test' WHERE Id = 1")]
    [InlineData("DELETE FROM Events WHERE Id = 1")]
    [InlineData("MERGE INTO Events")]
    [InlineData("DROP TABLE Events")]
    [InlineData("ALTER TABLE Events ADD Column")]
    [InlineData("TRUNCATE TABLE Events")]
    [InlineData("EXEC sp_execute")]
    [InlineData("EXECUTE sp_something")]
    [InlineData("CREATE TABLE NewTable")]
    [InlineData("GRANT SELECT ON Events TO user")]
    [InlineData("REVOKE SELECT ON Events FROM user")]
    public void ValidateSqlQuery_NonSelectDangerousQuery_ShouldBeRejected(string query)
    {
        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        // These queries don't start with SELECT, so they should be rejected
        result.IsValid.Should().BeFalse("Non-SELECT query should be rejected");
    }

    [Theory]
    [InlineData("SELECT * FROM Events; INSERT INTO Events (Name) VALUES ('Test')", "INSERT")]
    [InlineData("SELECT * FROM Events; UPDATE Events SET Name = 'Test' WHERE Id = 1", "UPDATE")]
    [InlineData("SELECT * FROM Events; DELETE FROM Events WHERE Id = 1", "DELETE")]
    [InlineData("SELECT * FROM Events; DROP TABLE Events", "DROP")]
    [InlineData("SELECT * FROM Events; TRUNCATE TABLE Events", "TRUNCATE")]
    [InlineData("SELECT * FROM Events; EXEC sp_execute", "EXEC")]
    [InlineData("SELECT * FROM Events; EXECUTE sp_something", "EXECUTE")]
    [InlineData("SELECT * FROM Events; CREATE TABLE NewTable (Id INT)", "CREATE")]
    [InlineData("SELECT * FROM Events; GRANT SELECT ON Events TO user", "GRANT")]
    [InlineData("SELECT * FROM Events; REVOKE SELECT ON Events FROM user", "REVOKE")]
    [InlineData("SELECT * FROM Events; MERGE INTO Events USING src ON 1=1", "MERGE")]
    [InlineData("SELECT * FROM Events; ALTER TABLE Events ADD Column INT", "ALTER")]
    public void ValidateSqlQuery_SelectWithDangerousKeyword_ShouldBeRejected(string query, string expectedKeyword)
    {
        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeFalse($"SELECT query containing {expectedKeyword} keyword should be rejected");
        result.ErrorMessage.Should().Contain(expectedKeyword);
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithInsertInComment_ShouldBeInvalid()
    {
        // This is a tricky case - even if INSERT is in a comment-like structure,
        // we should reject it for safety
        // Arrange
        var query = "SELECT * FROM Events -- INSERT INTO Events";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        // Note: This query contains INSERT keyword so it should be rejected
        result.IsValid.Should().BeFalse("Query containing INSERT even in comment should be rejected for safety");
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithDeleteInString_ShouldBeInvalid()
    {
        // Even if DELETE appears as a string value, we reject for safety
        // Arrange
        var query = "SELECT * FROM Events WHERE Name = 'DELETE'";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        // This will be rejected because DELETE is found in the query
        result.IsValid.Should().BeFalse("Query containing DELETE even as string literal should be rejected for safety");
    }

    [Fact]
    public void ValidateSqlQuery_InsertLowerCase_ShouldBeRejected()
    {
        // Arrange
        var query = "insert into Events (Name) VALUES ('Test')";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeFalse("Lowercase INSERT should be rejected");
    }

    [Fact]
    public void ValidateSqlQuery_UpdateMixedCase_ShouldBeRejected()
    {
        // Arrange
        var query = "UpDaTe Events SET Name = 'Test'";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeFalse("Mixed case UPDATE should be rejected");
    }

    [Fact]
    public void ValidateSqlQuery_SelectFollowedByUpdate_ShouldBeRejected()
    {
        // Arrange - SQL injection attempt
        var query = "SELECT * FROM Events; UPDATE Events SET Name = 'Hacked'";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeFalse("SELECT followed by UPDATE should be rejected");
        result.ErrorMessage.Should().Contain("UPDATE");
    }

    [Fact]
    public void ValidateSqlQuery_SelectWithDropTable_ShouldBeRejected()
    {
        // Arrange - SQL injection attempt
        var query = "SELECT * FROM Events; DROP TABLE Events";

        // Act
        var result = _sqlValidationService.ValidateQuery(query);

        // Assert
        result.IsValid.Should().BeFalse("SELECT with DROP TABLE should be rejected");
        result.ErrorMessage.Should().Contain("DROP");
    }

    #endregion

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
