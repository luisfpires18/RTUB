using FluentAssertions;
using RTUB.Application.Extensions;

namespace RTUB.Application.Tests.Extensions;

/// <summary>
/// Unit tests for QueryableExtensions
/// Tests pagination and conditional query methods
/// </summary>
public class QueryableExtensionsTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private List<TestEntity> CreateTestData(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new TestEntity
            {
                Id = i,
                Name = $"Item {i}",
                IsActive = i % 2 == 0,
                CreatedAt = DateTime.Today.AddDays(-i)
            })
            .ToList();
    }

    [Fact]
    public void Paginate_FirstPage_ReturnsCorrectItems()
    {
        // Arrange
        var data = CreateTestData(25).AsQueryable();

        // Act
        var result = data.Paginate(1, 10).ToList();

        // Assert
        result.Should().HaveCount(10);
        result.First().Id.Should().Be(1);
        result.Last().Id.Should().Be(10);
    }

    [Fact]
    public void Paginate_SecondPage_ReturnsCorrectItems()
    {
        // Arrange
        var data = CreateTestData(25).AsQueryable();

        // Act
        var result = data.Paginate(2, 10).ToList();

        // Assert
        result.Should().HaveCount(10);
        result.First().Id.Should().Be(11);
        result.Last().Id.Should().Be(20);
    }

    [Fact]
    public void Paginate_LastPage_ReturnsRemainingItems()
    {
        // Arrange
        var data = CreateTestData(25).AsQueryable();

        // Act
        var result = data.Paginate(3, 10).ToList();

        // Assert
        result.Should().HaveCount(5);
        result.First().Id.Should().Be(21);
        result.Last().Id.Should().Be(25);
    }

    [Fact]
    public void Paginate_InvalidPage_DefaultsToOne()
    {
        // Arrange
        var data = CreateTestData(25).AsQueryable();

        // Act
        var result = data.Paginate(0, 10).ToList();

        // Assert
        result.Should().HaveCount(10);
        result.First().Id.Should().Be(1);
    }

    [Fact]
    public void Paginate_NegativePage_DefaultsToOne()
    {
        // Arrange
        var data = CreateTestData(25).AsQueryable();

        // Act
        var result = data.Paginate(-5, 10).ToList();

        // Assert
        result.Should().HaveCount(10);
        result.First().Id.Should().Be(1);
    }

    [Fact]
    public void Paginate_InvalidPageSize_DefaultsToTen()
    {
        // Arrange
        var data = CreateTestData(25).AsQueryable();

        // Act
        var result = data.Paginate(1, 0).ToList();

        // Assert
        result.Should().HaveCount(10);
    }

    [Fact]
    public void Paginate_NegativePageSize_DefaultsToTen()
    {
        // Arrange
        var data = CreateTestData(25).AsQueryable();

        // Act
        var result = data.Paginate(1, -5).ToList();

        // Assert
        result.Should().HaveCount(10);
    }

    [Fact]
    public void WhereIf_ConditionTrue_AppliesFilter()
    {
        // Arrange
        var data = CreateTestData(10).AsQueryable();

        // Act
        var result = data.WhereIf(true, x => x.IsActive).ToList();

        // Assert
        result.Should().HaveCount(5);
        result.Should().OnlyContain(x => x.IsActive);
    }

    [Fact]
    public void WhereIf_ConditionFalse_ReturnsOriginal()
    {
        // Arrange
        var data = CreateTestData(10).AsQueryable();

        // Act
        var result = data.WhereIf(false, x => x.IsActive).ToList();

        // Assert
        result.Should().HaveCount(10);
    }

    [Fact]
    public void WhereIf_MultipleConditions_AppliesAll()
    {
        // Arrange
        var data = CreateTestData(20).AsQueryable();

        // Act
        var result = data
            .WhereIf(true, x => x.IsActive)
            .WhereIf(true, x => x.Id > 5)
            .ToList();

        // Assert
        result.Should().OnlyContain(x => x.IsActive && x.Id > 5);
    }

    [Fact]
    public void OrderByIf_ConditionTrue_AppliesOrdering()
    {
        // Arrange
        var data = CreateTestData(10).AsQueryable();

        // Act
        var result = data.OrderByIf(true, x => x.Name).ToList();

        // Assert
        result.Select(x => x.Name).Should().BeInAscendingOrder();
    }

    [Fact]
    public void OrderByIf_ConditionFalse_ReturnsOriginalOrder()
    {
        // Arrange
        var data = CreateTestData(10).AsQueryable();
        var originalOrder = data.Select(x => x.Id).ToList();

        // Act
        var result = data.OrderByIf(false, x => x.Name).ToList();

        // Assert
        result.Select(x => x.Id).Should().BeEquivalentTo(originalOrder, o => o.WithStrictOrdering());
    }

    [Fact]
    public void OrderByDescendingIf_ConditionTrue_AppliesDescendingOrdering()
    {
        // Arrange
        var data = CreateTestData(10).AsQueryable();

        // Act
        var result = data.OrderByDescendingIf(true, x => x.Id).ToList();

        // Assert
        result.Select(x => x.Id).Should().BeInDescendingOrder();
    }

    [Fact]
    public void OrderByDescendingIf_ConditionFalse_ReturnsOriginalOrder()
    {
        // Arrange
        var data = CreateTestData(10).AsQueryable();
        var originalOrder = data.Select(x => x.Id).ToList();

        // Act
        var result = data.OrderByDescendingIf(false, x => x.Id).ToList();

        // Assert
        result.Select(x => x.Id).Should().BeEquivalentTo(originalOrder, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Paginate_EmptyCollection_ReturnsEmpty()
    {
        // Arrange
        var data = new List<TestEntity>().AsQueryable();

        // Act
        var result = data.Paginate(1, 10).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void WhereIf_EmptyCollection_ReturnsEmpty()
    {
        // Arrange
        var data = new List<TestEntity>().AsQueryable();

        // Act
        var result = data.WhereIf(true, x => x.IsActive).ToList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Paginate_SingleItem_ReturnsSingleItem()
    {
        // Arrange
        var data = CreateTestData(1).AsQueryable();

        // Act
        var result = data.Paginate(1, 10).ToList();

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void ChainedOperations_WorkCorrectly()
    {
        // Arrange
        var data = CreateTestData(50).AsQueryable();

        // Act
        var result = data
            .WhereIf(true, x => x.IsActive)
            .OrderByIf(true, x => x.Id)
            .Paginate(1, 5)
            .ToList();

        // Assert
        result.Should().HaveCount(5);
        result.Should().OnlyContain(x => x.IsActive);
        result.Select(x => x.Id).Should().BeInAscendingOrder();
    }
}
