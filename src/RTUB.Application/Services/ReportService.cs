using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using RTUB.Core.Exceptions;

namespace RTUB.Application.Services;

/// <summary>
/// Service for managing reports including report generation, activities, and transactions.
/// Now depends on IReportRepository abstraction instead of concrete DbContext
/// </summary>
public class ReportService : IReportService
{
    private readonly IReportRepository _reportRepository;

    /// <summary>
    /// Initializes a new instance of the ReportService
    /// </summary>
    /// <param name="reportRepository">Repository for report operations</param>
    public ReportService(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    /// <summary>
    /// Gets a report by its ID with all activities and transactions
    /// </summary>
    /// <param name="id">The ID of the report to retrieve</param>
    /// <returns>The report if found, null otherwise</returns>
    public async Task<Report?> GetReportByIdAsync(int id)
    {
        // Get report with activities and transactions for computed properties
        return await _reportRepository.GetByIdWithActivitiesAsync(id);
    }

    /// <summary>
    /// Creates a new report
    /// </summary>
    /// <param name="title">The title of the report</param>
    /// <param name="year">The fiscal year for the report</param>
    /// <param name="summary">Optional summary text for the report</param>
    /// <returns>The created report</returns>
    public async Task<Report> CreateReportAsync(string title, int year, string? summary = null)
    {
        var report = Report.Create(title, year, summary);
        return await _reportRepository.AddAsync(report);
    }

    /// <summary>
    /// Publishes a report, making it publicly available
    /// </summary>
    /// <param name="id">The ID of the report to publish</param>
    /// <exception cref="EntityNotFoundException">Thrown when the report is not found</exception>
    public async Task PublishReportAsync(int id)
    {
        var report = await _reportRepository.GetByIdOrThrowAsync(id);

        report.Publish();
        await _reportRepository.UpdateAsync(report);
    }
}
