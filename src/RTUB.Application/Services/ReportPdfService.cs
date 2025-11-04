using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Report = RTUB.Core.Entities.Report;
using Activity = RTUB.Core.Entities.Activity;
using Transaction = RTUB.Core.Entities.Transaction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RTUB.Application.Data;

namespace RTUB.Application.Services
{
    public class ReportPdfService
    {
        private readonly IMemoryCache _cache;
        private readonly MemoryCacheEntryOptions _cacheOptions;

        public ReportPdfService(IMemoryCache cache)
        {
            _cache = cache;
            
            // Cache generated PDFs for 1 hour (they don't change frequently)
            _cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
                SlidingExpiration = TimeSpan.FromMinutes(30),
                Size = 1 // Size is used for eviction policy
            };
        }

        public byte[] GenerateReportPdf(Report report, List<Activity> activities, List<(Activity activity, List<Transaction> transactions)> allTransactions)
        {
            // Generate cache key based on report content (use current time if UpdatedAt is null)
            var timestamp = report.UpdatedAt?.ToString("yyyyMMddHHmmss") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var cacheKey = $"report-pdf-{report.Id}-{timestamp}";
            
            if (_cache.TryGetValue<byte[]>(cacheKey, out var cachedPdf) && cachedPdf != null)
            {
                return cachedPdf;
            }

            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Black));

                    page.Header()
                        .ShowOnce()
                        .AlignCenter()
                        .Column(column =>
                        {
                            column.Item().Text(report.Title)
                                .FontSize(24)
                                .Bold()
                                .FontColor("#6f42c1");

                            column.Item().PaddingTop(5).Text($"Ano Fiscal: {report.Year}")
                                .FontSize(12)
                                .FontColor(Colors.Grey.Darken2);

                            if (!string.IsNullOrEmpty(report.Summary))
                            {
                                column.Item().PaddingTop(10).Text(report.Summary)
                                    .FontSize(11)
                                    .Italic()
                                    .FontColor(Colors.Grey.Darken1);
                            }

                            if (report.PublishedAt.HasValue)
                            {
                                column.Item().PaddingTop(5).Text($"Publicado em: {report.PublishedAt.Value:dd/MM/yyyy HH:mm}")
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Medium);
                            }
                            else
                            {
                                column.Item().PaddingTop(5).Text("Status: Rascunho")
                                    .FontSize(10)
                                    .FontColor("#f0ad4e");
                            }
                        });

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            // Financial Summary - calculate from actual transactions
                            var totalIncome = allTransactions.SelectMany(x => x.transactions)
                                .Where(t => t.Type == "Income")
                                .Sum(t => t.Amount);
                            var totalExpenses = allTransactions.SelectMany(x => x.transactions)
                                .Where(t => t.Type == "Expense")
                                .Sum(t => t.Amount);
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

                            column.Item().PaddingTop(20).Text("Atividades e Transações")
                                .FontSize(18)
                                .Bold()
                                .FontColor("#6f42c1");

                            // Activities and Transactions
                            foreach (var (activity, transactions) in allTransactions)
                            {
                                column.Item().PaddingTop(15).Column(activityColumn =>
                                {
                                    activityColumn.Item().Background("#f0f0f0").Padding(10).Column(headerColumn =>
                                    {
                                        headerColumn.Item().Text(activity.Name)
                                            .FontSize(14)
                                            .Bold()
                                            .FontColor("#333");

                                        if (!string.IsNullOrEmpty(activity.Description))
                                        {
                                            headerColumn.Item().PaddingTop(5).Text(activity.Description)
                                                .FontSize(10)
                                                .FontColor(Colors.Grey.Darken1);
                                        }

                                        // Calculate activity totals from transactions
                                        var activityIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
                                        var activityExpenses = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
                                        var activityBalance = activityIncome - activityExpenses;

                                        headerColumn.Item().PaddingTop(10).Row(row =>
                                        {
                                            row.RelativeItem().Text($"Receitas: €{activityIncome:N2}")
                                                .FontSize(10)
                                                .Bold()
                                                .FontColor("#28a745");

                                            row.RelativeItem().Text($"Despesas: €{activityExpenses:N2}")
                                                .FontSize(10)
                                                .Bold()
                                                .FontColor("#dc3545");

                                            row.RelativeItem().Text($"Saldo: €{activityBalance:N2}")
                                                .FontSize(10)
                                                .Bold()
                                                .FontColor(activityBalance >= 0 ? "#28a745" : "#dc3545");
                                        });
                                    });

                                    if (transactions.Any())
                                    {
                                        activityColumn.Item().PaddingTop(10).Table(table =>
                                        {
                                            table.ColumnsDefinition(columns =>
                                            {
                                                columns.RelativeColumn(1.5f);
                                                columns.RelativeColumn(3);
                                                columns.RelativeColumn(2);
                                                columns.RelativeColumn(1.5f);
                                                columns.RelativeColumn(2);
                                            });

                                            // Header
                                            table.Header(header =>
                                            {
                                                header.Cell().Background("#6f42c1").Padding(8)
                                                    .Text("Data").FontSize(10).Bold().FontColor(Colors.White);
                                                header.Cell().Background("#6f42c1").Padding(8)
                                                    .Text("Descrição").FontSize(10).Bold().FontColor(Colors.White);
                                                header.Cell().Background("#6f42c1").Padding(8)
                                                    .Text("Categoria").FontSize(10).Bold().FontColor(Colors.White);
                                                header.Cell().Background("#6f42c1").Padding(8)
                                                    .Text("Tipo").FontSize(10).Bold().FontColor(Colors.White);
                                                header.Cell().Background("#6f42c1").Padding(8).AlignRight()
                                                    .Text("Valor").FontSize(10).Bold().FontColor(Colors.White);
                                            });

                                            // Rows
                                            foreach (var transaction in transactions)
                                            {
                                                var isIncome = transaction.Type == "Income";
                                                var typeColor = isIncome ? "#28a745" : "#dc3545";
                                                var typeLabel = isIncome ? "Receita" : "Despesa";
                                                var sign = isIncome ? "+" : "-";

                                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6)
                                                    .Text($"{transaction.Date:dd/MM/yyyy}").FontSize(9);
                                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6)
                                                    .Text(transaction.Description ?? "").FontSize(9);
                                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6)
                                                    .Text(transaction.Category ?? "").FontSize(9);
                                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6)
                                                    .Text(typeLabel).FontSize(9).FontColor(typeColor);
                                                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight()
                                                    .Text($"{sign}€{transaction.Amount:N2}").FontSize(9).Bold().FontColor(typeColor);
                                            }
                                        });
                                    }
                                    else
                                    {
                                        activityColumn.Item().PaddingTop(10).Text("Sem transações")
                                            .FontSize(10)
                                            .Italic()
                                            .FontColor(Colors.Grey.Medium);
                                    }
                                });
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
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

            var pdfBytes = document.GeneratePdf();
            
            // Cache the generated PDF
            _cache.Set(cacheKey, pdfBytes, _cacheOptions);
            
            return pdfBytes;
        }
    }
}
