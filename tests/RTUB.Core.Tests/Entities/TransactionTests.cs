using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Core.Tests.Entities;

public class TransactionTests
{
    [Fact]
    public void Create_WithValidData_CreatesTransaction()
    {
        // Arrange
        var date = DateTime.Now;
        var description = "Test transaction";
        var category = "Test category";
        decimal amount = 100.50m;
        var type = "Income";

        // Act
        var transaction = Transaction.Create(date, description, category, amount, type);

        // Assert
        transaction.Should().NotBeNull();
        transaction.Date.Should().Be(date);
        transaction.Description.Should().Be(description);
        transaction.Category.Should().Be(category);
        transaction.Amount.Should().Be(amount);
        transaction.Type.Should().Be(type);
        transaction.ActivityId.Should().BeNull();
    }

    [Fact]
    public void Create_WithActivityId_AssignsActivityId()
    {
        // Arrange
        var date = DateTime.Now;
        var activityId = 5;

        // Act
        var transaction = Transaction.Create(date, "Description", "Category", 50m, "Expense", activityId);

        // Assert
        transaction.ActivityId.Should().Be(activityId);
    }

    [Theory]
    [InlineData("Income")]
    [InlineData("Expense")]
    public void Create_WithValidType_CreatesTransaction(string type)
    {
        // Act
        var transaction = Transaction.Create(DateTime.Now, "Test", "Category", 100m, type);

        // Assert
        transaction.Type.Should().Be(type);
    }

    [Fact]
    public void Create_WithEmptyDescription_ThrowsArgumentException()
    {
        // Act
        var act = () => Transaction.Create(DateTime.Now, "", "Category", 100m, "Income");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*descrição não pode estar vazia*");
    }

    [Fact]
    public void Create_WithEmptyCategory_ThrowsArgumentException()
    {
        // Act
        var act = () => Transaction.Create(DateTime.Now, "Description", "", 100m, "Income");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*categoria não pode estar vazia*");
    }

    [Fact]
    public void Create_WithNegativeAmount_ThrowsArgumentException()
    {
        // Act
        var act = () => Transaction.Create(DateTime.Now, "Description", "Category", -50m, "Income");

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*montante deve ser positivo*");
    }

    [Theory]
    [InlineData("Invalid")]
    [InlineData("INCOME")]
    [InlineData("expense")]
    [InlineData("")]
    public void Create_WithInvalidType_ThrowsArgumentException(string invalidType)
    {
        // Act
        var act = () => Transaction.Create(DateTime.Now, "Description", "Category", 100m, invalidType);

        // Assert
        act.Should().Throw<ArgumentException>().WithMessage("*tipo deve ser 'Income' ou 'Expense'*");
    }

    [Fact]
    public void UpdateDetails_WithValidData_UpdatesTransaction()
    {
        // Arrange
        var transaction = Transaction.Create(DateTime.Now, "Original", "Original", 100m, "Income");
        var newDate = DateTime.Now.AddDays(1);

        // Act
        transaction.UpdateDetails(newDate, "Updated", "Updated Category", 200m, "Expense");

        // Assert
        transaction.Date.Should().Be(newDate);
        transaction.Description.Should().Be("Updated");
        transaction.Category.Should().Be("Updated Category");
        transaction.Amount.Should().Be(200m);
        transaction.Type.Should().Be("Expense");
    }

    [Fact]
    public void UpdateDetails_WithInvalidData_ThrowsArgumentException()
    {
        // Arrange
        var transaction = Transaction.Create(DateTime.Now, "Test", "Test", 100m, "Income");

        // Act
        var act = () => transaction.UpdateDetails(DateTime.Now, "", "Category", 100m, "Income");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Transaction_Properties_CanBeSet()
    {
        // Arrange & Act
        var transaction = new Transaction
        {
            Date = DateTime.Now,
            Description = "Test",
            Category = "Category",
            Amount = 100m,
            Type = "Income",
            ActivityId = 5
        };

        // Assert
        transaction.Description.Should().Be("Test");
        transaction.ActivityId.Should().Be(5);
    }
}
