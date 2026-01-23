using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RTUB.Application.Extensions;
using RTUB.Application.Interfaces;
using RTUB.Core.Constants;
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
    /// Gets all reports with their activities and transactions
    /// </summary>
    /// <returns>Collection of all reports</returns>
    public async Task<IEnumerable<Report>> GetAllReportsAsync()
    {
        return await _reportRepository.GetAllWithActivitiesAsync();
    }

    /// <summary>
    /// Gets all published reports with their activities and transactions
    /// </summary>
    /// <returns>Collection of published reports</returns>
    public async Task<IEnumerable<Report>> GetPublishedReportsAsync()
    {
        // Get published reports with activities and transactions
        return await _reportRepository.GetPublishedWithActivitiesAsync();
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
    /// Updates the summary of a report
    /// </summary>
    /// <param name="id">The ID of the report to update</param>
    /// <param name="summary">The new summary text</param>
    /// <exception cref="EntityNotFoundException">Thrown when the report is not found</exception>
    public async Task UpdateReportAsync(int id, string? summary)
    {
        var report = await _reportRepository.GetByIdOrThrowAsync(id);

        report.UpdateSummary(summary);
        await _reportRepository.UpdateAsync(report);
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

    /// <summary>
    /// Unpublishes a report, making it no longer publicly available
    /// </summary>
    /// <param name="id">The ID of the report to unpublish</param>
    /// <exception cref="EntityNotFoundException">Thrown when the report is not found</exception>
    public async Task UnpublishReportAsync(int id)
    {
        var report = await _reportRepository.GetByIdOrThrowAsync(id);

        report.Unpublish();
        await _reportRepository.UpdateAsync(report);
    }

    /// <summary>
    /// Generates a PDF document for a report and stores it in the report entity
    /// </summary>
    /// <param name="reportId">The ID of the report to generate a PDF for</param>
    /// <returns>The generated PDF as a byte array</returns>
    /// <exception cref="EntityNotFoundException">Thrown when the report is not found</exception>
    public async Task<byte[]> GenerateReportPdfAsync(int reportId)
    {
        var report = await _reportRepository.GetByIdWithActivitiesAsync(reportId);

        if (report == null)
            throw new EntityNotFoundException(nameof(Report), reportId);

        var activities = report.Activities.OrderBy(a => a.Name).ToList();
        var allTransactions = new List<(Activity activity, List<Transaction> transactions)>();

        foreach (var activity in activities)
        {
            var transactions = activity.Transactions.OrderBy(t => t.Date).ToList();
            allTransactions.Add((activity, transactions));
        }

        // Generate PDF using business logic
        var pdfData = GeneratePdf(report, activities, allTransactions);

        report.SetPdfData(pdfData);
        await _reportRepository.UpdateAsync(report);

        return pdfData;
    }

    /// <summary>
    /// Deletes a report and all its associated activities and transactions
    /// </summary>
    /// <param name="reportId">The ID of the report to delete</param>
    /// <exception cref="EntityNotFoundException">Thrown when the report is not found</exception>
    public async Task DeleteReportAsync(int reportId)
    {
        var report = await _reportRepository.GetByIdWithActivitiesAsync(reportId);

        if (report == null)
            throw new EntityNotFoundException(nameof(Report), reportId);

        // Repository will handle cascading deletes through EF Core relationships
        await _reportRepository.DeleteAsync(report);
    }

    private byte[] GeneratePdf(Report report, List<Activity> activities, List<(Activity activity, List<Transaction> transactions)> allTransactions)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Black));

                // Header
                page.Header().ShowOnce().AlignCenter().Column(column =>
                {
                    column.Item().Text(report.Title).FontSize(24).Bold().FontColor("#6f42c1");
                    column.Item().PaddingTop(5).Text($"Ano Letivo: {report.Year}").FontSize(12).FontColor(Colors.Grey.Darken2);

                    if (!string.IsNullOrEmpty(report.Summary))
                    {
                        column.Item().PaddingTop(10).Text(report.Summary).FontSize(11).Italic().FontColor(Colors.Grey.Darken1);
                    }

                    if (report.PublishedAt.HasValue)
                    {
                        column.Item().PaddingTop(5).Text($"Publicado em: {report.PublishedAt.Value:dd/MM/yyyy HH:mm}")
                            .FontSize(10).FontColor(Colors.Grey.Medium);
                    }
                });

                // Content
                page.Content().PaddingVertical(1, Unit.Centimetre).Column(column =>
                {
                    // Financial Summary - calculate from transactions (excluding hidden activities)
                    var regularTransactions = allTransactions
                        .Where(x => !x.activity.IsHiddenFromCalculations())
                        .SelectMany(x => x.transactions);
                    var totalIncome = regularTransactions.Where(t => t.Type == TransactionTypes.Income).Sum(t => t.Amount);
                    var totalExpenses = regularTransactions.Where(t => t.Type == TransactionTypes.Expense).Sum(t => t.Amount);
                    var balance = totalIncome - totalExpenses;

                    column.Item().Background("#f8f9fa").Padding(15).Column(summaryColumn =>
                    {
                        summaryColumn.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Receitas").FontSize(10).FontColor(Colors.Grey.Darken1);
                                col.Item().Text($"€{totalIncome:N2}").FontSize(18).Bold().FontColor("#28a745");
                            });
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Despesas").FontSize(10).FontColor(Colors.Grey.Darken1);
                                col.Item().Text($"€{totalExpenses:N2}").FontSize(18).Bold().FontColor("#dc3545");
                            });
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Saldo").FontSize(10).FontColor(Colors.Grey.Darken1);
                                col.Item().Text($"€{balance:N2}").FontSize(18).Bold()
                                    .FontColor(balance >= 0 ? "#28a745" : "#dc3545");
                            });
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Atividades").FontSize(10).FontColor(Colors.Grey.Darken1);
                                col.Item().Text(activities.Count.ToString()).FontSize(18).Bold().FontColor("#6f42c1");
                            });
                        });
                    });
                });

                // Footer
                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(TextStyle.Default.FontSize(9).FontColor(Colors.Grey.Medium));
                    text.Span("Gerado em: ");
                    text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    text.Span(" | Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
