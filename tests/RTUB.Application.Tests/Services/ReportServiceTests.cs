using FluentAssertions;
using Moq;
using MockQueryable.Moq;
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
    public async Task GetAllReportsAsync_WithMultipleReports_ReturnsAll()
    {
        // Arrange
        var reports = new List<Report>
        {
            Report.Create("Report 1", 2021),
            Report.Create("Report 2", 2022),
            Report.Create("Report 3", 2023)
        };
        _mockReportRepository.Setup(r => r.GetAllWithActivitiesAsync())
            .ReturnsAsync(reports);

        // Act
        var result = await _reportService.GetAllReportsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPublishedReportsAsync_OnlyReturnsPublished()
    {
        // Arrange
        var report1 = Report.Create("Report 1", 2021);
        report1.Publish();
        var report3 = Report.Create("Report 3", 2023);
        report3.Publish();

        var publishedReports = new List<Report> { report1, report3 };
        _mockReportRepository.Setup(r => r.GetPublishedWithActivitiesAsync())
            .ReturnsAsync(publishedReports);

        // Act
        var result = await _reportService.GetPublishedReportsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.IsPublished);
    }

    [Fact]
    public async Task GetPublishedReportsAsync_OrdersByYearDescending()
    {
        // Arrange
        var report1 = Report.Create("Report 2021", 2021);
        report1.Publish();
        var report2 = Report.Create("Report 2023", 2023);
        report2.Publish();
        var report3 = Report.Create("Report 2022", 2022);
        report3.Publish();

        var publishedReports = new List<Report> { report2, report3, report1 }; // Already ordered
        _mockReportRepository.Setup(r => r.GetPublishedWithActivitiesAsync())
            .ReturnsAsync(publishedReports);

        // Act
        var result = (await _reportService.GetPublishedReportsAsync()).ToList();

        // Assert
        result.First().Year.Should().Be(2023);
        result.Last().Year.Should().Be(2021);
    }

    [Fact]
    public async Task UpdateReportAsync_UpdatesSummary()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        var newSummary = "Updated summary";

        _mockReportRepository.Setup(r => r.GetByIdAsync(report.Id))
            .ReturnsAsync(report);

        // Act
        await _reportService.UpdateReportAsync(report.Id, newSummary);

        // Assert
        report.Summary.Should().Be(newSummary);
        _mockReportRepository.Verify(r => r.UpdateAsync(report), Times.Once);
    }

    [Fact]
    public async Task UpdateReportAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        _mockReportRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Report?)null);

        // Act & Assert
        var act = async () => await _reportService.UpdateReportAsync(999, "Summary");
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
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

    [Fact]
    public async Task UnpublishReportAsync_UnpublishesReport()
    {
        // Arrange
        var report = Report.Create("Test Report", 2023);
        report.Publish();
        _mockReportRepository.Setup(r => r.GetByIdAsync(report.Id))
            .ReturnsAsync(report);

        // Act
        await _reportService.UnpublishReportAsync(report.Id);

        // Assert
        report.IsPublished.Should().BeFalse();
        report.PublishedAt.Should().BeNull();
        _mockReportRepository.Verify(r => r.UpdateAsync(report), Times.Once);
    }

    [Fact]
    public async Task UnpublishReportAsync_WithInvalidId_ThrowsException()
    {
        // Arrange
        _mockReportRepository.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Report?)null);

        // Act & Assert
        var act = async () => await _reportService.UnpublishReportAsync(999);
        await act.Should().ThrowAsync<EntityNotFoundException>()
            .WithMessage("*not found*");
    }
}
