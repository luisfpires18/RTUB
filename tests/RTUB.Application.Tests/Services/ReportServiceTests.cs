using FluentAssertions;
using Moq;
using RTUB.Application.Interfaces;
using RTUB.Application.Services;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for ReportService
/// Tests financial report operations and calculations
/// </summary>
public class ReportServiceTests
{
    private readonly Mock<IReportRepository> _mockReportRepository;
    private readonly ReportService _reportService;

    public ReportServiceTests()
    {
        _mockReportRepository = new Mock<IReportRepository>();
        _reportService = new ReportService(_mockReportRepository.Object);
    }

    [Fact]
    public async Task CreateReportAsync_WithValidData_CreatesReport()
    {
        // Arrange
        var title = "Annual Report 2023-2024";
        var year = 2023;
        var summary = "Financial summary";
        var expectedReport = Report.Create(title, year, summary);

        _mockReportRepository.Setup(r => r.AddAsync(It.IsAny<Report>()))
            .ReturnsAsync(expectedReport);

        // Act
        var result = await _reportService.CreateReportAsync(title, year, summary);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be(title);
        result.Year.Should().Be(year);
        result.Summary.Should().Be(summary);
        result.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task GetReportByIdAsync_ExistingReport_ReturnsReport()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        _mockReportRepository.Setup(r => r.GetByIdWithActivitiesAsync(report.Id))
            .ReturnsAsync(report);

        // Act
        var result = await _reportService.GetReportByIdAsync(report.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(report.Id);
        result.Title.Should().Be("Test Report");
    }

    [Fact]
    public async Task GetReportByIdAsync_NonExistingReport_ReturnsNull()
    {
        // Arrange
        _mockReportRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Report?)null);

        // Act
        var result = await _reportService.GetReportByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task PublishReportAsync_PublishesReport()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        _mockReportRepository.Setup(r => r.GetByIdAsync(report.Id))
            .ReturnsAsync(report);

        // Act
        await _reportService.PublishReportAsync(report.Id);

        // Assert
        report.IsPublished.Should().BeTrue();
        report.PublishedAt.Should().NotBeNull();
        _mockReportRepository.Verify(r => r.UpdateAsync(report), Times.Once);
    }

    [Fact]
    public async Task PublishReportAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        _mockReportRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Report?)null);

        // Act & Assert
        var act = async () => await _reportService.PublishReportAsync(999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }

}
