// NerbaOrderService: update and delete.
using FluentAssertions;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

public partial class NerbaOrderServiceTests
{
    #region UpdateOrderAsync Tests

    [Fact]
    public async Task UpdateOrderAsync_WithExistingOrder_UpdatesAndReturnsSuccess()
    {
        // Arrange
        var ev = await CreateTestEvent();
        var order = await CreateTestOrder(ev.Id, "Original Item");

        var updated = new NerbaOrder
        {
            Id = order.Id,
            Item = "Updated Item",
            Stock = 20,
            PricePerUnit = 10.00m,
            EventId = ev.Id
        };

        // Act
        var (success, message) = await _service.UpdateOrderAsync(updated);

        // Assert
        success.Should().BeTrue();

        var fetched = await _service.GetByIdAsync(order.Id);
        fetched!.Item.Should().Be("Updated Item");
        fetched.Stock.Should().Be(20);
        fetched.PricePerUnit.Should().Be(10.00m);
    }

    [Fact]
    public async Task UpdateOrderAsync_WithNonExistentOrder_ReturnsFailure()
    {
        var order = new NerbaOrder
        {
            Id = 999,
            Item = "Ghost",
            Stock = 1,
            PricePerUnit = 1.00m,
            EventId = 1
        };

        var (success, _) = await _service.UpdateOrderAsync(order);

        success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateOrderAsync_WithOrderDate_UpdatesDate()
    {
        // Arrange
        var ev = await CreateTestEvent();
        var order = await CreateTestOrder(ev.Id, "Dated Item");
        var newDate = new DateTime(2025, 4, 26, 0, 0, 0, DateTimeKind.Utc);

        var updated = new NerbaOrder
        {
            Id = order.Id,
            Item = order.Item,
            Stock = order.Stock,
            PricePerUnit = order.PricePerUnit,
            EventId = ev.Id,
            OrderDate = newDate
        };

        // Act
        var (success, _) = await _service.UpdateOrderAsync(updated);

        // Assert
        success.Should().BeTrue();
        var fetched = await _service.GetByIdAsync(order.Id);
        fetched!.OrderDate.Should().Be(newDate);
    }

    #endregion

    #region DeleteOrderAsync Tests

    [Fact]
    public async Task DeleteOrderAsync_WithExistingOrder_DeletesAndReturnsSuccess()
    {
        // Arrange
        var ev = await CreateTestEvent();
        var order = await CreateTestOrder(ev.Id);

        // Act
        var (success, message) = await _service.DeleteOrderAsync(order.Id);

        // Assert
        success.Should().BeTrue();
        var fetched = await _service.GetByIdAsync(order.Id);
        fetched.Should().BeNull();
    }

    [Fact]
    public async Task DeleteOrderAsync_WithNonExistentId_ReturnsFailure()
    {
        var (success, message) = await _service.DeleteOrderAsync(999);

        success.Should().BeFalse();
        message.Should().Contain("não encontrada");
    }

    #endregion

    #region DeleteByEventIdAsync Tests

    [Fact]
    public async Task DeleteByEventIdAsync_DeletesAllOrdersForEvent()
    {
        // Arrange
        var ev1 = await CreateTestEvent("Event To Delete");
        var ev2 = await CreateTestEvent("Event To Keep");
        await CreateTestOrder(ev1.Id, "Delete Me 1");
        await CreateTestOrder(ev1.Id, "Delete Me 2");
        await CreateTestOrder(ev2.Id, "Keep Me");

        // Act
        var (success, message) = await _service.DeleteByEventIdAsync(ev1.Id);

        // Assert
        success.Should().BeTrue();

        var remaining = await _service.GetAllAsync();
        remaining.Should().HaveCount(1);
        remaining.First().Item.Should().Be("Keep Me");
    }

    [Fact]
    public async Task DeleteByEventIdAsync_WithNoOrders_ReturnsSuccess()
    {
        var ev = await CreateTestEvent();

        var (success, _) = await _service.DeleteByEventIdAsync(ev.Id);

        success.Should().BeTrue();
    }

    #endregion
}
