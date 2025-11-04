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
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal Balance { get; set; }
    
    // Navigation properties
    public virtual Report? Report { get; set; }
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    // Private constructor for EF Core
    public Activity() { }

    public static Activity Create(int reportId, string name, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Activity name cannot be empty", nameof(name));

        return new Activity
        {
            ReportId = reportId,
            Name = name,
            Description = description
        };
    }

    public void UpdateDetails(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Activity name cannot be empty", nameof(name));

        Name = name;
        Description = description;
    }

    public void RecalculateFinancials()
    {
        TotalIncome = Transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
        TotalExpenses = Transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
        Balance = TotalIncome - TotalExpenses;
    }
}
