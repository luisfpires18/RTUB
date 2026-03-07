using FluentAssertions;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;
using RTUB.Core.Entities;
using RTUB.Core.Enums;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for NerbaOrderService
/// Tests Nerba order CRUD operations and event-based queries
/// </summary>
public class NerbaOrderServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly NerbaOrderService _service;

    public NerbaOrderServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _context = _fixture.CreateContext();
        _service = new NerbaOrderService(_fixture.CreateContextFactory());
    }

    #region Helper Methods

    private async Task<Event> CreateTestEvent(string name = "Nerba Event", EventType type = EventType.Nerba)
    {
        var ev = Event.Create(name, DateTime.UtcNow, "Test Location", type);
        _context.Events.Add(ev);
        await _context.SaveChangesAsync();
        return ev;
    }

    private async Task<NerbaOrder> CreateTestOrder(int eventId, string item = "Test Item", int stock = 10, decimal price = 5.00m)
    {
        var order = new NerbaOrder
        {
            Item = item,
            Stock = stock,
            PricePerUnit = price,
            EventId = eventId
        };
        _context.NerbaOrders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    #endregion

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

    #region GetByReportIdAsync Tests

    [Fact]
    public async Task GetByReportIdAsync_ReturnsOnlyOrdersForReport()
    {
        // Arrange
        var ev = await CreateTestEvent();
        var order1 = await CreateTestOrder(ev.Id, "Item With Report");
        order1.ReportId = 42;
        _context.Update(order1);
        await _context.SaveChangesAsync();

        await CreateTestOrder(ev.Id, "Item Without Report");

        // Act
        var result = await _service.GetByReportIdAsync(42);

        // Assert
        result.Should().HaveCount(1);
        result.First().Item.Should().Be("Item With Report");
    }

    #endregion

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

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
