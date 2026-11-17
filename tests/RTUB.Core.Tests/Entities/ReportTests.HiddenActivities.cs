// Report financials that exclude hidden activities
using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public partial class ReportTests
{
    [Fact]
    public void ComputedFinancials_ExcludesHiddenActivities_BancoFromCalculations()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        var testDate = new DateTime(2023, 6, 15);

        // Add regular activity
        var regularActivity = Activity.Create(1, "Regular Activity", testDate);
        regularActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Regular Income", "Cat", 1000m, "Income", 1));
        regularActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Regular Expense", "Cat", 200m, "Expense", 1));

        // Add hidden BANCO activity - should be excluded from totals
        var bancoActivity = Activity.Create(1, "DINHEIRO NO BANCO", testDate);
        bancoActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Bank Balance", "Saldo", 5000m, "Income", 1));

        report.Activities.Add(regularActivity);
        report.Activities.Add(bancoActivity);

        // Assert - BANCO activity should NOT be included in totals
        report.TotalIncome.Should().Be(1000m); // Only regular income
        report.TotalExpenses.Should().Be(200m); // Only regular expense
        report.FinalBalance.Should().Be(800m); // 1000 - 200 = 800
    }

    [Fact]
    public void ComputedFinancials_IncludesCaixaActivity_InCalculations()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        var testDate = new DateTime(2023, 6, 15);

        // Add regular activity
        var regularActivity = Activity.Create(1, "Regular Activity", testDate);
        regularActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Regular Income", "Cat", 2000m, "Income", 1));
        regularActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Regular Expense", "Cat", 500m, "Expense", 1));

        // Add CAIXA activity - should be INCLUDED in totals (not hidden)
        var caixaActivity = Activity.Create(1, "DINHEIRO EM CAIXA", testDate);
        caixaActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Cash Balance", "Saldo", 3000m, "Income", 1));

        report.Activities.Add(regularActivity);
        report.Activities.Add(caixaActivity);

        // Assert - CAIXA activity SHOULD be included in totals
        report.TotalIncome.Should().Be(5000m); // 2000 + 3000
        report.TotalExpenses.Should().Be(500m); // Only regular expense
        report.FinalBalance.Should().Be(4500m); // 5000 - 500 = 4500
    }

    [Fact]
    public void ComputedFinancials_ExcludesHiddenActivities_CalotesFromCalculations()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        var testDate = new DateTime(2023, 6, 15);

        // Add regular activity
        var regularActivity = Activity.Create(1, "Regular Activity", testDate);
        regularActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Regular Income", "Cat", 1500m, "Income", 1));
        regularActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Regular Expense", "Cat", 300m, "Expense", 1));

        // Add hidden CALOTES activity - should be excluded from totals
        var calotesActivity = Activity.Create(1, "CALOTES 2023", testDate);
        calotesActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Calote", "Debt", 1000m, "Income", 1));

        report.Activities.Add(regularActivity);
        report.Activities.Add(calotesActivity);

        // Assert - CALOTES activity should NOT be included in totals
        report.TotalIncome.Should().Be(1500m); // Only regular income
        report.TotalExpenses.Should().Be(300m); // Only regular expense
        report.FinalBalance.Should().Be(1200m); // 1500 - 300 = 1200
    }

    [Fact]
    public void ComputedFinancials_ExcludesAllHiddenActivities_FromCalculations()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        var testDate = new DateTime(2023, 6, 15);

        // Add regular activities
        var activity1 = Activity.Create(1, "Event 1", testDate);
        activity1.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Income 1", "Cat", 1000m, "Income", 1));
        activity1.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Expense 1", "Cat", 400m, "Expense", 1));

        var activity2 = Activity.Create(1, "Event 2", testDate.AddMonths(1));
        activity2.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Income 2", "Cat", 500m, "Income", 1));
        activity2.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Expense 2", "Cat", 100m, "Expense", 1));

        // Add hidden activities (BANCO and CALOTES are excluded, CAIXA is included)
        var bancoActivity = Activity.Create(1, "DINHEIRO NO BANCO", testDate);
        bancoActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Bank", "Saldo", 10000m, "Income", 1));

        var caixaActivity = Activity.Create(1, "DINHEIRO EM CAIXA", testDate);
        caixaActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Cash", "Saldo", 5000m, "Income", 1));

        var calotesActivity = Activity.Create(1, "CALOTES", testDate);
        calotesActivity.Transactions.Add(Transaction.Create(DateTime.UtcNow, "Debts", "Debt", 2000m, "Income", 1));

        report.Activities.Add(activity1);
        report.Activities.Add(activity2);
        report.Activities.Add(bancoActivity);
        report.Activities.Add(caixaActivity);
        report.Activities.Add(calotesActivity);

        // Assert - BANCO and CALOTES excluded, CAIXA is included
        report.TotalIncome.Should().Be(6500m); // 1000 + 500 + 5000 (CAIXA included)
        report.TotalExpenses.Should().Be(500m); // 400 + 100
        report.FinalBalance.Should().Be(6000m); // 6500 - 500
    }
}
