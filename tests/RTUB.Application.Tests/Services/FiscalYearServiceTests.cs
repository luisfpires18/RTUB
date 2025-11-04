using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

public class FiscalYearServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly FiscalYearService _service;

    public FiscalYearServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _service = new FiscalYearService(_context);
    }

    [Fact]
    public async Task CreateFiscalYearAsync_ValidYear_CreatesFiscalYear()
    {
        // Arrange
        var startYear = DateTime.Now.Month >= 9 ? DateTime.Now.Year : DateTime.Now.Year - 1;

        // Act
        var result = await _service.CreateFiscalYearAsync(startYear);

        // Assert
        result.Should().NotBeNull();
        result.StartYear.Should().Be(startYear);
        result.EndYear.Should().Be(startYear + 1);
    }

    [Fact]
    public async Task CreateFiscalYearAsync_DuplicateYear_ThrowsException()
    {
        // Arrange
        var startYear = DateTime.Now.Month >= 9 ? DateTime.Now.Year : DateTime.Now.Year - 1;
        await _service.CreateFiscalYearAsync(startYear);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.CreateFiscalYearAsync(startYear));
    }

    [Fact]
    public async Task CreateFiscalYearAsync_FutureYear_ThrowsException()
    {
        // Arrange
        var futureYear = DateTime.Now.Year + 2;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.CreateFiscalYearAsync(futureYear));
    }

    [Fact]
    public async Task GetFiscalYearByIdAsync_ExistingId_ReturnsFiscalYear()
    {
        // Arrange
        var fiscalYear = FiscalYear.Create(2023, 2024);
        _context.FiscalYears.Add(fiscalYear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetFiscalYearByIdAsync(fiscalYear.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(fiscalYear.Id);
        result.StartYear.Should().Be(2023);
    }

    [Fact]
    public async Task GetFiscalYearByIdAsync_NonExistingId_ReturnsNull()
    {
        // Act
        var result = await _service.GetFiscalYearByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllFiscalYearsAsync_ReturnsAllFiscalYears()
    {
        // Arrange
        var fiscalYear1 = FiscalYear.Create(2022, 2023);
        var fiscalYear2 = FiscalYear.Create(2023, 2024);
        _context.FiscalYears.AddRange(fiscalYear1, fiscalYear2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetAllFiscalYearsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(fy => fy.StartYear == 2022);
        result.Should().Contain(fy => fy.StartYear == 2023);
    }

    [Fact]
    public async Task GetFiscalYearByStartYearAsync_ExistingYear_ReturnsFiscalYear()
    {
        // Arrange
        var fiscalYear = FiscalYear.Create(2023, 2024);
        _context.FiscalYears.Add(fiscalYear);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.GetFiscalYearByStartYearAsync(2023);

        // Assert
        result.Should().NotBeNull();
        result!.StartYear.Should().Be(2023);
        result.EndYear.Should().Be(2024);
    }

    [Fact]
    public async Task GetFiscalYearByStartYearAsync_NonExistingYear_ReturnsNull()
    {
        // Act
        var result = await _service.GetFiscalYearByStartYearAsync(2025);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteFiscalYearAsync_ExistingId_DeletesFiscalYear()
    {
        // Arrange
        var fiscalYear = FiscalYear.Create(2023, 2024);
        _context.FiscalYears.Add(fiscalYear);
        await _context.SaveChangesAsync();
        var fiscalYearId = fiscalYear.Id;

        // Act
        await _service.DeleteFiscalYearAsync(fiscalYearId);

        // Assert
        var deleted = await _context.FiscalYears.FindAsync(fiscalYearId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteFiscalYearAsync_NonExistingId_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            _service.DeleteFiscalYearAsync(999));
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
