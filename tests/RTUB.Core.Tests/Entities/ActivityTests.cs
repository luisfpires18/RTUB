using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class ActivityTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateInstance()
    {
        // Arrange
        var reportId = 1;
        var name = "Concerto de Natal";
        var description = "Evento anual de Natal";

        // Act
        var activity = Activity.Create(reportId, name, description);

        // Assert
        activity.Should().NotBeNull();
        activity.ReportId.Should().Be(reportId);
        activity.Name.Should().Be(name);
        activity.Description.Should().Be(description);
        activity.TotalIncome.Should().Be(0);
        activity.TotalExpenses.Should().Be(0);
        activity.Balance.Should().Be(0);
    }

    [Fact]
    public void Create_WithoutDescription_ShouldCreateInstance()
    {
        // Arrange
        var reportId = 1;
        var name = "Concerto de Páscoa";

        // Act
        var activity = Activity.Create(reportId, name);

        // Assert
        activity.Should().NotBeNull();
        activity.ReportId.Should().Be(reportId);
        activity.Name.Should().Be(name);
        activity.Description.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyName_ShouldThrowException(string? emptyName)
    {
        // Arrange
        var reportId = 1;

        // Act
        var act = () => Activity.Create(reportId, emptyName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Activity name cannot be empty*");
    }

    [Fact]
    public void UpdateDetails_WithValidData_ShouldUpdateProperties()
    {
        // Arrange
        var activity = Activity.Create(1, "Old Name", "Old Description");
        var newName = "New Name";
        var newDescription = "New Description";

        // Act
        activity.UpdateDetails(newName, newDescription);

        // Assert
        activity.Name.Should().Be(newName);
        activity.Description.Should().Be(newDescription);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void UpdateDetails_WithEmptyName_ShouldThrowException(string? emptyName)
    {
        // Arrange
        var activity = Activity.Create(1, "Valid Name");

        // Act
        var act = () => activity.UpdateDetails(emptyName!, "desc");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Activity name cannot be empty*");
    }

    [Fact]
    public void RecalculateFinancials_WithNoTransactions_ShouldHaveZeroValues()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity");

        // Act
        activity.RecalculateFinancials();

        // Assert
        activity.TotalIncome.Should().Be(0);
        activity.TotalExpenses.Should().Be(0);
        activity.Balance.Should().Be(0);
    }

    [Fact]
    public void RecalculateFinancials_WithTransactions_ShouldCalculateCorrectly()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity");
        var date = DateTime.UtcNow;
        var transactions = new List<Transaction>
        {
            Transaction.Create(date, "Income 1", "Category1", 100.00m, "Income", 1),
            Transaction.Create(date, "Income 2", "Category2", 50.00m, "Income", 1),
            Transaction.Create(date, "Expense 1", "Category3", 30.00m, "Expense", 1),
            Transaction.Create(date, "Expense 2", "Category4", 20.00m, "Expense", 1)
        };
        
        foreach (var transaction in transactions)
        {
            activity.Transactions.Add(transaction);
        }

        // Act
        activity.RecalculateFinancials();

        // Assert
        activity.TotalIncome.Should().Be(150.00m);
        activity.TotalExpenses.Should().Be(50.00m);
        activity.Balance.Should().Be(100.00m);
    }

    [Fact]
    public void RecalculateFinancials_WithOnlyIncome_ShouldCalculateCorrectly()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity");
        activity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Income", "Cat", 200.00m, "Income", 1));

        // Act
        activity.RecalculateFinancials();

        // Assert
        activity.TotalIncome.Should().Be(200.00m);
        activity.TotalExpenses.Should().Be(0);
        activity.Balance.Should().Be(200.00m);
    }

    [Fact]
    public void RecalculateFinancials_WithOnlyExpenses_ShouldCalculateCorrectly()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity");
        activity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Expense", "Cat", 75.00m, "Expense", 1));

        // Act
        activity.RecalculateFinancials();

        // Assert
        activity.TotalIncome.Should().Be(0);
        activity.TotalExpenses.Should().Be(75.00m);
        activity.Balance.Should().Be(-75.00m);
    }
}
