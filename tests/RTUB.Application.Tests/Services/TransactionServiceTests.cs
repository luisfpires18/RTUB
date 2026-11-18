using Moq;
using RTUB.Application.Data;
using RTUB.Application.Interfaces;
using RTUB.Application.Repositories;
using RTUB.Application.Services;
using RTUB.Application.Tests.Fixtures;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for TransactionService
/// Tests financial transaction operations (Income/Expense)
/// HIGH PRIORITY - Financial data handling
/// </summary>
public partial class TransactionServiceTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DatabaseFixture _fixture;
    private readonly TransactionService _service;
    private readonly Mock<IReceiptStorageService> _mockReceiptStorageService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<IActivityService> _mockActivityService;

    public TransactionServiceTests(DatabaseFixture fixture)
    {
        // Clean database at constructor start to ensure test isolation
        _fixture = fixture;
        var tempContext = _fixture.CreateContext();
        _fixture.CleanDatabase(tempContext).GetAwaiter().GetResult();
        tempContext.Dispose();

        _fixture = fixture;
        _context = _fixture.CreateContext();
        _mockReceiptStorageService = new Mock<IReceiptStorageService>();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockActivityService = new Mock<IActivityService>();
        _service = new TransactionService(
            new TransactionRepository(_fixture.CreateContextFactory()),
            _mockReceiptStorageService.Object,
            _mockAuditLogService.Object,
            _mockActivityService.Object);
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
