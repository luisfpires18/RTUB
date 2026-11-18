namespace RTUB.Core.Entities;

/// <summary>
/// Represents a financial report for a fiscal year
/// </summary>
public class Report : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Summary { get; set; }
    public DateTime? PublishedAt { get; set; }
    public bool IsPublished { get; set; }

    // Navigation properties
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();

    // Computed properties - calculate from transactions (source of truth)
    // Excludes hidden activities (BANCO, CAIXA, CALOTES) from totals
    public decimal TotalIncome => Activities
        .Where(a => !a.IsHiddenFromCalculations())
        .SelectMany(a => a.Transactions)
        .Where(t => t.Type == "Income")
        .Sum(t => t.Amount);

    public decimal TotalExpenses => Activities
        .Where(a => !a.IsHiddenFromCalculations())
        .SelectMany(a => a.Transactions)
        .Where(t => t.Type == "Expense")
        .Sum(t => t.Amount);

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

    public void Publish()
    {
        if (IsPublished)
            throw new InvalidOperationException("Report is already published");

        IsPublished = true;
        PublishedAt = DateTime.UtcNow;
    }
}
