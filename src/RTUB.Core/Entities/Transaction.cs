using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a financial transaction (income or expense)
/// </summary>
public class Transaction : BaseEntity
{
    [Range(1, int.MaxValue, ErrorMessage = "ID de Atividade inválido")]
    public int? ActivityId { get; set; }

    [Required(ErrorMessage = "A data é obrigatória")]
    public DateTime Date { get; set; }

    [Required(ErrorMessage = "A descrição é obrigatória")]
    [MaxLength(500, ErrorMessage = "A descrição não pode exceder 500 caracteres")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "A categoria é obrigatória")]
    [MaxLength(100, ErrorMessage = "A categoria não pode exceder 100 caracteres")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "O montante é obrigatório")]
    [Range(0.01, double.MaxValue, ErrorMessage = "O montante deve ser maior que 0")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "O tipo é obrigatório")]
    [RegularExpression("^(Income|Expense)$", ErrorMessage = "O tipo deve ser 'Income' ou 'Expense'")]
    public string Type { get; set; } = "Expense";

    [MaxLength(500, ErrorMessage = "A URL do recibo não pode exceder 500 caracteres")]
    public string? ReceiptUrl { get; set; }

    /// <summary>
    /// Optional foreign key to ApplicationUser for CALOTES tracking
    /// Represents the member who owes money to the Tuna
    /// </summary>
    public string? UserId { get; set; }

    // Navigation properties
    public virtual Activity? Activity { get; set; }
    public virtual ApplicationUser? User { get; set; }

    // Private constructor for EF Core
    public Transaction() { }

    public static Transaction Create(DateTime date, string description, string category, decimal amount, string type, int? activityId = null, string? receiptUrl = null, string? userId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A descrição não pode estar vazia", nameof(description));

        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("A categoria não pode estar vazia", nameof(category));

        if (amount < 0)
            throw new ArgumentException("O montante deve ser positivo", nameof(amount));

        if (type != "Income" && type != "Expense")
            throw new ArgumentException("O tipo deve ser 'Income' ou 'Expense'", nameof(type));

        return new Transaction
        {
            Date = date,
            Description = description,
            Category = category,
            Amount = amount,
            Type = type,
            ActivityId = activityId,
            ReceiptUrl = receiptUrl,
            UserId = userId
        };
    }

    public void SetReceiptUrl(string? receiptUrl)
    {
        ReceiptUrl = receiptUrl;
    }

    public void UpdateDetails(DateTime date, string description, string category, decimal amount, string type, string? userId = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A descrição não pode estar vazia", nameof(description));

        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("A categoria não pode estar vazia", nameof(category));

        if (amount < 0)
            throw new ArgumentException("O montante deve ser positivo", nameof(amount));

        if (type != "Income" && type != "Expense")
            throw new ArgumentException("O tipo deve ser 'Income' ou 'Expense'", nameof(type));

        Date = date;
        Description = description;
        Category = category;
        Amount = amount;
        Type = type;
        UserId = userId;
    }
}
