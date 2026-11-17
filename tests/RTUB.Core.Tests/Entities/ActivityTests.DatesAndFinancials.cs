// Activity end date, latest date, financials and hidden flags
using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public partial class ActivityTests
{
    [Fact]
    public void SetEndDate_WithValidDate_ShouldSetEndDate()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity", TestDate);
        var endDate = TestDate.AddDays(5);

        // Act
        activity.SetEndDate(endDate);

        // Assert
        activity.EndDate.Should().Be(endDate);
    }

    [Fact]
    public void SetEndDate_WithDateBeforeStartDate_ShouldThrowException()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity", TestDate);
        var endDate = TestDate.AddDays(-1);

        // Act
        var act = () => activity.SetEndDate(endDate);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*data de fim*");
    }

    [Fact]
    public void LatestDate_WithNoEndDate_ShouldReturnStartDate()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity", TestDate);

        // Assert
        activity.LatestDate.Should().Be(TestDate);
    }

    [Fact]
    public void LatestDate_WithEndDate_ShouldReturnEndDate()
    {
        // Arrange
        var endDate = TestDate.AddDays(5);
        var activity = Activity.Create(1, "Activity", TestDate, null, endDate);

        // Assert
        activity.LatestDate.Should().Be(endDate);
    }

    [Fact]
    public void ComputedFinancials_WithNoTransactions_ShouldHaveZeroValues()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity", TestDate);

        // Assert - computed properties work immediately
        activity.TotalIncome.Should().Be(0);
        activity.TotalExpenses.Should().Be(0);
        activity.Balance.Should().Be(0);
    }

    [Fact]
    public void ComputedFinancials_WithTransactions_ShouldCalculateCorrectly()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity", TestDate);
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

        // Assert - computed properties calculate on access
        activity.TotalIncome.Should().Be(150.00m);
        activity.TotalExpenses.Should().Be(50.00m);
        activity.Balance.Should().Be(100.00m);
    }

    [Fact]
    public void ComputedFinancials_WithOnlyIncome_ShouldCalculateCorrectly()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity", TestDate);
        activity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Income", "Cat", 200.00m, "Income", 1));

        // Assert - computed properties calculate on access
        activity.TotalIncome.Should().Be(200.00m);
        activity.TotalExpenses.Should().Be(0);
        activity.Balance.Should().Be(200.00m);
    }

    [Fact]
    public void ComputedFinancials_WithOnlyExpenses_ShouldCalculateCorrectly()
    {
        // Arrange
        var activity = Activity.Create(1, "Activity", TestDate);
        activity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Expense", "Cat", 75.00m, "Expense", 1));

        // Assert - computed properties calculate on access
        activity.TotalIncome.Should().Be(0);
        activity.TotalExpenses.Should().Be(75.00m);
        activity.Balance.Should().Be(-75.00m);
    }

    [Theory]
    [InlineData("DINHEIRO NO BANCO", true)]
    [InlineData("DINHEIRO EM CAIXA", false)]  // CAIXA should be included in calculations
    [InlineData("CALOTES 2024", true)]
    [InlineData("Dinheiro no Banco", true)]
    [InlineData("Dinheiro em Caixa", false)]  // CAIXA should be included in calculations
    [InlineData("calotes", true)]
    [InlineData("BANCO DE PORTUGAL", true)]
    [InlineData("Caixa de Natal", false)]  // CAIXA should be included in calculations
    [InlineData("Regular Event", false)]
    [InlineData("Concerto de Natal", false)]
    [InlineData("Ensaio Geral", false)]
    public void IsHiddenFromCalculations_ShouldCorrectlyIdentifyHiddenActivities(string activityName, bool expectedHidden)
    {
        // Arrange
        var activity = Activity.Create(1, activityName, TestDate);

        // Act
        var isHidden = activity.IsHiddenFromCalculations();

        // Assert
        isHidden.Should().Be(expectedHidden, $"Activity '{activityName}' should{(expectedHidden ? "" : " not")} be hidden from calculations");
    }

    [Theory]
    [InlineData("DINHEIRO NO BANCO", true)]
    [InlineData("DINHEIRO EM CAIXA", true)]  // CAIXA should be hidden from activity list
    [InlineData("CALOTES 2024", true)]
    [InlineData("Dinheiro no Banco", true)]
    [InlineData("Dinheiro em Caixa", true)]  // CAIXA should be hidden from activity list
    [InlineData("calotes", true)]
    [InlineData("BANCO DE PORTUGAL", true)]
    [InlineData("Caixa de Natal", true)]  // CAIXA should be hidden from activity list
    [InlineData("Regular Event", false)]
    [InlineData("Concerto de Natal", false)]
    [InlineData("Ensaio Geral", false)]
    public void IsHiddenFromActivityList_ShouldCorrectlyIdentifyHiddenActivities(string activityName, bool expectedHidden)
    {
        // Arrange
        var activity = Activity.Create(1, activityName, TestDate);

        // Act
        var isHidden = activity.IsHiddenFromActivityList();

        // Assert
        isHidden.Should().Be(expectedHidden, $"Activity '{activityName}' should{(expectedHidden ? "" : " not")} be hidden from activity list");
    }
}
