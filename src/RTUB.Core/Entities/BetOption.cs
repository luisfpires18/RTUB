using System.ComponentModel.DataAnnotations;

namespace RTUB.Core.Entities;

/// <summary>
/// Represents a betting option within a bet
/// For MATCH category: contains two members and their odds
/// For DECISION category: contains custom option title and odds
/// </summary>
public class BetOption : BaseEntity
{
    [Required]
    public int BetId { get; set; }

    [Required(ErrorMessage = "O título da opção é obrigatório")]
    [MaxLength(200, ErrorMessage = "O título da opção não pode exceder 200 caracteres")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "As odds são obrigatórias")]
    [Range(1.01, 999.99, ErrorMessage = "As odds devem estar entre 1.01 e 999.99")]
    public decimal Odds { get; set; }

    // For MATCH category - optional members
    public string? MemberAId { get; set; }
    public string? MemberBId { get; set; }

    // Navigation properties
    public virtual Bet Bet { get; set; } = null!;
    public virtual ApplicationUser? MemberA { get; set; }
    public virtual ApplicationUser? MemberB { get; set; }

    // Private constructor for EF Core
    public BetOption() { }

    // Factory method for DECISION category
    public static BetOption CreateDecisionOption(int betId, string title, decimal odds)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da opção não pode estar vazio", nameof(title));

        if (odds < 1.01m || odds > 999.99m)
            throw new ArgumentException("As odds devem estar entre 1.01 e 999.99", nameof(odds));

        return new BetOption
        {
            BetId = betId,
            Title = title,
            Odds = odds
        };
    }

    // Factory method for MATCH category
    public static BetOption CreateMatchOption(int betId, string title, decimal odds, string memberAId, string memberBId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da opção não pode estar vazio", nameof(title));

        if (odds < 1.01m || odds > 999.99m)
            throw new ArgumentException("As odds devem estar entre 1.01 e 999.99", nameof(odds));

        if (string.IsNullOrWhiteSpace(memberAId))
            throw new ArgumentException("O ID do membro A é obrigatório", nameof(memberAId));

        if (string.IsNullOrWhiteSpace(memberBId))
            throw new ArgumentException("O ID do membro B é obrigatório", nameof(memberBId));

        if (memberAId == memberBId)
            throw new ArgumentException("Os membros A e B devem ser diferentes");

        return new BetOption
        {
            BetId = betId,
            Title = title,
            Odds = odds,
            MemberAId = memberAId,
            MemberBId = memberBId
        };
    }

    // Business methods
    public void UpdateOdds(decimal odds)
    {
        if (odds < 1.01m || odds > 999.99m)
            throw new ArgumentException("As odds devem estar entre 1.01 e 999.99", nameof(odds));

        Odds = odds;
    }

    public void UpdateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("O título da opção não pode estar vazio", nameof(title));

        Title = title;
    }
}
