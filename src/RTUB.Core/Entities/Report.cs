namespace RTUB.Core.Entities;

/// <summary>
/// Represents a financial report for a fiscal year
/// </summary>
public class Report : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal FinalBalance { get; set; }
    public string? Summary { get; set; }
    public byte[]? PdfData { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsPublished { get; set; }

    // Private constructor for EF Core
    public Report() { }

    public static Report Create(string title, int year, string? summary = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título do relatório não pode estar vazio", nameof(title));

        return new Report
        {
            Title = title,
            Year = year,
            Summary = summary,
            IsPublished = false
        };
    }

    public void UpdateFinancials(decimal totalIncome, decimal totalExpenses)
    {
        TotalIncome = totalIncome;
        TotalExpenses = totalExpenses;
        FinalBalance = totalIncome - totalExpenses;
    }

    public void UpdateSummary(string? summary)
    {
        Summary = summary;
    }

    public void SetPdfData(byte[] pdfData)
    {
        PdfData = pdfData ?? throw new ArgumentNullException(nameof(pdfData));
    }

    public void Publish()
    {
        if (IsPublished)
            throw new InvalidOperationException("Report is already published");

        IsPublished = true;
        PublishedAt = DateTime.UtcNow;
    }

    public void Unpublish()
    {
        IsPublished = false;
        PublishedAt = null;
    }

    /// <summary>
    /// Checks if this report is for the current fiscal year
    /// Fiscal year runs from September to August (Sept-Aug cycle)
    /// </summary>
    /// <returns>True if the report is for the current fiscal year, false otherwise</returns>
    public bool IsCurrentFiscalYear()
    {
        var today = DateTime.Today;
        var currentFiscalYearStartYear = today.Month >= 9 ? today.Year : today.Year - 1;
        return Year == currentFiscalYearStartYear;
    }
}
