using FluentAssertions;
using MockQueryable.Moq;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

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

}
