using FluentAssertions;
using RTUB.Application.Interfaces;
using RTUB.Web.Services;

namespace RTUB.Web.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SqlValidationService"/>, the SQL rules of the Owner's database tool (/owner/db, React since
/// task 035; were the Blazor page's tests)
/// </summary>
public partial class SqlValidationServiceTests
{
    private readonly ISqlValidationService _sqlValidationService;

    public SqlValidationServiceTests()
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
}
