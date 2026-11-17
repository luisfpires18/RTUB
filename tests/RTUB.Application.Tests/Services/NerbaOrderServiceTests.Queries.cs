// NerbaOrderService: queries.
using FluentAssertions;

namespace RTUB.Application.Tests.Services;

public partial class NerbaOrderServiceTests
{
    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_WithNoOrders_ReturnsEmptyList()
    {
        var result = await _service.GetAllAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WithOrders_ReturnsAllOrdersWithEvent()
    {
        // Arrange
        var ev = await CreateTestEvent();
        await CreateTestOrder(ev.Id, "Item A");
        await CreateTestOrder(ev.Id, "Item B");

        // Act
        var result = await _service.GetAllAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(o => o.Event.Should().NotBeNull());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOrderedByIdDescending()
    {
        // Arrange
        var ev = await CreateTestEvent();
        var first = await CreateTestOrder(ev.Id, "First");
        var second = await CreateTestOrder(ev.Id, "Second");

        // Act
        var result = await _service.GetAllAsync();

        // Assert
        result.First().Id.Should().Be(second.Id);
        result.Last().Id.Should().Be(first.Id);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithExistingId_ReturnsOrderWithEvent()
    {
        // Arrange
        var ev = await CreateTestEvent();
        var order = await CreateTestOrder(ev.Id);

        // Act
        var result = await _service.GetByIdAsync(order.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Item.Should().Be("Test Item");
        result.Event.Should().NotBeNull();
        result.Event.Name.Should().Be("Nerba Event");
    }

    [Fact]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(999);

        result.Should().BeNull();
    }

    #endregion

    #region GetByEventIdAsync Tests

    [Fact]
    public async Task GetByEventIdAsync_ReturnsOnlyOrdersForEvent()
    {
        // Arrange
        var ev1 = await CreateTestEvent("Event 1");
        var ev2 = await CreateTestEvent("Event 2");
        await CreateTestOrder(ev1.Id, "Item A");
        await CreateTestOrder(ev1.Id, "Item B");
        await CreateTestOrder(ev2.Id, "Item C");

        // Act
        var result = await _service.GetByEventIdAsync(ev1.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(o => o.EventId.Should().Be(ev1.Id));
    }

    [Fact]
    public async Task GetByEventIdAsync_WithNoOrders_ReturnsEmptyList()
    {
        var ev = await CreateTestEvent();

        var result = await _service.GetByEventIdAsync(ev.Id);

        result.Should().BeEmpty();
    }

    #endregion
}
