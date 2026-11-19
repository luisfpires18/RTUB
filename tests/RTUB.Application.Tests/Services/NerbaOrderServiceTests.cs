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
public partial class NerbaOrderServiceTests : IClassFixture<DatabaseFixture>, IDisposable
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

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
