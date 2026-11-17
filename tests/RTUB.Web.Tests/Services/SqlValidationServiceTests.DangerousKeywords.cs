// SQL validation dangerous keywords
using FluentAssertions;
using RTUB.Application.Interfaces;
using RTUB.Web.Services;
using Xunit;

namespace RTUB.Web.Tests.Services;

public partial class SqlValidationServiceTests
{
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
}
