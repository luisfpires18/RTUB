using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

public class FiscalYearServiceTests
{
    private readonly Mock<IFiscalYearRepository> _repositoryMock;
    private readonly FiscalYearService _service;

    public FiscalYearServiceTests()
    {
        _repositoryMock = new Mock<IFiscalYearRepository>();
        _service = new FiscalYearService(_repositoryMock.Object);
    }

    [Fact]
    public async Task CreateFiscalYearAsync_ValidYear_CreatesFiscalYear()
    {
        // Arrange
        var startYear = DateTime.Now.Month >= 9 ? DateTime.Now.Year : DateTime.Now.Year - 1;
        var expectedFiscalYear = FiscalYear.Create(startYear, startYear + 1);

        _repositoryMock.Setup(r => r.GetByStartYearAsync(startYear))
            .ReturnsAsync((FiscalYear?)null);
        _repositoryMock.Setup(r => r.AddAsync(It.IsAny<FiscalYear>()))
            .ReturnsAsync(expectedFiscalYear);

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
        var existingFiscalYear = FiscalYear.Create(startYear, startYear + 1);

        _repositoryMock.Setup(r => r.GetByStartYearAsync(startYear))
            .ReturnsAsync(existingFiscalYear);

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

        _repositoryMock.Setup(r => r.GetByIdAsync(fiscalYear.Id))
            .ReturnsAsync(fiscalYear);

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
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((FiscalYear?)null);

        // Act
        var result = await _service.GetFiscalYearByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllFiscalYearsAsync_ReturnsAllFiscalYears()
    {
        // Arrange
        var fiscalYears = new List<FiscalYear>
        {
            FiscalYear.Create(2022, 2023),
            FiscalYear.Create(2023, 2024)
        };

        _repositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(fiscalYears);

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

        _repositoryMock.Setup(r => r.GetByStartYearAsync(2023))
            .ReturnsAsync(fiscalYear);

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
        // Arrange
        _repositoryMock.Setup(r => r.GetByStartYearAsync(2025))
            .ReturnsAsync((FiscalYear?)null);

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
        var fiscalYearId = fiscalYear.Id;

        _repositoryMock.Setup(r => r.GetByIdAsync(fiscalYearId))
            .ReturnsAsync(fiscalYear);
        _repositoryMock.Setup(r => r.DeleteAsync(fiscalYearId))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteFiscalYearAsync(fiscalYearId);

        // Assert
        _repositoryMock.Verify(r => r.DeleteAsync(fiscalYearId), Times.Once);
    }

    [Fact]
    public async Task DeleteFiscalYearAsync_NonExistingId_ThrowsException()
    {
        // Arrange
        _repositoryMock.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((FiscalYear?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.DeleteFiscalYearAsync(999));
    }

    [Fact]
    public async Task GetAvailableFiscalYearStartYearsAsync_ReturnsOnlyAvailableYears()
    {
        // Arrange
        var currentMonth = DateTime.Now.Month;
        var currentYear = DateTime.Now.Year;
        var currentFiscalStartYear = currentMonth >= 9 ? currentYear : currentYear - 1;

        var existingFiscalYears = new List<FiscalYear>
        {
            FiscalYear.Create(1991, 1992),
            FiscalYear.Create(1992, 1993),
            FiscalYear.Create(currentFiscalStartYear, currentFiscalStartYear + 1)
        };

        _repositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(existingFiscalYears);

        // Act
        var availableYears = (await _service.GetAvailableFiscalYearStartYearsAsync()).ToList();

        // Assert
        availableYears.Should().NotContain(1991);
        availableYears.Should().NotContain(1992);
        availableYears.Should().NotContain(currentFiscalStartYear);
        availableYears.Should().Contain(1993);
        availableYears.Should().Contain(2000);
    }

    [Fact]
    public async Task GetAvailableFiscalYearStartYearsAsync_ReturnsEmptyWhenAllYearsCreated()
    {
        // Arrange
        var currentMonth = DateTime.Now.Month;
        var currentYear = DateTime.Now.Year;
        var currentFiscalStartYear = currentMonth >= 9 ? currentYear : currentYear - 1;

        // Create all fiscal years from 1991 to current
        var allFiscalYears = new List<FiscalYear>();
        for (int year = 1991; year <= currentFiscalStartYear; year++)
        {
            allFiscalYears.Add(FiscalYear.Create(year, year + 1));
        }

        _repositoryMock.Setup(r => r.GetAllAsync())
            .ReturnsAsync(allFiscalYears);

        // Act
        var availableYears = (await _service.GetAvailableFiscalYearStartYearsAsync()).ToList();

        // Assert
        availableYears.Should().BeEmpty();
    }
}
