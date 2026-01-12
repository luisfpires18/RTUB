using RTUB.Core.Attributes;
using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents an activity within a financial report
/// </summary>
public class Activity : BaseEntity
{
    [Required(ErrorMessage = "O ID do relatório é obrigatório")]
    [Range(1, int.MaxValue, ErrorMessage = "ID de Relatório inválido")]
    public int ReportId { get; set; }

    [Required(ErrorMessage = "O nome da atividade é obrigatório")]
    [MaxLength(200, ErrorMessage = "O nome da atividade não pode exceder 200 caracteres")]
    public string Name { get; set; } = string.Empty;
    [MaxLength(1000, ErrorMessage = "A descrição não pode exceder 1000 caracteres")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "A data da atividade é obrigatória")]
    public DateTime StartDate { get; set; }

    [DateGreaterThan(nameof(StartDate), ErrorMessage = "A data de fim não pode ser anterior à data de início")]
    public DateTime? EndDate { get; set; }

    // Navigation properties
    public virtual Report? Report { get; set; }
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    // Computed properties - calculate from transactions (source of truth)
    public decimal TotalIncome => Transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
    public decimal TotalExpenses => Transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
    public decimal Balance => TotalIncome - TotalExpenses;

    // Private constructor for EF Core
    public Activity() { }

    public static Activity Create(int reportId, string name, DateTime startDate, string? description = null, DateTime? endDate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Activity name cannot be empty", nameof(name));

        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("A data de fim não pode ser anterior à data de início", nameof(endDate));

        return new Activity
        {
            ReportId = reportId,
            Name = name,
            Description = description,
            StartDate = startDate,
            EndDate = endDate
        };
    }

    public void UpdateDetails(string name, DateTime startDate, string? description, DateTime? endDate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Activity name cannot be empty", nameof(name));

        if (endDate.HasValue && endDate.Value < startDate)
            throw new ArgumentException("A data de fim não pode ser anterior à data de início", nameof(endDate));

        Name = name;
        Description = description;
        StartDate = startDate;
        EndDate = endDate;
    }

    public void SetEndDate(DateTime? endDate)
    {
        if (endDate.HasValue && endDate.Value < StartDate)
            throw new ArgumentException("A data de fim não pode ser anterior à data de início");

        EndDate = endDate;
    }

    /// <summary>
    /// Gets the latest date (EndDate if available, otherwise StartDate) for sorting purposes
    /// </summary>
    public DateTime LatestDate => EndDate ?? StartDate;
}
