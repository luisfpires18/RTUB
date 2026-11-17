// NerbaOrderService: add order.
using FluentAssertions;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

public partial class NerbaOrderServiceTests
{
    #region AddOrderAsync Tests

    [Fact]
    public async Task AddOrderAsync_WithValidOrder_ReturnsSuccess()
    {
        // Arrange
        var ev = await CreateTestEvent();
        var order = new NerbaOrder
        {
            Item = "New Item",
            Stock = 5,
            PricePerUnit = 2.50m,
            EventId = ev.Id
        };

        // Act
        var (success, message) = await _service.AddOrderAsync(order);

        // Assert
        success.Should().BeTrue();
        message.Should().Contain("sucesso");
        order.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AddOrderAsync_WithOrderDate_PersistsDate()
    {
        // Arrange
        var ev = await CreateTestEvent();
        var orderDate = new DateTime(2025, 4, 25, 0, 0, 0, DateTimeKind.Utc);
        var order = new NerbaOrder
        {
            Item = "Dated Item",
            Stock = 3,
            PricePerUnit = 1.00m,
            EventId = ev.Id,
            OrderDate = orderDate
        };

        // Act
        var (success, _) = await _service.AddOrderAsync(order);

        // Assert
        success.Should().BeTrue();
        var fetched = await _service.GetByIdAsync(order.Id);
        fetched!.OrderDate.Should().Be(orderDate);
    }

    [Fact]
    public async Task AddOrderAsync_WithNullOrderDate_PersistsNull()
    {
        // Arrange
        var ev = await CreateTestEvent();
        var order = new NerbaOrder
        {
            Item = "No Date Item",
            Stock = 1,
            PricePerUnit = 1.00m,
            EventId = ev.Id,
            OrderDate = null
        };

        // Act
        var (success, _) = await _service.AddOrderAsync(order);

        // Assert
        success.Should().BeTrue();
        var fetched = await _service.GetByIdAsync(order.Id);
        fetched!.OrderDate.Should().BeNull();
    }

    [Fact]
    public async Task AddOrderAsync_WithZeroEventId_ReturnsFailure()
    {
        var order = new NerbaOrder
        {
            Item = "Invalid Item",
            Stock = 1,
            PricePerUnit = 1.00m,
            EventId = 0
        };

        var (success, message) = await _service.AddOrderAsync(order);

        success.Should().BeFalse();
        message.Should().Contain("Evento");
    }

    [Fact]
    public async Task AddOrderAsync_WithNegativeEventId_ReturnsFailure()
    {
        var order = new NerbaOrder
        {
            Item = "Invalid Item",
            Stock = 1,
            PricePerUnit = 1.00m,
            EventId = -1
        };

        var (success, message) = await _service.AddOrderAsync(order);

        success.Should().BeFalse();
    }

    #endregion
}
