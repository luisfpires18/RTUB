using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a debt owed by a member to the Tuna
/// Separate from Transaction entity to avoid double-counting
/// </summary>
public class MemberDebt : BaseEntity
{
    [Required(ErrorMessage = "O ID do utilizador é obrigatório")]
    public string UserId { get; set; } = string.Empty;

    [Required(ErrorMessage = "O montante devido é obrigatório")]
    [Range(0.01, double.MaxValue, ErrorMessage = "O montante deve ser maior que 0")]
    public decimal AmountOwed { get; set; }

    [MaxLength(500, ErrorMessage = "A descrição não pode exceder 500 caracteres")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "O ano fiscal é obrigatório")]
    [Range(1, int.MaxValue, ErrorMessage = "ID de Ano Fiscal inválido")]
    public int FiscalYearId { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual FiscalYear FiscalYear { get; set; } = null!;

    // Private constructor for EF Core
    private MemberDebt() { }

    public static MemberDebt Create(string userId, decimal amountOwed, string? description, int fiscalYearId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("O ID do utilizador não pode estar vazio", nameof(userId));

        if (amountOwed <= 0)
            throw new ArgumentException("O montante deve ser maior que 0", nameof(amountOwed));

        if (fiscalYearId <= 0)
            throw new ArgumentException("ID de Ano Fiscal inválido", nameof(fiscalYearId));

        return new MemberDebt
        {
            UserId = userId,
            AmountOwed = amountOwed,
            Description = description,
            FiscalYearId = fiscalYearId
        };
    }

    public void UpdateDetails(decimal amountOwed, string? description)
    {
        if (amountOwed <= 0)
            throw new ArgumentException("O montante deve ser maior que 0", nameof(amountOwed));

        AmountOwed = amountOwed;
        Description = description;
    }
}
