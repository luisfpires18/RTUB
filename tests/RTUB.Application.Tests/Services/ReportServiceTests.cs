using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;
using RTUB.Application.Services;
using RTUB.Core.Entities;

namespace RTUB.Application.Tests.Services;

/// <summary>
/// Unit tests for ReportService
/// Tests financial report operations and calculations
/// </summary>
public class ReportServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ReportService _reportService;

    public ReportServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _reportService = new ReportService(_context);
    }

    [Fact]
    public async Task CreateReportAsync_WithValidData_CreatesReport()
    {
        // Arrange
        var title = "Annual Report 2023-2024";
        var year = 2023;
        var summary = "Financial summary";

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
        var report = await _reportService.CreateReportAsync("Test Report", 2023);

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
        // Act
        var result = await _reportService.GetReportByIdAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllReportsAsync_WithMultipleReports_ReturnsAll()
    {
        // Arrange
        await _reportService.CreateReportAsync("Report 1", 2021);
        await _reportService.CreateReportAsync("Report 2", 2022);
        await _reportService.CreateReportAsync("Report 3", 2023);

        // Act
        var result = await _reportService.GetAllReportsAsync();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPublishedReportsAsync_OnlyReturnsPublished()
    {
        // Arrange
        var report1 = await _reportService.CreateReportAsync("Report 1", 2021);
        var report2 = await _reportService.CreateReportAsync("Report 2", 2022);
        var report3 = await _reportService.CreateReportAsync("Report 3", 2023);
        
        await _reportService.PublishReportAsync(report1.Id);
        await _reportService.PublishReportAsync(report3.Id);

        // Act
        var result = await _reportService.GetPublishedReportsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.IsPublished);
        result.Should().Contain(r => r.Id == report1.Id);
        result.Should().Contain(r => r.Id == report3.Id);
    }

    [Fact]
    public async Task GetPublishedReportsAsync_OrdersByYearDescending()
    {
        // Arrange
        var report1 = await _reportService.CreateReportAsync("Report 2021", 2021);
        var report2 = await _reportService.CreateReportAsync("Report 2023", 2023);
        var report3 = await _reportService.CreateReportAsync("Report 2022", 2022);
        
        await _reportService.PublishReportAsync(report1.Id);
        await _reportService.PublishReportAsync(report2.Id);
        await _reportService.PublishReportAsync(report3.Id);

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
        var report = await _reportService.CreateReportAsync("Test Report", 2023);
        var newSummary = "Updated summary";

        // Act
        await _reportService.UpdateReportAsync(report.Id, newSummary);
        var updated = await _reportService.GetReportByIdAsync(report.Id);

        // Assert
        updated!.Summary.Should().Be(newSummary);
    }

    [Fact]
    public async Task UpdateReportAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _reportService.UpdateReportAsync(999, "Summary");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task PublishReportAsync_PublishesReport()
    {
        // Arrange
        var report = await _reportService.CreateReportAsync("Test Report", 2023);

        // Act
        await _reportService.PublishReportAsync(report.Id);
        var published = await _reportService.GetReportByIdAsync(report.Id);

        // Assert
        published!.IsPublished.Should().BeTrue();
        published.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PublishReportAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _reportService.PublishReportAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task UnpublishReportAsync_UnpublishesReport()
    {
        // Arrange
        var report = await _reportService.CreateReportAsync("Test Report", 2023);
        await _reportService.PublishReportAsync(report.Id);

        // Act
        await _reportService.UnpublishReportAsync(report.Id);
        var unpublished = await _reportService.GetReportByIdAsync(report.Id);

        // Assert
        unpublished!.IsPublished.Should().BeFalse();
        unpublished.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task UnpublishReportAsync_WithInvalidId_ThrowsException()
    {
        // Act & Assert
        var act = async () => await _reportService.UnpublishReportAsync(999);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    public void Dispose()
    {
        _context?.Dispose();
    }
}
