using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RTUB.Application.Interfaces;
using RTUB.Core.Entities;
using Microsoft.EntityFrameworkCore;
using RTUB.Application.Data;


namespace RTUB.Application.Services;

/// <summary>
/// Service for managing reports including report generation, activities, and transactions.
/// </summary>
public class ReportService : IReportService
{
    private readonly ApplicationDbContext _context;

    public ReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Report?> GetReportByIdAsync(int id)
    {
        // Include Activities and Transactions for computed properties
        return await _context.Reports
            .Include(r => r.Activities)
                .ThenInclude(a => a.Transactions)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<Report>> GetAllReportsAsync()
    {
        // Include Activities and Transactions for computed properties
        return await _context.Reports
            .Include(r => r.Activities)
                .ThenInclude(a => a.Transactions)
            .ToListAsync();
    }

    public async Task<IEnumerable<Report>> GetPublishedReportsAsync()
    {
        // Include Activities and Transactions for computed properties
        return await _context.Reports
            .Include(r => r.Activities)
                .ThenInclude(a => a.Transactions)
            .Where(r => r.IsPublished)
            .OrderByDescending(r => r.Year)
            .ToListAsync();
    }

    public async Task<Report> CreateReportAsync(string title, int year, string? summary = null)
    {
        var report = Report.Create(title, year, summary);
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();
        return report;
    }

    public async Task UpdateReportAsync(int id, string? summary)
    {
        var report = await _context.Reports.FindAsync(id);
        if (report == null)
            throw new InvalidOperationException($"Report with ID {id} not found");

        report.UpdateSummary(summary);
        _context.Reports.Update(report);
        await _context.SaveChangesAsync();
    }

    public async Task PublishReportAsync(int id)
    {
        var report = await _context.Reports.FindAsync(id);
        if (report == null)
            throw new InvalidOperationException($"Report with ID {id} not found");

        report.Publish();
        _context.Reports.Update(report);
        await _context.SaveChangesAsync();
    }

    public async Task UnpublishReportAsync(int id)
    {
        var report = await _context.Reports.FindAsync(id);
        if (report == null)
            throw new InvalidOperationException($"Report with ID {id} not found");

        report.Unpublish();
        _context.Reports.Update(report);
        await _context.SaveChangesAsync();
    }

    public async Task<byte[]> GenerateReportPdfAsync(int reportId)
    {
        var report = await _context.Reports
            .Include(r => r.Activities)
                .ThenInclude(a => a.Transactions)
            .FirstOrDefaultAsync(r => r.Id == reportId);
            
        if (report == null)
            throw new InvalidOperationException($"Report with ID {reportId} not found");

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
        _context.Reports.Update(report);
        await _context.SaveChangesAsync();

        return pdfData;
    }

    public async Task DeleteReportAsync(int reportId)
    {
        var report = await _context.Reports
            .Include(r => r.Activities)
                .ThenInclude(a => a.Transactions)
            .FirstOrDefaultAsync(r => r.Id == reportId);
            
        if (report == null)
            throw new InvalidOperationException($"Report with ID {reportId} not found");

        // Delete all transactions for each activity
        foreach (var activity in report.Activities)
        {
            foreach (var transaction in activity.Transactions)
            {
                _context.Transactions.Remove(transaction);
            }
        }

        // Delete all activities
        foreach (var activity in report.Activities)
        {
            _context.Activities.Remove(activity);
        }

        // Finally delete the report
        _context.Reports.Remove(report);
        await _context.SaveChangesAsync();
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
                    // Financial Summary - calculate from transactions
                    var totalIncome = allTransactions.SelectMany(x => x.transactions).Where(t => t.Type == "Income").Sum(t => t.Amount);
                    var totalExpenses = allTransactions.SelectMany(x => x.transactions).Where(t => t.Type == "Expense").Sum(t => t.Amount);
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
